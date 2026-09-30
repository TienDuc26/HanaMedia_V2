namespace HanaMedia.Constants;

public static class CampaignStatuses
{
    // Keep the persisted planning code for compatibility with existing databases.
    // Only director confirmation starts a campaign. Booking contracts stay separate.
    public const string AwaitingSignature = "planning";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Accepted = "accepted";

    public static bool IsSelectable(string? value) => value is AwaitingSignature or Running or Completed or Accepted;

    public static string Label(string? value) => value switch
    {
        AwaitingSignature => "Đang chờ ký",
        Running => "Đang chạy",
        Completed => "Hoàn thành",
        Accepted => "Đã nghiệm thu",
        // Historical states remain readable; they cannot be selected for new saves.
        "paused" => "Tạm dừng (dữ liệu cũ)",
        "cancelled" => "Đã hủy",
        _ => "Trạng thái cũ"
    };
}
