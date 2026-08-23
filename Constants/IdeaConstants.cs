namespace HanaMedia.Constants;

/// <summary>
/// Trạng thái vòng đời của một ý tưởng — đúng quy trình 6 bước của Module 13:
/// Ý tưởng → Review → Chỉnh sửa → Duyệt → Triển khai → Hoàn thành.
/// Đã khớp với ràng buộc CHECK constraint trong migration Module 2 (chk_idea_status).
/// </summary>
public static class IdeaStatuses
{
    public const string Draft = "y_tuong";               // Khởi tạo: NV/QL vừa tạo
    public const string Review = "review";                // NV đã gửi duyệt
    public const string NeedRevision = "need_revision";   // QL yêu cầu ch�nh sửa
    public const string Approved = "approved";            // QL đã duyệt
    public const string InProduction = "in_production";   // QL chuyển sang triển khai (tên mới; xem ghi chú)
    public const string Done = "done";                    // QL đánh dấu hoàn thành

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly([Draft, Review, NeedRevision, Approved, InProduction, Done]);

    public static bool IsValid(string? status) => status is not null && All.Contains(status);

    public static string GetLabel(string status) => status switch
    {
        Draft => "Ý tưởng",
        Review => "Đang review",
        NeedRevision => "Cần chỉnh sửa",
        Approved => "Đã duyệt",
        InProduction => "�ang triển khai",
        Done => "Hoàn thành",
        _ => status
    };

    /// <summary>
    /// CSS class cho status-chip trên UI. Mapping theo tông màu chuẩn đang dùng ở các module khác.
    /// </summary>
    public static string GetCssClass(string status) => status switch
    {
        Draft => "status-todo",
        Review => "status-review",
        NeedRevision => "status-need_revision",
        Approved => "status-approved",
        InProduction => "status-in_progress",
        Done => "status-done",
        _ => "status-todo"
    };
}

/// <summary>
/// Phân loại thư viện ý tưởng — phục vụ Module 14 (Idea Library) sau này,
/// đã có sẵn ràng buộc CHECK constraint chk_idea_cat trong DB.
/// Module 13 chỉ sử dụng giá trị mặc định, NV/QL có thể ghi đè khi cần.
/// </summary>
public static class IdeaCategories
{
    public const string Trend = "trend";
    public const string Viral = "viral";
    public const string DaTrienKhai = "da_trien_khai";
    public const string ChuaSuDung = "chua_su_dung";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly([Trend, Viral, DaTrienKhai, ChuaSuDung]);

    public static bool IsValid(string? category) => category is not null && All.Contains(category);

    public static string GetLabel(string category) => category switch
    {
        Trend => "Theo trend",
        Viral => "Viral",
        DaTrienKhai => "Đã triển khai",
        ChuaSuDung => "Chưa sử dụng",
        _ => category
    };
}

/// <summary>
/// Loại comment trên một ý tưởng — dùng để phân biệt ghi chú nội bộ với feedback từ QL/Giám đốc.
/// </summary>
public static class IdeaCommentTypes
{
    public const string General = "general";               // bình luận thường
    public const string Review = "review";                 // QL review
    public const string RevisionRequest = "revision_request"; // QL yêu cầu chỉnh sửa

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly([General, Review, RevisionRequest]);

    public static bool IsValid(string? type) => type is not null && All.Contains(type);
}
