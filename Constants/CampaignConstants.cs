namespace HanaMedia.Constants;

public static class CampaignStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly([Draft, Active, Paused, Completed, Cancelled]);

    public static bool IsValid(string? status) => status is not null && All.Contains(status);

    public static string GetLabel(string status) => status switch
    {
        Draft => "Bản nháp",
        Active => "Đang chạy",
        Paused => "Tạm dừng",
        Completed => "Hoàn thành",
        Cancelled => "Đã hủy",
        _ => status
    };
}

public static class CampaignPermissions
{
    public static IReadOnlyList<string> ViewRoles { get; } = Array.AsReadOnly(
    [
        AppRoles.Director,
        AppRoles.BookingManager,
        AppRoles.BookingStaff,
        AppRoles.IdeaManager,
        AppRoles.IdeaStaff
    ]);

    public static bool CanView(string? role) => role is not null && ViewRoles.Contains(role);

    public static bool CanManage(string? role) => role == AppRoles.BookingManager;
}
