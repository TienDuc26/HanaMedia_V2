namespace HanaMedia.Services.Auditing;

public interface ISystemAuditService
{
    void AddAccountEvent(int? userId, string actionType, string detail);
}

