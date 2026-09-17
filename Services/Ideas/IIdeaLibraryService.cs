using HanaMedia.ViewModels;

namespace HanaMedia.Services.Ideas;

public interface IIdeaLibraryService
{
    Task<IdeaLibraryPageViewModel> GetPageAsync(
        int actorUserId,
        string actorRole,
        IdeaLibraryQueryModel query,
        CancellationToken cancellationToken = default);

    Task<IdeaLibraryItemViewModel?> GetByIdAsync(
        int id,
        int actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task<IdeaOperationResult> UpdateClassificationAsync(
        int id,
        UpdateIdeaClassificationInputModel input,
        int actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);
}
