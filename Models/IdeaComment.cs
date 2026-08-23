using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

/// <summary>
/// Lịch sử comment/feedback trên một ý tưởng.
/// Ghi lại ai comment, nội dung, thời điểm — phục vụ audit và hiển thị trong UI ý tưởng.
/// Module 13: dùng ở bước Review / Ch�nh sửa của quy trình ý tưởng.
/// </summary>
public partial class IdeaComment
{
    public long Id { get; set; }

    public int IdeaId { get; set; }

    /// <summary>
    /// User đã ghi comment. Nullable để hỗ trợ audit người dùng đã xoá.
    /// </summary>
    public int? AuthorUserId { get; set; }

    /// <summary>
    /// "review" = QL Ý tưởng review, "revision_request" = yêu cầu chỉnh sửa, "general" = bình thường.
    /// Mặc định: "general".
    /// </summary>
    public string CommentType { get; set; } = "general";

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Idea Idea { get; set; } = null!;

    public virtual User? AuthorUser { get; set; }
}
