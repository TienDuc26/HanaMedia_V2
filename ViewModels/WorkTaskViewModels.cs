using System.ComponentModel.DataAnnotations;

namespace HanaMedia.ViewModels;

public sealed class WorkTaskPageViewModel
{
    public string Module { get; init; } = null!;
    public string ModuleLabel { get; init; } = null!;
    public bool CanCreate { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int? SelectedEmployeeId { get; init; }
    public bool OpenCreateModal { get; init; }
    public int ActiveEmployeesWithoutAccount { get; init; }
    public string? SelectedStatus { get; init; }
    public int? SelectedReviewerId { get; init; }
    public bool? SelectedOverdue { get; init; }
    public IReadOnlyList<WorkTaskListItemViewModel> Tasks { get; init; } = [];
    public IReadOnlyList<WorkTaskEmployeeOptionViewModel> Employees { get; init; } = [];
    public IReadOnlyList<WorkTaskModuleOptionViewModel> AvailableModules { get; init; } = [];
    public IReadOnlyList<ReviewerOptionViewModel> Reviewers { get; init; } = [];
}

public sealed class DirectorHumanResourcesViewModel
{
    public string? Search { get; init; }
    public string? Department { get; init; }
    public string? Status { get; init; }
    public IReadOnlyList<DirectorEmployeeListItemViewModel> Employees { get; init; } = [];
    public IReadOnlyList<WorkTaskEmployeeDepartmentViewModel> Departments { get; init; } = [];
}

public sealed record WorkTaskEmployeeDepartmentViewModel(string Code, string Name);

public sealed class DirectorEmployeeListItemViewModel
{
    public int Id { get; init; }
    public string FullName { get; init; } = null!;
    public string Department { get; init; } = null!;
    public string DepartmentName { get; init; } = null!;
    public string Position { get; init; } = null!;
    public DateOnly JoinedDate { get; init; }
    public decimal SalaryAndAllowance { get; init; }
    public string Status { get; init; } = null!;
    public string StatusLabel { get; init; } = null!;
    public bool HasAccount { get; init; }
    public string? TaskModule { get; init; }
}

public sealed class WorkTaskListItemViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = null!;
    public string? Description { get; init; }
    public string Module { get; init; } = null!;
    public string ModuleLabel { get; init; } = null!;
    public string EmployeeName { get; init; } = null!;
    public string CreatorName { get; init; } = null!;
    public string ReviewerName { get; init; } = null!;
    public DateTime Deadline { get; init; }
    public string Status { get; init; } = null!;
    public string StatusLabel { get; init; } = null!;
    public string RowVersion { get; init; } = null!;
    public string? RelatedType { get; init; }
    public int? RelatedId { get; init; }
    public string? RelatedLabel { get; init; }
    public IReadOnlyList<WorkTaskTransitionViewModel> AllowedTransitions { get; init; } = [];
}

public sealed record WorkTaskEmployeeOptionViewModel(int Id, string Name, string Department);
public sealed record WorkTaskModuleOptionViewModel(string Code, string Label);
public sealed record WorkTaskTransitionViewModel(string Status, string Label, bool RequiresComment);

public sealed class CreateWorkTaskInputModel
{
    [Required, StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public string Module { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int AssignedEmployeeId { get; set; }

    [Required]
    public DateTime Deadline { get; set; }

    [StringLength(30)]
    public string? RelatedType { get; set; }

    [Range(1, int.MaxValue)]
    public int? RelatedId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn người duyệt.")]
    public int ReviewerUserId { get; set; }

    public int? CampaignId { get; set; }
}

public sealed class TransitionWorkTaskInputModel
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required]
    public string TargetStatus { get; set; } = string.Empty;

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Comment { get; set; }

    public string Module { get; set; } = string.Empty;
}

public sealed record WorkTaskOperationResult(bool Succeeded, string Message)
{
    public static WorkTaskOperationResult Success(string message) => new(true, message);
    public static WorkTaskOperationResult Failure(string message) => new(false, message);
}

public sealed class WorkTaskWorkspaceViewModel
{
    public WorkTaskListItemViewModel Task { get; set; } = null!;
    public string? DraftData { get; set; }
    public List<WorkTaskSubmissionViewModel> Submissions { get; set; } = [];
    public WorkspaceEmployeeContext? EmployeeContext { get; set; }
    public WorkspaceBookingContext? BookingContext { get; set; }
    public WorkspaceIdeaContext? IdeaContext { get; set; }
    public List<ReviewerOptionViewModel> AvailableReviewers { get; set; } = [];
    public bool CanReview { get; set; }
}

public sealed class WorkTaskSubmissionViewModel
{
    public int Id { get; set; }
    public int Version { get; set; }
    public string SubmittedByName { get; set; } = null!;
    public DateTime SubmittedAt { get; set; }
    public string Result { get; set; } = null!;
    public string? Notes { get; set; }
    public string? Feedback { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string Status { get; set; } = null!;
    public string StatusLabel { get; set; } = null!;
    public List<WorkspaceFileViewModel> Files { get; set; } = [];
}

public sealed class WorkspaceFileViewModel
{
    public string Name { get; set; } = null!;
    public string Url { get; set; } = null!;
    public long Size { get; set; }
    public string UploadedBy { get; set; } = null!;
    public DateTime UploadedAt { get; set; }
}

public sealed class WorkspaceEmployeeContext
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string Email { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string Dob { get; set; } = null!;
    public string JoinedDate { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string Position { get; set; } = null!;
    public string ContractType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? ManagerName { get; set; }
}

public sealed class WorkspaceBookingContext
{
    public int Id { get; set; }
    public string ClientName { get; set; } = null!;
    public string CampaignName { get; set; } = null!;
    public string? KolName { get; set; }
    public string? JobDescription { get; set; }
    public string Deadline { get; set; } = null!;
    public string? PostingDate { get; set; }
    public decimal BookingPrice { get; set; }
    public decimal ActualCost { get; set; }
    public string? PrimaryManagerName { get; set; }
    public string Status { get; set; } = null!;
    public string? ContractFileUrl { get; set; }
    public string? QuotationFileUrl { get; set; }
    public string? PostLink { get; set; }
    public string? Notes { get; set; }
}

public sealed class WorkspaceIdeaContext
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string ClientName { get; set; } = null!;
    public string CampaignName { get; set; } = null!;
    public string Industry { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string? Insight { get; set; }
    public string? Concept { get; set; }
    public string? ContentDetails { get; set; }
    public string? ReferenceLink { get; set; }
    public string? MoodboardDesc { get; set; }
    public string? ScriptText { get; set; }
    public string Deadline { get; set; } = null!;
    public string? CreatorName { get; set; }
    public string? PrimaryStaffName { get; set; }
    public string? ReviewerName { get; set; }
    public string Status { get; set; } = null!;
    public string? FeedbackComment { get; set; }
}

public sealed class ReviewerOptionViewModel
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
}
