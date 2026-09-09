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
}
