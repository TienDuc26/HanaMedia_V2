using System.ComponentModel.DataAnnotations;

namespace HanaMedia.ViewModels;

public sealed class CampaignPageViewModel
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public bool CanManage { get; init; }
    public IReadOnlyList<CampaignListItemViewModel> Campaigns { get; init; } = [];
}

public sealed class CampaignListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string ClientName { get; init; } = null!;
    public string? Description { get; init; }
    public decimal Budget { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Status { get; init; } = null!;
    public string StatusLabel { get; init; } = null!;
    public string CreatedByName { get; init; } = null!;
    public int BookingCount { get; init; }
    public int IdeaCount { get; init; }
    public string RowVersion { get; init; } = null!;
}

public class CampaignInputModel
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 2)]
    public string ClientName { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal Budget { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "draft";
}

public sealed class CreateCampaignInputModel : CampaignInputModel;

public sealed class UpdateCampaignInputModel : CampaignInputModel
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class DeleteCampaignInputModel
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed record CampaignOperationResult(bool Succeeded, string Message)
{
    public static CampaignOperationResult Success(string message) => new(true, message);
    public static CampaignOperationResult Failure(string message) => new(false, message);
}
