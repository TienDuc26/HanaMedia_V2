using HanaMedia.ViewModels;

namespace HanaMedia.Services.Tasks;

public interface IWorkTaskService
{
    Task<WorkTaskPageViewModel> GetPageAsync(int actorUserId, string actorRole, string? module, string? search, int page, int? employeeId = null, bool openCreate = false, CancellationToken cancellationToken = default);
    Task<WorkTaskOperationResult> CreateAsync(CreateWorkTaskInputModel input, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<WorkTaskOperationResult> TransitionAsync(TransitionWorkTaskInputModel input, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
}
