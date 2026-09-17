using HanaMedia.ViewModels;

namespace HanaMedia.Services.Dashboard;

public interface ICompanyDashboardService
{
    Task<CompanyDashboardViewModel> GetAsync(
        string? period,
        CancellationToken cancellationToken = default);
}
