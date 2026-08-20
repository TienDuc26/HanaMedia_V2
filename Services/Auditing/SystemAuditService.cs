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

    public void AddAccountEvent(int? userId, string actionType, string detail)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var deviceInfo = httpContext?.Request.Headers.UserAgent.ToString();

        _context.SystemAuditLogs.Add(new SystemAuditLog
        {
            UserId = userId,
            ActionType = Truncate(actionType, 50),
            Module = "Tai_Khoan",
            LogDetail = detail,
            IpAddress = Truncate(ipAddress, 45),
            DeviceInfo = string.IsNullOrWhiteSpace(deviceInfo) ? null : Truncate(deviceInfo, 255),
            CreatedAt = DateTime.Now
        });
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

