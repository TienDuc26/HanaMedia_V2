using System.ComponentModel.DataAnnotations;

namespace HanaMedia.ViewModels;

public sealed class KolPageViewModel
{
    public string? Search { get; init; }
    public string? Platform { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public bool CanCreate { get; init; }
    public bool CanManageAll { get; init; }
    public IReadOnlyList<KolListItemViewModel> Kols { get; init; } = [];
    public IReadOnlyList<KolResponsibleStaffOptionViewModel> ResponsibleStaff { get; init; } = [];
}

public sealed class KolListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string Platform { get; init; } = null!;
    public string ProfileLink { get; init; } = null!;
    public int FollowersCount { get; init; }
    public decimal EngagementRate { get; init; }
    public string Niche { get; init; } = null!;
    public decimal BookingPrice { get; init; }
    public string Location { get; init; } = null!;
    public string ContactInfo { get; init; } = null!;
    public int? ResponsibleStaffId { get; init; }
    public string ResponsibleStaffName { get; init; } = "Chưa phân công";
    public byte? RatingScore { get; init; }
    public string Status { get; init; } = null!;
    public string StatusLabel { get; init; } = null!;
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
    public string RowVersion { get; init; } = null!;
    public IReadOnlyList<KolBookingHistoryViewModel> BookingHistory { get; init; } = [];
}

public sealed record KolResponsibleStaffOptionViewModel(int Id, string Name);

public sealed record KolBookingHistoryViewModel(
    int BookingId, string ClientName, string CampaignName, decimal BookingPrice,
    string Status, string StatusLabel, DateOnly Deadline);

public class KolInputModel
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Platform { get; set; } = string.Empty;

    [Required, StringLength(255), Url]
    public string ProfileLink { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int FollowersCount { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal EngagementRate { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    public string Niche { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "9999999999999")]
    public decimal BookingPrice { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    public string Location { get; set; } = string.Empty;

    [Required, StringLength(255, MinimumLength = 3)]
    public string ContactInfo { get; set; } = string.Empty;

    public int? ResponsibleStaffId { get; set; }

    [Range(1, 5)]
    public byte? RatingScore { get; set; }

    [Required, StringLength(30)]
    public string Status { get; set; } = "tiem_nang";
}

public sealed class CreateKolInputModel : KolInputModel;

public sealed class UpdateKolInputModel : KolInputModel
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class DeleteKolInputModel
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed record KolOperationResult(bool Succeeded, string Message)
{
    public static KolOperationResult Success(string message) => new(true, message);
    public static KolOperationResult Failure(string message) => new(false, message);
}
