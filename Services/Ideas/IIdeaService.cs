using HanaMedia.ViewModels;

namespace HanaMedia.Services.Ideas;

public interface IIdeaService
{
    Task<IdeaPageViewModel> GetPageAsync(int actorUserId, string actorRole, string? search, string? status, int page, CancellationToken cancellationToken = default);
    Task<IdeaOperationResult> CreateAsync(SaveIdeaInputModel input, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<IdeaOperationResult> UpdateAsync(SaveIdeaInputModel input, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<IdeaOperationResult> SubmitAsync(int id, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<IdeaOperationResult> ReviewAsync(int id, string decision, string? feedback, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<IdeaOperationResult> AdvanceAsync(int id, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<IdeaOperationResult> AddCommentAsync(int id, string? content, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<IdeaOperationResult> DeleteAsync(int id, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<IdeaOperationResult> DeleteMoodboardImageAsync(long imageId, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
}
