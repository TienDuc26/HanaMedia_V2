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
    public IReadOnlyList<WorkTaskListItemViewModel> Tasks { get; init; } = [];
    public IReadOnlyList<WorkTaskEmployeeOptionViewModel> Employees { get; init; } = [];
    public IReadOnlyList<WorkTaskModuleOptionViewModel> AvailableModules { get; init; } = [];
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
