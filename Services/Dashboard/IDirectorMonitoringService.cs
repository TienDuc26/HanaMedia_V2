using HanaMedia.ViewModels;

namespace HanaMedia.Services.Dashboard;

public interface IDirectorMonitoringService
{
    Task<DirectorMonitoringViewModel> GetAsync(
        CancellationToken cancellationToken = default);
}
