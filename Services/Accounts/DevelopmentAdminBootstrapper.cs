using System.Data;
using System.Net.Mail;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.Services.Security;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Accounts;

public sealed class DevelopmentAdminBootstrapper
{
    private readonly ApplicationDbContext _context;
    private readonly IAccountPasswordService _passwordService;
    private readonly ISystemAuditService _auditService;

    public DevelopmentAdminBootstrapper(
        ApplicationDbContext context,
        IAccountPasswordService passwordService,
        ISystemAuditService auditService)
    {
        _context = context;
        _passwordService = passwordService;
        _auditService = auditService;
    }

    internal static AdminBootstrapCredentials ReadConfiguration(IConfiguration configuration)
    {
        var username = ReadRequiredValue(configuration, "BootstrapAdmin:Username").Trim();
        var email = ReadRequiredValue(configuration, "BootstrapAdmin:Email").Trim();
        var password = ReadRequiredValue(configuration, "BootstrapAdmin:Password");

        Validate(username, email, password);
        return new AdminBootstrapCredentials(username, email, password);
    }

    internal async Task<AdminBootstrapResult> RunAsync(
        AdminBootstrapCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await _context.Database.ExecuteSqlRawAsync(
            """
            DECLARE @lock_result INT;
            EXEC @lock_result = sys.sp_getapplock
                @Resource = N'HanaMedia:BootstrapAdmin',
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 15000;
            IF @lock_result < 0
                THROW 50020, 'Không thể khóa tiến trình bootstrap AdminIT.', 1;
            """,
            cancellationToken);

        if (await _context.Users.AnyAsync(
                user => user.Role == AppRoles.AdminIT && user.Status == AccountStatuses.Active,
                cancellationToken))
        {
            return new AdminBootstrapResult(
                false,
                "Database đã có ít nhất một AdminIT đang hoạt động; không tạo thêm tài khoản bootstrap.");
        }

        if (await _context.Users.AnyAsync(
                user => user.Username == credentials.Username || user.Email == credentials.Email,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Username hoặc email bootstrap đã được sử dụng bởi một tài khoản khác.");
        }

        var now = DateTime.Now;
        var user = new User
        {
            Username = credentials.Username,
            Email = credentials.Email,
            Role = AppRoles.AdminIT,
            Status = AccountStatuses.Active,
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = _passwordService.HashPassword(user, credentials.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        _auditService.AddAccountEvent(
            null,
            "bootstrap_admin_created",
            $"Tạo tài khoản AdminIT bootstrap {user.Username} trong môi trường Development.");
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdminBootstrapResult(true, $"Đã tạo AdminIT bootstrap {user.Username}.");
    }

    private static string ReadRequiredValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Thiếu cấu hình {key}. Hãy đặt bằng User Secrets hoặc biến môi trường.")
            : value;
    }

    private static void Validate(string username, string email, string password)
    {
        if (username.Length is < 3 or > 50 ||
            username.Any(character =>
                character is not (>= 'a' and <= 'z') and
                not (>= 'A' and <= 'Z') and
                not (>= '0' and <= '9') and
                not '.' and not '_' and not '-'))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:Username phải dài 3-50 ký tự và chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.");
        }

        if (email.Length > 100 ||
            !MailAddress.TryCreate(email, out var parsedEmail) ||
            !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("BootstrapAdmin:Email không hợp lệ.");
        }

        if (password.Length < 12 ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsDigit) ||
            !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:Password phải có ít nhất 12 ký tự, gồm chữ thường, chữ hoa, số và ký tự đặc biệt.");
        }
    }
}

public sealed record AdminBootstrapResult(bool Created, string Message);

internal sealed record AdminBootstrapCredentials(
    string Username,
    string Email,
    string Password);
