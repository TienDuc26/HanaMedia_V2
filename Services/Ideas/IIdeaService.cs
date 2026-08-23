using HanaMedia.ViewModels;

namespace HanaMedia.Services.Ideas;

public interface IIdeaService
{
    Task<IdeaPageViewModel> GetPageAsync(
        int actorUserId, string actorRole, int? actorEmployeeId,
        string? search, string? status, string? client,
        int page,
        CancellationToken cancellationToken = default);

    Task<IdeaDetailViewModel?> GetDetailAsync(
        int ideaId, int actorUserId, string actorRole, int? actorEmployeeId,
        CancellationToken cancellationToken = default);

    Task<IdeaOperationResult> CreateAsync(
        IdeaUpsertInputModel input, int actorUserId, string actorRole, int? actorEmployeeId,
        CancellationToken cancellationToken = default);

    Task<IdeaOperationResult> UpdateAsync(
        IdeaUpsertInputModel input, int actorUserId, string actorRole, int? actorEmployeeId,
        CancellationToken cancellationToken = default);

    Task<IdeaOperationResult> TransitionAsync(
        IdeaTransitionInputModel input, int actorUserId, string actorRole, int? actorEmployeeId,
        CancellationToken cancellationToken = default);

    Task<IdeaOperationResult> AddCommentAsync(
        IdeaCommentInputModel input, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default);
}
