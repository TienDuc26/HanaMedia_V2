namespace HanaMedia.Constants;

/// <summary>
/// Hạng mục công việc trong một ý tưởng (Module 13). Lưu ở cột work_tasks.work_category.
/// Null/"" = "Toàn bộ ý tưởng" (mặc định — không giới hạn 1 hạng mục).
/// </summary>
public static class IdeaTaskCategories
{
    public const string Concept = "concept";
    public const string Script = "script";
    public const string Moodboard = "moodboard";
    public const string Reference = "reference";
    public const string Content = "content";
    public const string Full = "full";
    public const string Other = "other";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly([Concept, Script, Moodboard, Reference, Content, Full, Other]);

    public static string GetLabel(string? code) => code switch
    {
        Concept => "Concept",
        Script => "Script",
        Moodboard => "Moodboard",
        Reference => "Reference",
        Content => "Nội dung tổng thể",
        Full => "Toàn bộ ý tưởng",
        Other => "Khác",
        null or "" => "Toàn bộ ý tưởng",
        _ => code
    };

    /// <summary>
    /// Tạo gợi ý tiêu đề từ tên ý tưởng + hạng mục. QL vẫn sửa lại được trước khi lưu.
    /// </summary>
    public static string SuggestTitle(string? ideaTitle, string? categoryCode)
    {
        var label = GetLabel(categoryCode);
        if (string.IsNullOrWhiteSpace(ideaTitle)) return label;
        return $"{label} — {ideaTitle.Trim()}";
    }
}