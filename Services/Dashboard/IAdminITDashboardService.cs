using HanaMedia.ViewModels;

namespace HanaMedia.Services.Dashboard;

public interface IAdminITDashboardService
{
    Task<AdminITDashboardViewModel> GetAsync(
        string? period,
        string? companyPeriod,
        CancellationToken cancellationToken = default);
}
