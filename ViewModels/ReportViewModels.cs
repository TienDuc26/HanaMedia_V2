namespace HanaMedia.ViewModels;

public static class ReportTypes
{
    public const string HumanResources = "human_resources";
    public const string Booking = "booking";
    public const string Ideas = "ideas";
}

public sealed class ReportPageViewModel
{
    public string ReportType { get; init; } = ReportTypes.HumanResources;
    public string Period { get; init; } = "month";
    public string PeriodLabel { get; init; } = "Tháng này";
    public bool IsDirector { get; init; }
    public bool IsLimited { get; init; }
    public HumanResourcesReportViewModel? HumanResources { get; init; }
    public BookingReportViewModel? Booking { get; init; }
    public IdeaReportViewModel? Ideas { get; init; }
}

public sealed class HumanResourcesReportViewModel
{
    public int TotalEmployees { get; init; }
    public int NewEmployees { get; init; }
    public int DepartedEmployees { get; init; }
    public decimal RetentionRate { get; init; }
    public IReadOnlyList<ReportBreakdownRowViewModel> Departments { get; init; } = [];
    public IReadOnlyList<ReportBreakdownRowViewModel> ContractTypes { get; init; } = [];
    public IReadOnlyList<EmployeePerformanceReportRowViewModel> EmployeePerformance { get; init; } = [];
}

public sealed class BookingReportViewModel
{
    public int TotalBookings { get; init; }
    public int CompletedBookings { get; init; }
    public int OverdueBookings { get; init; }
    public decimal Revenue { get; init; }
    public decimal Cost { get; init; }
    public decimal Profit => Revenue - Cost;
    public decimal MyWage { get; init; }
    public IReadOnlyList<ReportBreakdownRowViewModel> Statuses { get; init; } = [];
    public IReadOnlyList<BookingReportRowViewModel> Rows { get; init; } = [];
}

public sealed class IdeaReportViewModel
{
    public int TotalIdeas { get; init; }
    public int ApprovedIdeas { get; init; }
    public int RevisionIdeas { get; init; }
    public int PendingIdeas { get; init; }
    public decimal ApprovalRate { get; init; }
    public IReadOnlyList<ReportBreakdownRowViewModel> Statuses { get; init; } = [];
    public IReadOnlyList<IdeaPerformanceReportRowViewModel> EmployeePerformance { get; init; } = [];
    public IReadOnlyList<IdeaReportRowViewModel> Rows { get; init; } = [];
}

public sealed class ReportBreakdownRowViewModel
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal Percentage { get; init; }
}

public sealed class EmployeePerformanceReportRowViewModel
{
    public int EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;
    public int TotalTasks { get; init; }
    public int CompletedTasks { get; init; }
    public int OverdueTasks { get; init; }
    public decimal CompletionRate { get; init; }
}

public sealed class BookingReportRowViewModel
{
    public int Id { get; init; }
    public string ClientCampaign { get; init; } = string.Empty;
    public string KolName { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public DateOnly Deadline { get; init; }
    public decimal Revenue { get; init; }
    public decimal Cost { get; init; }
    public decimal AllocatedWage { get; init; }
    public decimal MyWage { get; init; }
}

public sealed class IdeaPerformanceReportRowViewModel
{
    public int? EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public int TotalIdeas { get; init; }
    public int ApprovedIdeas { get; init; }
    public int RevisionIdeas { get; init; }
    public decimal ApprovalRate { get; init; }
}

public sealed class IdeaReportRowViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ClientCampaign { get; init; } = string.Empty;
    public string OwnerName { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string DirectorStatusLabel { get; init; } = string.Empty;
    public DateOnly Deadline { get; init; }
}
