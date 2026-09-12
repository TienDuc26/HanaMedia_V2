using System.ComponentModel.DataAnnotations;

namespace HanaMedia.ViewModels;

public sealed class IdeaLibraryQueryModel
{
    [StringLength(150)]
    public string? Search { get; set; }

    [StringLength(100)]
    public string? Industry { get; set; }

    [StringLength(100)]
    public string? Client { get; set; }

    public int? CampaignId { get; set; }

    [StringLength(30)]
    public string? Category { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
}

public sealed class IdeaLibraryPageViewModel
{
    public IReadOnlyList<IdeaLibraryItemViewModel> Items { get; init; } = [];
    public IReadOnlyList<string> Industries { get; init; } = [];
    public IReadOnlyList<string> Clients { get; init; } = [];
    public IReadOnlyList<IdeaCampaignOptionViewModel> Campaigns { get; init; } = [];
    public IReadOnlyList<IdeaLibraryCategoryViewModel> Categories { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}

public sealed class IdeaLibraryItemViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Industry { get; init; } = string.Empty;
    public string Client { get; init; } = string.Empty;
    public int? CampaignId { get; init; }
    public string? CampaignName { get; init; }
    public string Category { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string? Insight { get; init; }
    public string? Concept { get; init; }
    public string? ContentDetails { get; init; }
    public string? ReferenceLink { get; init; }
    public string? ReferenceFileUrl { get; init; }
    public string? MoodboardDescription { get; init; }
    public IReadOnlyList<IdeaMoodboardImageViewModel> MoodboardImages { get; init; } = [];
    public string? Script { get; init; }
    public string CreatorName { get; init; } = "-";
    public string PrimaryStaffName { get; init; } = "-";
    public DateOnly Deadline { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record IdeaLibraryCategoryViewModel(string Code, string Label);

public sealed class UpdateIdeaClassificationInputModel
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Industry { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Category { get; set; } = string.Empty;
}
