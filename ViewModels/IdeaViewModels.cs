using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HanaMedia.ViewModels;

public sealed class IdeaPageViewModel
{
    public IReadOnlyList<IdeaListItemViewModel> Items { get; init; } = [];
    public IReadOnlyList<IdeaCampaignOptionViewModel> Campaigns { get; init; } = [];
    public IReadOnlyList<IdeaEmployeeOptionViewModel> Staff { get; init; } = [];
    public IReadOnlyList<IdeaEmployeeOptionViewModel> Reviewers { get; init; } = [];
    public string? Search { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalItems { get; init; }
    public bool IsManager { get; init; }
}

public sealed class IdeaListItemViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = null!;
    public int? CampaignId { get; init; }
    public string ClientName { get; init; } = null!;
    public string CampaignName { get; init; } = null!;
    public string? Insight { get; init; }
    public string? Concept { get; init; }
    public string? ContentDetails { get; init; }
    public string? ReferenceLink { get; init; }
    public string? ReferenceFilePath { get; init; }
    public string? MoodboardDesc { get; init; }
    public string? MoodboardFilePath { get; init; }
    public IReadOnlyList<IdeaMoodboardImageViewModel> MoodboardImages { get; init; } = [];
    public string? ScriptText { get; init; }
    public DateOnly Deadline { get; init; }
    public int? PrimaryStaffId { get; init; }
    public int? ReviewerEmployeeId { get; init; }
    public string CreatorName { get; init; } = "-";
    public string PrimaryStaffName { get; init; } = "-";
    public string ReviewerName { get; init; } = "-";
    public string Status { get; init; } = null!;
    public string StatusLabel { get; init; } = null!;
    public string? FeedbackComment { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool CanEdit { get; init; }
    public bool CanSubmit { get; init; }
    public bool CanReview { get; init; }
    public bool CanAdvance { get; init; }
    public bool CanDelete { get; init; }
    public IReadOnlyList<IdeaCommentViewModel> Comments { get; init; } = [];
}

public sealed record IdeaCampaignOptionViewModel(int Id, string Name, string Client);
public sealed record IdeaEmployeeOptionViewModel(int Id, string Name, int OpenTaskCount);
public sealed record IdeaCommentViewModel(string AuthorName, string AuthorRole, string Content, DateTime CreatedAt);
public sealed record IdeaMoodboardImageViewModel(long Id, string FileUrl, int SortOrder);

public sealed class SaveIdeaInputModel
{
    public int Id { get; set; }

    [Required, StringLength(150, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CampaignId { get; set; }

    [StringLength(2000)]
    public string? Insight { get; set; }

    [StringLength(4000)]
    public string? Concept { get; set; }

    [StringLength(8000)]
    public string? ContentDetails { get; set; }

    [Url, StringLength(255)]
    public string? ReferenceLink { get; set; }

    [StringLength(2000)]
    public string? MoodboardDesc { get; set; }

    [StringLength(12000)]
    public string? ScriptText { get; set; }

    [Required]
    public DateOnly Deadline { get; set; }

    public int? PrimaryStaffId { get; set; }
    public int? ReviewerEmployeeId { get; set; }
    public IFormFile? ReferenceFile { get; set; }
    public IFormFile? MoodboardFile { get; set; }
    public List<IFormFile> MoodboardFiles { get; set; } = [];
}

public sealed record IdeaOperationResult(bool Succeeded, string Message)
{
    public static IdeaOperationResult Success(string message) => new(true, message);
    public static IdeaOperationResult Failure(string message) => new(false, message);
}
