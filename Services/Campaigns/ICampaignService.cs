using HanaMedia.ViewModels;

namespace HanaMedia.Services.Campaigns;

public interface ICampaignService
{
    Task<CampaignPageViewModel> GetPageAsync(string actorRole, string? search, string? status, int page, CancellationToken cancellationToken = default);
    Task<CampaignOperationResult> CreateAsync(CreateCampaignInputModel input, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<CampaignOperationResult> UpdateAsync(UpdateCampaignInputModel input, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<CampaignOperationResult> DeleteAsync(DeleteCampaignInputModel input, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
}
