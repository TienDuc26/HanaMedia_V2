namespace HanaMedia.ViewModels;

public sealed class AuditLogFilterInputModel
{
    public string? User { get; set; }

    public string? ActionType { get; set; }

    public string? Module { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public int Page { get; set; } = 1;
}

public sealed class AuditLogPageViewModel
{
    public AuditLogFilterInputModel Filter { get; set; } = new();

    public string? ValidationMessage { get; set; }

    public IReadOnlyList<AuditLogOptionViewModel> ActionOptions { get; init; } = [];

    public IReadOnlyList<AuditLogOptionViewModel> ModuleOptions { get; init; } = [];

    public IReadOnlyList<AuditLogItemViewModel> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int FailureCount { get; init; }

    public IReadOnlyList<AuditAlertViewModel> Alerts { get; init; } = [];

    public int CurrentPage { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int TotalPages { get; init; } = 1;
}

public sealed record AuditAlertViewModel(
    string Title,
    string Description,
    string Icon,
    string Severity);

public sealed record AuditLogOptionViewModel(string Code, string Label);

public sealed class AuditLogItemViewModel
{
    public DateTime? OccurredAt { get; init; }

    public string ActorDisplayName { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string RoleName { get; init; } = string.Empty;

    public string ActionLabel { get; init; } = string.Empty;

    public string ActionType { get; init; } = string.Empty;

    public string BadgeClass { get; init; } = string.Empty;

    public string ModuleLabel { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string IpAddress { get; init; } = string.Empty;
}

public sealed record AuditLogExportResult(
    IReadOnlyList<AuditLogItemViewModel> Rows,
    bool ExceedsLimit);

