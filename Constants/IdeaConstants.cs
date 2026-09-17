namespace HanaMedia.Constants;

public static class IdeaStatuses
{
    public const string Idea = "y_tuong";
    public const string Review = "review";
    public const string NeedRevision = "need_revision";
    public const string Approved = "approved";
    public const string InProgress = "in_production";
    public const string Done = "done";

    public static readonly IReadOnlyList<string> All =
        [Idea, Review, NeedRevision, Approved, InProgress, Done];

    public static bool IsValid(string? status) => status is not null && All.Contains(status);

    public static string GetLabel(string status) => status switch
    {
        Idea => "Ý tưởng",
        Review => "Chờ review",
        NeedRevision => "Cần chỉnh sửa",
        Approved => "Đã duyệt",
        InProgress => "Đang triển khai",
        Done => "Hoàn thành",
        _ => status
    };
}

public static class IdeaCommentTypes
{
    public const string General = "general";
    public const string Review = "review";
    public const string RevisionRequest = "revision_request";
    public const string DirectorFeedback = "director_feedback";
    public const string DirectorDecision = "director_decision";
}

public static class DirectorIdeaReviewStatuses
{
    public const string Pending = "pending";
    public const string RevisionRequested = "revision_requested";
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public static readonly IReadOnlyList<string> All =
        [Pending, RevisionRequested, Approved, Rejected];

    public static bool IsValid(string? status) => status is not null && All.Contains(status);

    public static string GetLabel(string status) => status switch
    {
        Pending => "Chờ Giám đốc duyệt",
        RevisionRequested => "Giám đốc yêu cầu sửa",
        Approved => "Giám đốc đã duyệt",
        Rejected => "Giám đốc từ chối",
        _ => status
    };
}

public static class IdeaLibraryCategories
{
    public const string Trend = "trend";
    public const string Viral = "viral";
    public const string Deployed = "da_trien_khai";
    public const string Unused = "chua_su_dung";

    public static readonly IReadOnlyList<string> All =
        [Trend, Viral, Deployed, Unused];

    public static bool IsValid(string? category) =>
        category is not null && All.Contains(category);

    public static string GetLabel(string category) => category switch
    {
        Trend => "Trend",
        Viral => "Viral",
        Deployed => "Đã triển khai",
        Unused => "Chưa sử dụng",
        _ => category
    };
}
