using HanaMedia.ViewModels;

namespace HanaMedia.Services.Config;

public interface IBusinessConfigService
{
    Task<BusinessConfigViewModel> GetAsync(CancellationToken cancellationToken = default);
    Task<BusinessConfigUpdateResult> UpdateAsync(BusinessConfigViewModel input, int actorUserId, CancellationToken cancellationToken = default);
}
