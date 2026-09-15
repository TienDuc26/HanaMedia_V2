using HanaMedia.ViewModels;

namespace HanaMedia.Services.Reports;

public interface IReportService
{
    Task<ReportPageViewModel?> GetAsync(
        string? reportType,
        string? period,
        int actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task<byte[]?> ExportCsvAsync(
        string? reportType,
        string? period,
        int actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);
}
