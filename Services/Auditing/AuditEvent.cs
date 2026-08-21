namespace HanaMedia.Services.Auditing;

public sealed record AuditEvent(
    string Module,
    string ActionType,
    string Detail,
    int? UserId = null,
    string? TargetType = null,
    string? TargetId = null,
    AuditSeverity Severity = AuditSeverity.Information);

public enum AuditSeverity
{
    Information,
    Warning,
    Critical
}
