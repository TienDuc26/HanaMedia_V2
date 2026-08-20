using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Auditing;

public sealed class AuditLogQueryService : IAuditLogQueryService
{
    private const int PageSize = 20;
    private const int ExportRowLimit = 10_000;
    private const int UserFilterLength = 100;
    private const int ActionFilterLength = 50;
    private const int ModuleFilterLength = 30;

    private static readonly IReadOnlyDictionary<string, string> ActionLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["login"] = "Đăng nhập",
            ["login_succeeded"] = "Đăng nhập",
            ["login_failed"] = "Đăng nhập thất bại",
            ["logout"] = "Đăng xuất",
            ["create"] = "Tạo mới",
            ["account_created"] = "Tạo tài khoản",
            ["edit"] = "Chỉnh sửa",
            ["update"] = "Cập nhật",
            ["delete"] = "Xóa dữ liệu",
            ["approve"] = "Phê duyệt",
            ["change_role"] = "Đổi vai trò",
            ["role_changed"] = "Đổi vai trò",
            ["account_locked"] = "Khóa tài khoản",
            ["account_unlocked"] = "Mở khóa tài khoản",
            ["password_reset"] = "Đặt lại mật khẩu",
            ["config_changed"] = "Thay đổi cấu hình"
        };

    private static readonly IReadOnlyDictionary<string, string> ModuleLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Nhan_Su"] = "Nhân sự",
            ["Booking"] = "Booking",
            ["Y_Tuong"] = "Ý tưởng",
            ["Tai_Khoan"] = "Tài khoản",
            ["Cau_Hinh"] = "Cấu hình"
        };

    private readonly ApplicationDbContext _context;

    public AuditLogQueryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AuditLogPageViewModel> GetPageAsync(
        AuditLogFilterInputModel filter,
        CancellationToken cancellationToken = default)
    {
        var normalizedFilter = NormalizeFilter(filter);
        var query = ApplyFilters(
            _context.SystemAuditLogs.AsNoTracking(),
            normalizedFilter);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        var currentPage = Math.Min(normalizedFilter.Page, totalPages);
        normalizedFilter.Page = currentPage;
        var failureCount = await query.CountAsync(
            log =>
                log.ActionType == "login_failed" ||
                log.ActionType.EndsWith("_failed") ||
                log.ActionType.Contains("failure"),
            cancellationToken);

        var logs = await query
            .Include(log => log.User)
                .ThenInclude(user => user!.Employee)
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var actionCodes = await _context.SystemAuditLogs
            .AsNoTracking()
            .Select(log => log.ActionType)
            .Distinct()
            .ToListAsync(cancellationToken);
        var moduleCodes = await _context.SystemAuditLogs
            .AsNoTracking()
            .Select(log => log.Module)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new AuditLogPageViewModel
        {
            Filter = normalizedFilter,
            Items = logs.Select(MapItem).ToList(),
            TotalCount = totalCount,
            FailureCount = failureCount,
            CurrentPage = currentPage,
            PageSize = PageSize,
            TotalPages = totalPages,
            ActionOptions = actionCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => new AuditLogOptionViewModel(code, GetActionLabel(code)))
                .OrderBy(option => option.Label, StringComparer.CurrentCulture)
                .ThenBy(option => option.Code, StringComparer.Ordinal)
                .ToList(),
            ModuleOptions = moduleCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => new AuditLogOptionViewModel(code, GetModuleLabel(code)))
                .OrderBy(option => option.Label, StringComparer.CurrentCulture)
                .ThenBy(option => option.Code, StringComparer.Ordinal)
                .ToList()
        };
    }

    public async Task<AuditLogExportResult> GetExportRowsAsync(
        AuditLogFilterInputModel filter,
        CancellationToken cancellationToken = default)
    {
        var normalizedFilter = NormalizeFilter(filter);
        var logs = await ApplyFilters(
                _context.SystemAuditLogs.AsNoTracking(),
                normalizedFilter)
            .Include(log => log.User)
                .ThenInclude(user => user!.Employee)
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Take(ExportRowLimit + 1)
            .ToListAsync(cancellationToken);

        var exceedsLimit = logs.Count > ExportRowLimit;
        var rows = logs
            .Take(ExportRowLimit)
            .Select(MapItem)
            .ToList();
        return new AuditLogExportResult(rows, exceedsLimit);
    }

    private static IQueryable<SystemAuditLog> ApplyFilters(
        IQueryable<SystemAuditLog> query,
        AuditLogFilterInputModel filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.User))
        {
            var search = filter.User;
            query = query.Where(log =>
                log.User != null &&
                    (log.User.Username.Contains(search) ||
                     log.User.Email.Contains(search) ||
                     (log.User.Employee != null && log.User.Employee.FullName.Contains(search))));
        }

        if (!string.IsNullOrWhiteSpace(filter.ActionType))
        {
            query = query.Where(log => log.ActionType == filter.ActionType);
        }

        if (!string.IsNullOrWhiteSpace(filter.Module))
        {
            query = query.Where(log => log.Module == filter.Module);
        }

        if (filter.From.HasValue)
        {
            var from = filter.From.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(log => log.CreatedAt >= from);
        }

        if (filter.To.HasValue)
        {
            if (filter.To.Value < DateOnly.MaxValue)
            {
                var until = filter.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
                query = query.Where(log => log.CreatedAt < until);
            }
            else
            {
                var lastSqlDateTime = new DateTime(9999, 12, 31, 23, 59, 59, 997);
                query = query.Where(log => log.CreatedAt <= lastSqlDateTime);
            }
        }

        return query;
    }

    private static AuditLogFilterInputModel NormalizeFilter(AuditLogFilterInputModel? filter)
        => new()
        {
            User = NormalizeTextFilter(filter?.User, UserFilterLength),
            ActionType = NormalizeTextFilter(filter?.ActionType, ActionFilterLength),
            Module = NormalizeTextFilter(filter?.Module, ModuleFilterLength),
            From = filter?.From,
            To = filter?.To,
            Page = filter is not null && filter.Page > 0 ? filter.Page : 1
        };

    private static string? NormalizeTextFilter(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static AuditLogItemViewModel MapItem(SystemAuditLog log)
    {
        var user = log.User;
        return new AuditLogItemViewModel
        {
            OccurredAt = log.CreatedAt,
            ActorDisplayName = user?.Employee?.FullName ?? user?.Username ?? "Hệ thống",
            Username = user?.Username ?? "system",
            RoleName = GetRoleLabel(user?.Role),
            ActionLabel = GetActionLabel(log.ActionType),
            ActionType = log.ActionType,
            BadgeClass = GetBadgeClass(log.ActionType),
            ModuleLabel = GetModuleLabel(log.Module),
            Detail = log.LogDetail,
            IpAddress = log.IpAddress
        };
    }

    private static string GetRoleLabel(string? role)
    {
        if (AppRoles.TryGetLabel(role, out var label))
        {
            return label;
        }

        return string.IsNullOrWhiteSpace(role) ? "Hệ thống" : role;
    }

    private static string GetActionLabel(string code)
        => ActionLabels.TryGetValue(code, out var label) ? label : HumanizeCode(code);

    private static string GetModuleLabel(string code)
        => ModuleLabels.TryGetValue(code, out var label) ? label : HumanizeCode(code);

    private static string GetBadgeClass(string actionType)
    {
        if (actionType.Contains("unlocked", StringComparison.OrdinalIgnoreCase))
        {
            return "badge-success";
        }

        if (actionType.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            actionType.Contains("failure", StringComparison.OrdinalIgnoreCase) ||
            actionType.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
            actionType.Contains("locked", StringComparison.OrdinalIgnoreCase))
        {
            return "badge-danger";
        }

        if (actionType.Contains("create", StringComparison.OrdinalIgnoreCase) ||
            actionType.Contains("approve", StringComparison.OrdinalIgnoreCase) ||
            actionType.Contains("succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return "badge-success";
        }

        if (actionType.Contains("update", StringComparison.OrdinalIgnoreCase) ||
            actionType.Contains("edit", StringComparison.OrdinalIgnoreCase) ||
            actionType.Contains("change", StringComparison.OrdinalIgnoreCase) ||
            actionType.Contains("reset", StringComparison.OrdinalIgnoreCase))
        {
            return "badge-warning";
        }

        return "badge-info";
    }

    private static string HumanizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "Không xác định";
        }

        return code.Replace('_', ' ').Trim();
    }
}
