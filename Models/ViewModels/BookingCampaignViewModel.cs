namespace HanaMedia.Models.ViewModels;

public class BookingCampaignViewModel
{
    public List<Booking> Bookings { get; set; } = new();
    public List<KolSalarySummary> KolSummary { get; set; } = new();
    public List<ManagerSalarySummary> ManagerSummary { get; set; } = new();
}

public class KolSalarySummary
{
    public int KolId { get; set; }
    public string KolName { get; set; } = "";
    public string KolPlatform { get; set; } = "";
    public int BookingCount { get; set; }
    public decimal TotalCastAmount { get; set; }
    public List<KolBookingDetail> BookingDetails { get; set; } = new();
}

public class KolBookingDetail
{
    public int BookingId { get; set; }
    public string ClientName { get; set; } = "";
    public string CampaignName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string ManagerName { get; set; } = "";
    public decimal CastAmount { get; set; }
}

public class ManagerSalarySummary
{
    public int ManagerId { get; set; }
    public string ManagerName { get; set; } = "";
    public int BookingCount { get; set; }
    public decimal TotalCastPool { get; set; }
    public decimal TotalAllocated { get; set; }
    public List<ManagerBookingDetail> BookingDetails { get; set; } = new();
}

public class ManagerBookingDetail
{
    public int BookingId { get; set; }
    public string ClientName { get; set; } = "";
    public string CampaignName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int KolCount { get; set; }
    public decimal CastPool { get; set; }
    public decimal Allocated { get; set; }
}
