using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Models;

namespace HanaMedia.Services.Auditing;

public sealed class SystemAuditService : ISystemAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SystemAuditService(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddEvent(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        Validate(auditEvent);

        var httpContext = _httpContextAccessor.HttpContext;
        var userId = auditEvent.UserId ?? ResolveCurrentUserId(httpContext);
        var ipAddress = NormalizeIpAddress(
            httpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        var deviceInfo = httpContext?.Request.Headers.UserAgent.ToString();

        _context.SystemAuditLogs.Add(new SystemAuditLog
        {
            UserId = userId,
            ActionType = auditEvent.ActionType.Trim(),
            Module = auditEvent.Module,
            LogDetail = FormatDetail(auditEvent),
            IpAddress = Truncate(ipAddress, 45),
            DeviceInfo = string.IsNullOrWhiteSpace(deviceInfo) ? null : Truncate(deviceInfo, 255),
            CreatedAt = DateTime.Now
        });
    }

    public async Task WriteAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        AddEvent(auditEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public void AddAccountEvent(int? userId, string actionType, string detail)
        => AddEvent(new AuditEvent(
            AuditModules.Accounts,
            actionType,
            detail,
            userId));

    private static void Validate(AuditEvent auditEvent)
    {
        if (!AuditModules.IsValid(auditEvent.Module))
        {
            throw new ArgumentException("Phân hệ audit không hợp lệ.", nameof(auditEvent));
        }

        var actionType = auditEvent.ActionType?.Trim();
        if (string.IsNullOrWhiteSpace(actionType) || actionType.Length > 50 ||
            actionType.Any(character =>
                character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '_'))
        {
            throw new ArgumentException("Mã hành động audit không hợp lệ.", nameof(auditEvent));
        }

        if (string.IsNullOrWhiteSpace(auditEvent.Detail))
        {
            throw new ArgumentException("Nội dung audit không được để trống.", nameof(auditEvent));
        }
    }

    private static int? ResolveCurrentUserId(HttpContext? httpContext)
    {
        var identifier = httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(
            identifier,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var userId)
            ? userId
            : null;
    }

    private static string FormatDetail(AuditEvent auditEvent)
    {
        var detail = auditEvent.Detail.Trim();
        var metadata = new List<string>();

        if (!string.IsNullOrWhiteSpace(auditEvent.TargetType))
        {
            var target = auditEvent.TargetType.Trim();
            if (!string.IsNullOrWhiteSpace(auditEvent.TargetId))
            {
                target += $"#{auditEvent.TargetId.Trim()}";
            }

            metadata.Add($"Đối tượng: {target}");
        }

        if (auditEvent.Severity != AuditSeverity.Information)
        {
            metadata.Add($"Mức độ: {(auditEvent.Severity == AuditSeverity.Critical ? "Nghiêm trọng" : "Cảnh báo")}");
        }

        var prefix = metadata.Count == 0 ? string.Empty : $"[{string.Join("] [", metadata)}] ";
        return Truncate(prefix + detail, 4000);
    }

    private static string NormalizeIpAddress(string ipAddress)
        => ipAddress.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase)
            ? ipAddress[7..]
            : ipAddress;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

