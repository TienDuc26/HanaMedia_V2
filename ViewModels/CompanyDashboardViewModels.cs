namespace HanaMedia.ViewModels;

public sealed class CompanyDashboardViewModel
{
    public string Period { get; init; } = "month";
    public string PeriodLabel { get; init; } = "Tháng này";
    public DateTime RangeStart { get; init; }
    public DateTime RangeEndExclusive { get; init; }
    public DateTime GeneratedAt { get; init; }

    public int TotalEmployees { get; init; }
    public int NewEmployees { get; init; }
    public int DepartedEmployees { get; init; }
    public int BookingCount { get; init; }
    public int RunningBookings { get; init; }
    public decimal BookingRevenue { get; init; }
    public decimal BookingCost { get; init; }
    public int RunningCampaigns { get; init; }
    public int PendingIdeas { get; init; }
    public int OverdueTasks { get; init; }

    public IReadOnlyList<CompanyDashboardDepartmentViewModel> Departments { get; init; } = [];
    public IReadOnlyList<CompanyDashboardDepartmentPerformanceViewModel> DepartmentPerformance { get; init; } = [];
    public IReadOnlyList<CompanyDashboardBookingStatusViewModel> BookingStatuses { get; init; } = [];
    public IReadOnlyList<CompanyDashboardRevenuePointViewModel> RevenueSeries { get; init; } = [];
    public IReadOnlyList<CompanyDashboardEmployeeChangeViewModel> EmployeeChanges { get; init; } = [];

    public decimal BookingProfit => BookingRevenue - BookingCost;
    public decimal ProfitMargin => BookingRevenue == 0
        ? 0
        : Math.Round(BookingProfit * 100m / BookingRevenue, 1);
}

public sealed class CompanyDashboardDepartmentViewModel
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int EmployeeCount { get; init; }
    public decimal Percentage { get; init; }
}

public sealed class CompanyDashboardDepartmentPerformanceViewModel
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int TotalTasks { get; init; }
    public int CompletedTasks { get; init; }
    public decimal CompletionRate { get; init; }
}

public sealed class CompanyDashboardBookingStatusViewModel
{
    public string Status { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class CompanyDashboardRevenuePointViewModel
{
    public string Label { get; init; } = string.Empty;
    public decimal Revenue { get; init; }
    public decimal Cost { get; init; }
    public decimal Profit => Revenue - Cost;
}

public sealed class CompanyDashboardEmployeeChangeViewModel
{
    public int EmployeeId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public DateTime EventDate { get; init; }
    public string ChangeType { get; init; } = string.Empty;
    public string ChangeLabel { get; init; } = string.Empty;
}
