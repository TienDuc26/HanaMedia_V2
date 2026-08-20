using HanaMedia.ViewModels;

namespace HanaMedia.Services.Auditing;

public interface IAuditLogQueryService
{
    Task<AuditLogPageViewModel> GetPageAsync(
        AuditLogFilterInputModel filter,
        CancellationToken cancellationToken = default);

    Task<AuditLogExportResult> GetExportRowsAsync(
        AuditLogFilterInputModel filter,
        CancellationToken cancellationToken = default);
}

