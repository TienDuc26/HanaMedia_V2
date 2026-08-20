using System.Data;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.Services.Security;
using HanaMedia.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Accounts;

public sealed class AccountManagementService : IAccountManagementService
{
    private const int LoginHistoryPageSize = 20;
    private static readonly string[] ActiveEmployeeStatuses = ["dang_lam_viec", "thu_viec"];
    private static readonly string[] LoginActions = ["login_succeeded", "login_failed", "logout"];

    private readonly ApplicationDbContext _context;
    private readonly IAccountPasswordService _passwordService;
    private readonly ISystemAuditService _auditService;

    public AccountManagementService(
        ApplicationDbContext context,
        IAccountPasswordService passwordService,
        ISystemAuditService auditService)
    {
        _context = context;
        _passwordService = passwordService;
        _auditService = auditService;
    }

    public async Task<AccountManagementPageViewModel> GetPageAsync(
        string? search,
        int? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = search?.Trim();
        var accountsQuery = _context.Users
            .AsNoTracking()
            .Include(user => user.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            accountsQuery = accountsQuery.Where(user =>
                EF.Functions.Like(user.Username, pattern) ||
                EF.Functions.Like(user.Email, pattern) ||
                EF.Functions.Like(user.Role, pattern) ||
                (user.Employee != null && EF.Functions.Like(user.Employee.FullName, pattern)));
        }

        var users = await accountsQuery
            .OrderBy(user => user.Username)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(user => user.Id).ToArray();
        var lastLoginRows = await _context.SystemAuditLogs
            .AsNoTracking()
            .Where(log =>
                log.UserId.HasValue &&
                userIds.Contains(log.UserId.Value) &&
                log.Module == "Tai_Khoan" &&
                log.ActionType == "login_succeeded")
            .GroupBy(log => log.UserId!.Value)
            .Select(group => group
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.Id)
                .Select(log => new { UserId = log.UserId!.Value, log.CreatedAt, log.IpAddress })
                .First())
            .ToListAsync(cancellationToken);

        var lastLoginByUser = lastLoginRows.ToDictionary(log => log.UserId);
        var activeAdminCount = await _context.Users
            .AsNoTracking()
            .CountAsync(
                user => user.Role == AppRoles.AdminIT && user.Status == AccountStatuses.Active,
                cancellationToken);

