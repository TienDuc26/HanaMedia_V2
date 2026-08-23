using HanaMedia.ViewModels;

namespace HanaMedia.Services.Kols;

public interface IKolService
{
    Task<KolPageViewModel> GetPageAsync(int actorUserId, string actorRole, string? search, string? platform, string? status, int page, CancellationToken cancellationToken = default);
    Task<KolOperationResult> CreateAsync(CreateKolInputModel input, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<KolOperationResult> UpdateAsync(UpdateKolInputModel input, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<KolOperationResult> DeleteAsync(DeleteKolInputModel input, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
}
