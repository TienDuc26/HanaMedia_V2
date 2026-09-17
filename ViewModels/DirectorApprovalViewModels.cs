using HanaMedia.Models;

namespace HanaMedia.ViewModels;

public sealed class DirectorApprovalViewModel
{
    public IReadOnlyList<Booking> BookingApprovals { get; init; } = [];
    public IReadOnlyList<DirectorTaskApprovalRowViewModel> TaskApprovals { get; init; } = [];
    public int TotalCount => BookingApprovals.Count + TaskApprovals.Count;
    public int BookingCount => BookingApprovals.Count + TaskApprovals.Count(item => item.Module == Constants.WorkTaskModules.Booking);
    public int HumanResourcesCount => TaskApprovals.Count(item => item.Module == Constants.WorkTaskModules.HumanResources);
}

public sealed class DirectorTaskApprovalRowViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Module { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public string CreatedByName { get; init; } = string.Empty;
    public DateTime Deadline { get; init; }
    public DateTime SubmittedAt { get; init; }
}