        var accounts = users.Select(user =>
        {
            lastLoginByUser.TryGetValue(user.Id, out var lastLogin);
            var isCurrentAccount = currentUserId == user.Id;
            var isLastActiveAdmin =
                user.Role == AppRoles.AdminIT &&
                user.Status == AccountStatuses.Active &&
                activeAdminCount <= 1;

            return new AccountListItemViewModel
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.Employee?.FullName ?? user.Email,
                RoleCode = user.Role,
                RoleName = AppRoles.GetLabel(user.Role),
                Status = user.Status ?? AccountStatuses.Locked,
                SecurityStamp = user.SecurityStamp,
                LastLoginAt = lastLogin?.CreatedAt,
                LastLoginIp = lastLogin?.IpAddress,
                IsCurrentAccount = isCurrentAccount,
                CanLock = !isCurrentAccount && !isLastActiveAdmin,
                CanChangeRole = !isCurrentAccount && !isLastActiveAdmin
            };
        }).ToList();

        var eligibleEmployees = await _context.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.UserId == null &&
                ActiveEmployeeStatuses.Contains(employee.Status!) &&
                !_context.Users.Any(user => user.Email == employee.Email))
            .OrderBy(employee => employee.FullName)
            .Select(employee => new EmployeeOptionViewModel
            {
                Id = employee.Id,
                DisplayName = employee.FullName,
                Email = employee.Email,
                SuggestedUsername = BuildBaseUsername(employee.Email, employee.Id)
            })
            .ToListAsync(cancellationToken);

        return new AccountManagementPageViewModel
        {
            Accounts = accounts,
            EligibleEmployees = eligibleEmployees,
            Roles = AppRoles.All
                .Select(role => new RoleOptionViewModel(role, AppRoles.GetLabel(role)))
                .ToList(),
            Search = normalizedSearch
        };
    }

    public async Task<AccountOperationResult> CreateAccountAsync(
        CreateAccountInputModel input,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var username = (input.Username ?? string.Empty).Trim();
        if (username.Length is < 3 or > 50 ||
            !username.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'))
        {
            return Failure("Tên đăng nhập phải dài từ 3 đến 50 ký tự và chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.");
        }

        if (!AppRoles.IsValid(input.Role))
        {
            return Failure("Vai trò được chọn không hợp lệ.");
        }

        if (string.IsNullOrEmpty(input.InitialPassword) || input.InitialPassword.Length is < 8 or > 100)
        {
            return Failure("Mật khẩu khởi tạo phải có từ 8 đến 100 ký tự.");
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(item => item.Id == input.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return Failure("Không tìm thấy hồ sơ nhân viên.");
        }

        if (employee.UserId.HasValue)
        {
            return Failure("Nhân viên này đã được cấp tài khoản.");
        }

        if (!ActiveEmployeeStatuses.Contains(employee.Status ?? string.Empty))
        {
            return Failure("Chỉ có thể cấp tài khoản cho nhân viên đang làm việc hoặc thử việc.");
        }

        if (await _context.Users.AnyAsync(user => user.Email == employee.Email, cancellationToken))
        {
            return Failure("Email của nhân viên đã được một tài khoản khác sử dụng.");
        }

        if (await _context.Users.AnyAsync(user => user.Username == username, cancellationToken))
        {
            return Failure("Tên đăng nhập đã được sử dụng.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.Now;
            var user = new User
            {
                Username = username,
                Email = employee.Email,
                Role = input.Role,
                Status = AccountStatuses.Active,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = now,
                UpdatedAt = now
            };

            user.PasswordHash = _passwordService.HashPassword(user, input.InitialPassword);
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            employee.UserId = user.Id;
            employee.UpdatedAt = now;
            _auditService.AddAccountEvent(
                actorUserId,
                "account_created",
                $"Tạo tài khoản {user.Username} cho nhân viên {employee.FullName}; vai trò {AppRoles.GetLabel(user.Role)}.");

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new AccountOperationResult(
                true,
                $"Đã tạo tài khoản {user.Username}.",
                null,
                user.Username);
        }
        catch (DbUpdateException)
        {
            return Failure("Không thể tạo tài khoản do username hoặc email đã tồn tại.");
        }
    }

    public async Task<AccountOperationResult> ChangeRoleAsync(
        ChangeRoleInputModel input,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!AppRoles.IsValid(input.Role))
        {
            return Failure("Vai trò được chọn không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(input.ExpectedSecurityStamp))
        {
            return Failure("Dữ liệu tài khoản không hợp lệ. Vui lòng tải lại trang.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(
                item => item.Id == input.UserId,
                cancellationToken);
            if (user is null)
            {
                return Failure("Không tìm thấy tài khoản.");
            }

            if (user.SecurityStamp != input.ExpectedSecurityStamp)
            {
                return StaleDataFailure();
            }

            if (user.Id == actorUserId)
            {
                return Failure("Không thể tự thay đổi vai trò của tài khoản đang đăng nhập.");
            }

            if (user.Role == input.Role)
            {
                return Failure("Tài khoản đang có vai trò này.");
            }

            if (user.Role == AppRoles.AdminIT &&
                user.Status == AccountStatuses.Active &&
                input.Role != AppRoles.AdminIT &&
                await IsLastActiveAdminAsync(user.Id, cancellationToken))
            {
                return Failure("Không thể hạ quyền AdminIT đang hoạt động cuối cùng.");
            }

            var previousRole = user.Role;
            user.Role = input.Role;
            TouchSecurityState(user);
            _auditService.AddAccountEvent(
                actorUserId,
                "role_changed",
                $"Đổi vai trò tài khoản {user.Username} từ {AppRoles.GetLabel(previousRole)} sang {AppRoles.GetLabel(user.Role)}.");

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success($"Đã cập nhật vai trò cho {user.Username}.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return StaleDataFailure();
        }
        catch (Exception exception) when (IsSqlServerDeadlock(exception))
        {
            return ConcurrentOperationFailure();
        }
    }

    public async Task<AccountOperationResult> SetStatusAsync(
        int userId,
        string targetStatus,
        string expectedSecurityStamp,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!AccountStatuses.IsValid(targetStatus))
        {
            return Failure("Trạng thái tài khoản không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(expectedSecurityStamp))
        {
            return Failure("Dữ liệu tài khoản không hợp lệ. Vui lòng tải lại trang.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
            if (user is null)
            {
                return Failure("Không tìm thấy tài khoản.");
            }

            if (user.SecurityStamp != expectedSecurityStamp)
            {
                return StaleDataFailure();
            }

            if (user.Status == targetStatus)
            {
                return Failure($"Tài khoản đã ở trạng thái {(targetStatus == AccountStatuses.Active ? "hoạt động" : "đã khóa")}.");
            }

            var isLocking = targetStatus == AccountStatuses.Locked;
            if (user.Id == actorUserId && isLocking)
            {
                return Failure("Không thể tự khóa tài khoản đang đăng nhập.");
            }

            if (isLocking &&
                user.Role == AppRoles.AdminIT &&
                await IsLastActiveAdminAsync(user.Id, cancellationToken))
            {
                return Failure("Không thể khóa AdminIT đang hoạt động cuối cùng.");
            }

            user.Status = targetStatus;
            TouchSecurityState(user);
            _auditService.AddAccountEvent(
                actorUserId,
                isLocking ? "account_locked" : "account_unlocked",
                $"{(isLocking ? "Khóa" : "Mở khóa")} tài khoản {user.Username}.");

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success($"Đã {(isLocking ? "khóa" : "mở khóa")} tài khoản {user.Username}.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return StaleDataFailure();
        }
        catch (Exception exception) when (IsSqlServerDeadlock(exception))
        {
            return ConcurrentOperationFailure();
        }
    }

    public async Task<AccountOperationResult> ResetPasswordAsync(
        int userId,
        string expectedSecurityStamp,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return Failure("Không tìm thấy tài khoản.");
        }

        if (string.IsNullOrWhiteSpace(expectedSecurityStamp) || user.SecurityStamp != expectedSecurityStamp)
        {
            return StaleDataFailure();
        }

        if (user.Id == actorUserId)
        {
            return Failure("Không thể tự reset mật khẩu của tài khoản đang đăng nhập.");
        }

        var temporaryPassword = _passwordService.GenerateTemporaryPassword();
        user.PasswordHash = _passwordService.HashPassword(user, temporaryPassword);
        TouchSecurityState(user);
        _auditService.AddAccountEvent(
            actorUserId,
            "password_reset",
            $"Reset mật khẩu cho tài khoản {user.Username}.");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return new AccountOperationResult(
                true,
                $"Đã reset mật khẩu cho {user.Username}.",
                temporaryPassword,
                user.Username);
        }
        catch (DbUpdateConcurrencyException)
        {
            return StaleDataFailure();
        }
    }

    public async Task<LoginHistoryResponseViewModel?> GetLoginHistoryAsync(
        int userId,
        int page,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(item => item.Employee)
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var historyQuery = _context.SystemAuditLogs
            .AsNoTracking()
            .Where(log =>
                log.UserId == userId &&
                log.Module == "Tai_Khoan" &&
                LoginActions.Contains(log.ActionType));

        var totalCount = await historyQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)LoginHistoryPageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);

        var logs = await historyQuery
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Skip((currentPage - 1) * LoginHistoryPageSize)
            .Take(LoginHistoryPageSize)
            .ToListAsync(cancellationToken);

        return new LoginHistoryResponseViewModel
        {
            DisplayName = user.Employee?.FullName ?? user.Email,
            Username = user.Username,
            CurrentPage = currentPage,
            TotalPages = totalPages,
            TotalCount = totalCount,
            PageSize = LoginHistoryPageSize,
            Items = logs.Select(log => new LoginHistoryItemViewModel
            {
                OccurredAt = (log.CreatedAt ?? DateTime.Now).ToString("dd/MM/yyyy HH:mm:ss"),
                Action = log.ActionType,
                Label = GetLoginActionLabel(log.ActionType),
                IpAddress = log.IpAddress,
                DeviceInfo = log.DeviceInfo ?? "Không xác định",
                Detail = log.LogDetail
            }).ToList()
        };
    }

    private async Task<string> BuildUniqueUsernameAsync(
        string email,
        int employeeId,
        CancellationToken cancellationToken)
    {
        var baseUsername = BuildBaseUsername(email, employeeId);
        var candidate = baseUsername;
        var suffix = 1;

        while (await _context.Users.AnyAsync(user => user.Username == candidate, cancellationToken))
        {
            var suffixText = $"_{suffix++}";
            candidate = $"{baseUsername[..Math.Min(baseUsername.Length, 50 - suffixText.Length)]}{suffixText}";
        }

        return candidate;
    }

    private static string BuildBaseUsername(string email, int employeeId)
    {
        var localPart = email.Split('@', 2)[0].Trim().ToLowerInvariant();
        var sanitized = new string(localPart
            .Where(character =>
                character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = $"employee{employeeId}";
        }

        return sanitized[..Math.Min(sanitized.Length, 50)];
    }

    private async Task<bool> IsLastActiveAdminAsync(int userId, CancellationToken cancellationToken)
        => !await _context.Users.AnyAsync(
            user =>
                user.Id != userId &&
                user.Role == AppRoles.AdminIT &&
                user.Status == AccountStatuses.Active,
            cancellationToken);

    private static void TouchSecurityState(User user)
    {
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.UpdatedAt = DateTime.Now;
    }

    private static string GetLoginActionLabel(string actionType)
        => actionType switch
        {
            "login_succeeded" => "Đăng nhập",
            "login_failed" => "Đăng nhập thất bại",
            "logout" => "Đăng xuất",
            _ => actionType
        };

    private static AccountOperationResult Success(string message) => new(true, message);

    private static AccountOperationResult Failure(string message) => new(false, message);

    private static AccountOperationResult StaleDataFailure()
        => Failure("Tài khoản vừa được thay đổi ở nơi khác. Vui lòng tải lại trang và thử lại.");

    private static AccountOperationResult ConcurrentOperationFailure()
        => Failure("Có thao tác quản trị khác đang diễn ra. Vui lòng tải lại trang và thử lại.");

    private static bool IsSqlServerDeadlock(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 })
            {
                return true;
            }
        }

        return false;
    }
}
