using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class Kol
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Platform { get; set; } = null!;

    public string ProfileLink { get; set; } = null!;

    public int FollowersCount { get; set; }

    public decimal EngagementRate { get; set; }

    public string Niche { get; set; } = null!;

    public decimal BookingPrice { get; set; }

    public string Location { get; set; } = null!;

    public string ContactInfo { get; set; } = null!;

    public int? ResponsibleStaffId { get; set; }

    public byte? RatingScore { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual Employee? ResponsibleStaff { get; set; }
}
