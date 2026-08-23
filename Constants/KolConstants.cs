namespace HanaMedia.Constants;

public static class KolPlatforms
{
    public const string TikTok = "TikTok";
    public const string Instagram = "Instagram";
    public const string YouTube = "YouTube";
    public const string Facebook = "Facebook";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly([TikTok, Instagram, YouTube, Facebook]);

    public static bool IsValid(string? platform) => platform is not null && All.Contains(platform);
}

public static class KolStatuses
{
    public const string Potential = "tiem_nang";
    public const string Contacted = "da_lien_he";
    public const string Negotiating = "dang_deal";
    public const string Closed = "da_chot";
    public const string Running = "dang_chay";
    public const string Completed = "hoan_thanh";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly([Potential, Contacted, Negotiating, Closed, Running, Completed]);

    public static bool IsValid(string? status) => status is not null && All.Contains(status);

    public static string GetLabel(string status) => status switch
    {
        Potential => "Tiềm năng",
        Contacted => "Đã liên hệ",
        Negotiating => "Đang deal",
        Closed => "Đã chốt",
        Running => "Đang chạy",
        Completed => "Hoàn thành",
        _ => status
    };
}

public static class KolPermissions
{
    public static bool CanView(string? role) =>
        role is AppRoles.Director or AppRoles.BookingManager or AppRoles.BookingStaff;

    public static bool CanCreate(string? role) =>
        role is AppRoles.BookingManager or AppRoles.BookingStaff;

    public static bool CanManageAll(string? role) => role == AppRoles.BookingManager;
}
