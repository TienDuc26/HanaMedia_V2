using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

/// <summary>
/// Một ảnh thuộc bộ moodboard của một ý tưởng (1 ý tưởng có thể có nhiều ảnh).
/// Upload bằng IdeaAttachmentService.SaveManyAsync, hiển thị dạng gallery trong modal chi tiết.
/// </summary>
public partial class IdeaMoodboardImage
{
    public long Id { get; set; }

    public int IdeaId { get; set; }

    /// <summary>
    /// Đường dẫn tương đối trong wwwroot (ví dụ: /uploads/ideas/idea_5_mood_20260823_xxx.png).
    /// </summary>
    public string FileUrl { get; set; } = null!;

    /// <summary>
    /// Thứ tự hiển thị trong gallery (0 = đầu tiên).
    /// </summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Idea Idea { get; set; } = null!;
}