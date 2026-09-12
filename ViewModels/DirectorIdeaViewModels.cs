using System.ComponentModel.DataAnnotations;

namespace HanaMedia.ViewModels;

public sealed class DirectorIdeaPageViewModel
{
    public IReadOnlyList<DirectorIdeaItemViewModel> Items { get; init; } = [];
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? DirectorStatus { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalItems { get; init; }
}

public sealed class DirectorIdeaItemViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string CreatorName { get; init; } = "-";
    public string ClientName { get; init; } = string.Empty;
    public string CampaignName { get; init; } = string.Empty;
    public string PrimaryStaffName { get; init; } = "-";
    public string ReviewerName { get; init; } = "-";
    public string? Insight { get; init; }
    public string? Concept { get; init; }
    public string? ContentDetails { get; init; }
    public string? ReferenceLink { get; init; }
    public string? ReferenceFileUrl { get; init; }
    public string? MoodboardDescription { get; init; }
    public IReadOnlyList<IdeaMoodboardImageViewModel> MoodboardImages { get; init; } = [];
    public string? ScriptText { get; init; }
    public DateOnly Deadline { get; init; }
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string? ManagerFeedback { get; init; }
    public string DirectorReviewStatus { get; init; } = string.Empty;
    public string DirectorReviewStatusLabel { get; init; } = string.Empty;
    public string? DirectorFeedback { get; init; }
    public DateTime? DirectorReviewedAt { get; init; }
    public IReadOnlyList<IdeaCommentViewModel> Comments { get; init; } = [];
}

public sealed class DirectorEditIdeaInputModel
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [StringLength(2000)]
    public string? Insight { get; set; }

    [StringLength(4000)]
    public string? Concept { get; set; }

    [StringLength(8000)]
    public string? ContentDetails { get; set; }

    [StringLength(12000)]
    public string? ScriptText { get; set; }
}
