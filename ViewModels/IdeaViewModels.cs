using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HanaMedia.Constants;

namespace HanaMedia.ViewModels;

/// <summary>
/// Dữ liệu 1 ý tưởng hiển thị trong danh sách (QL Ý tưởng).
/// </summary>
public sealed class IdeaListItemViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = null!;
    public string CreatorName { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
    public string CampaignName { get; init; } = string.Empty;
    public string PrimaryStaffName { get; init; } = string.Empty;
    public string ReviewerName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusCssClass { get; init; } = string.Empty;
    public DateOnly Deadline { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool HasReferenceFile { get; init; }
    public bool HasMoodboardFile { get; init; }
    public int OpenTasks { get; init; }
}

/// <summary>
/// Dữ liệu chi tiết đầy đủ của 1 ý tưởng — dùng cho modal xem/chuyển trạng thái.
/// </summary>
public sealed class IdeaDetailViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = null!;
    public int? CreatorEmployeeId { get; init; }
    public string CreatorName { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
    public string CampaignName { get; init; } = string.Empty;
    public string Industry { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
    public string? Insight { get; init; }
    public string? Concept { get; init; }
    public string? ContentDetails { get; init; }
    public string? ReferenceLink { get; init; }
    public string? ReferenceFileUrl { get; init; }
    public string? MoodboardDesc { get; init; }
    public string? MoodboardFileUrl { get; init; }

    /// <summary>
    /// Danh sách ảnh moodboard (1 ý tưởng - nhiều ảnh) — thay thế dần cho MoodboardFileUrl.
    /// </summary>
    public IReadOnlyList<IdeaMoodboardImageViewModel> MoodboardImages { get; init; } = [];

    public string? ScriptText { get; init; }
    public DateOnly Deadline { get; init; }
    public int? PrimaryStaffId { get; init; }
    public string PrimaryStaffName { get; init; } = string.Empty;
    public int? ReviewerEmployeeId { get; init; }
    public string ReviewerName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusCssClass { get; init; } = string.Empty;
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public IReadOnlyList<IdeaCommentViewModel> Comments { get; init; } = [];
    public IReadOnlyList<IdeaTransitionOptionViewModel> AllowedTransitions { get; init; } = [];
    /// <summary>
    /// Các task (Module 6) gắn với ý tưởng này — hiển thị trong khu vực "Công việc liên quan"
    /// trong modal chi tiết. Sắp xếp theo deadline tăng dần; task Done chìm xuống cuối.
    /// </summary>
    public IReadOnlyList<IdeaRelatedTaskViewModel> RelatedTasks { get; init; } = [];
}

public sealed record IdeaRelatedTaskViewModel(
    int Id,
    string Title,
    string? WorkCategory,
    string? WorkCategoryLabel,
    string Status,
    string StatusLabel,
    string AssignedEmployeeName,
    DateTime Deadline);

public sealed class IdeaMoodboardImageViewModel
{
    public long Id { get; init; }
    public string FileUrl { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

/// <summary>
/// 1 comment/feedback trong lịch sử ý tưởng.
/// </summary>
public sealed class IdeaCommentViewModel
{
    public long Id { get; init; }
    public int IdeaId { get; init; }
    public int? AuthorUserId { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public string AuthorRole { get; init; } = string.Empty;
    public string CommentType { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// 1 lựa chọn chuyển trạng thái mà user hiện tại được phép thực hiện trên ý tưởng.
/// </summary>
public sealed record IdeaTransitionOptionViewModel(
    string Status,
    string Label,
    bool RequiresComment,
    bool RequiresContent,
    string CssModifier);

/// <summary>
/// ViewModel trang danh sách ý tưởng (QL Ý tưởng / NV Ý tưởng / Director).
/// </summary>
public sealed class IdeaPageViewModel
{
    public IReadOnlyList<IdeaListItemViewModel> Items { get; init; } = [];
    public IReadOnlyList<IdeaEmployeeOptionViewModel> Employees { get; init; } = [];
    /// <summary>
    /// Danh sách QL Ý tưởng (employee thuộc phòng Y_tuong có IsManager=true). Dùng cho dropdown "Người review".
    /// </summary>
    public IReadOnlyList<IdeaEmployeeOptionViewModel> Reviewers { get; init; } = [];
    public string? Search { get; init; }
    public string? StatusFilter { get; init; }
    public string? ClientFilter { get; init; }
    public int Page { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int TotalCount { get; init; }
    public bool CanCreate { get; init; }
    public bool CanEdit { get; init; }
    public bool CanTransition { get; init; }
    public bool CanComment { get; init; }
    public bool CanAssignTask { get; init; }
    public string CurrentUserRole { get; init; } = string.Empty;
    public int? CurrentEmployeeId { get; init; }
    public IReadOnlyList<string> AvailableStatuses { get; init; } = [];
    public IReadOnlyList<string> DistinctClients { get; init; } = [];
}

public sealed record IdeaEmployeeOptionViewModel(int Id, string Name, string Department);

/// <summary>
/// Input tạo / sửa ý tưởng.
/// </summary>
public sealed class IdeaUpsertInputModel
{
    public int? Id { get; set; }

    [Required, StringLength(150, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Hiện tại KHÔNG có FK tới bảng campaigns (Module 7 chưa có). Để trống.
    /// </summary>
    [StringLength(100)]
    public string? CampaignName { get; set; }

    [StringLength(100)]
    public string? Industry { get; set; }

    [Required]
    public string Category { get; set; } = IdeaCategories.ChuaSuDung;

    [StringLength(2000)]
    public string? Insight { get; set; }

    [StringLength(2000)]
    public string? Concept { get; set; }

    [StringLength(4000)]
    public string? ContentDetails { get; set; }

    [StringLength(255)]
    [Url]
    public string? ReferenceLink { get; set; }

    [StringLength(1000)]
    public string? MoodboardDesc { get; set; }

    [StringLength(10000)]
    public string? ScriptText { get; set; }

    [Required]
    public DateOnly Deadline { get; set; }

    public int? PrimaryStaffId { get; set; }

    public int? ReviewerEmployeeId { get; set; }
}

/// <summary>
/// Input thực hiện chuyển trạng thái ý tưởng.
/// </summary>
public sealed class IdeaTransitionInputModel
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required]
    public string TargetStatus { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Comment { get; set; }
}

/// <summary>
/// Input thêm comment mới vào ý tưởng.
/// </summary>
public sealed class IdeaCommentInputModel
{
    [Range(1, int.MaxValue)]
    public int IdeaId { get; set; }

    [Required, StringLength(2000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// "general" / "review" / "revision_request".
    /// </summary>
    public string CommentType { get; set; } = IdeaCommentTypes.General;
}

/// <summary>
/// Kết quả thao tác nghiệp vụ ý tưởng — trả về cho controller.
/// </summary>
public sealed record IdeaOperationResult(bool Succeeded, string Message, int? IdeaId = null)
{
    public static IdeaOperationResult Success(string message, int? ideaId = null) => new(true, message, ideaId);
    public static IdeaOperationResult Failure(string message) => new(false, message, null);
}
