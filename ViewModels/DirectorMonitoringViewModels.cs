namespace HanaMedia.ViewModels;

public sealed class DirectorMonitoringViewModel
{
    public int TodayLoginCount { get; init; }

    public int RecentActiveUserCount { get; init; }

    public int TodayBlockedNetworkCount { get; init; }

    public int TodaySecurityAlertCount { get; init; }

    public IReadOnlyList<DirectorMonitoringAlertViewModel> Alerts { get; init; } = [];

    public IReadOnlyList<DirectorModuleActivityViewModel> Modules { get; init; } = [];
}

public sealed record DirectorMonitoringAlertViewModel(
    string Title,
    string Summary,
    string Icon);

public sealed record DirectorModuleActivityViewModel(
    string ModuleLabel,
    int ActionCount,
    string CommonAction,
    DateTime? LastUpdatedAt);
