namespace HanaMedia.Services.Auditing;

public interface ISystemAuditService
{
    void AddEvent(AuditEvent auditEvent);

    Task WriteAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default);

    void AddAccountEvent(int? userId, string actionType, string detail);
}

