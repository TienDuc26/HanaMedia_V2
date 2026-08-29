using HanaMedia.ViewModels;

namespace HanaMedia.Services.Tasks;

public interface IWorkTaskService
{
    Task<WorkTaskPageViewModel> GetPageAsync(int actorUserId, string actorRole, string? module, string? search, int page, int? employeeId = null, bool openCreate = default, string? status = null, int? reviewerId = null, bool? overdue = null, CancellationToken cancellationToken = default);
    Task<WorkTaskOperationResult> CreateAsync(CreateWorkTaskInputModel input, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<WorkTaskOperationResult> TransitionAsync(TransitionWorkTaskInputModel input, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    
    Task<WorkTaskWorkspaceViewModel> GetWorkspaceDetailsAsync(int taskId, int actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<WorkTaskOperationResult> SaveDraftAsync(int taskId, string draftDataJson, int actorUserId, CancellationToken cancellationToken = default);
    Task<WorkTaskOperationResult> SubmitReviewAsync(int taskId, string result, string notes, int actorUserId, CancellationToken cancellationToken = default);
    Task<WorkTaskOperationResult> ReviewTaskAsync(int taskId, string targetStatus, string? feedback, int reviewerUserId, string reviewerRole, CancellationToken cancellationToken = default);
    Task<List<ReviewerOptionViewModel>> GetAvailableReviewersAsync(CancellationToken cancellationToken = default);
}
