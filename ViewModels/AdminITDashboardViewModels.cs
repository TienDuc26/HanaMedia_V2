namespace HanaMedia.ViewModels;

public sealed class AdminITDashboardViewModel
{
    public string Period { get; init; } = "today";
    public string PeriodLabel { get; init; } = "Hôm nay";
    public int LoginCount { get; init; }
    public string LoginComparisonText { get; init; } = "Không có dữ liệu kỳ trước";
    public bool IsLoginComparisonUp { get; init; }
    public int ActiveUserCount { get; init; }
    public int BlockedNetworkCount { get; init; }
    public int SecurityAlertCount { get; init; }
    public IReadOnlyList<AdminITSecurityAlertViewModel> SecurityAlerts { get; init; } = [];
    public IReadOnlyList<AdminITRecentSessionViewModel> RecentSessions { get; init; } = [];
}

public sealed class AdminITSecurityAlertViewModel
{
    public string Icon { get; init; } = "ti-alert-triangle";
    public string Color { get; init; } = "var(--amber)";
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsCritical { get; init; }
}

public sealed class AdminITRecentSessionViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string TimeLabel { get; init; } = string.Empty;
}
