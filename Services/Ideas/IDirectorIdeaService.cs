using HanaMedia.ViewModels;

namespace HanaMedia.Services.Ideas;

public interface IDirectorIdeaService
{
    Task<DirectorIdeaPageViewModel> GetPageAsync(
        string? search,
        string? status,
        string? directorStatus,
        int page,
        CancellationToken cancellationToken = default);

    Task<IdeaOperationResult> UpdateContentAsync(
        DirectorEditIdeaInputModel input,
        int actorUserId,
        CancellationToken cancellationToken = default);

    Task<IdeaOperationResult> SendFeedbackAsync(
        int id,
        string? feedback,
        int actorUserId,
        CancellationToken cancellationToken = default);

    Task<IdeaOperationResult> DecideAsync(
        int id,
        string decision,
        string? reason,
        int actorUserId,
        CancellationToken cancellationToken = default);
}
