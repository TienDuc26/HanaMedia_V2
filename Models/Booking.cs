using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class Booking
{
    public int Id { get; set; }

    public string ClientName { get; set; } = null!;

    public string CampaignName { get; set; } = null!;

    public int? CampaignId { get; set; }

    public int? KolId { get; set; }

    public string? JobDescription { get; set; }

    public DateOnly Deadline { get; set; }

    public DateOnly? PostingDate { get; set; }

    public decimal BookingPrice { get; set; }

    public decimal ActualCost { get; set; }

    public int? PrimaryManagerId { get; set; }

    public string? Status { get; set; }

    public string? ContractFileUrl { get; set; }

    public string? QuotationFileUrl { get; set; }

    public string? PostLink { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<BookingWageAuditLog> BookingWageAuditLogs { get; set; } = new List<BookingWageAuditLog>();

    public virtual ICollection<BookingWage> BookingWages { get; set; } = new List<BookingWage>();

    public virtual Kol? Kol { get; set; }

    public virtual Employee? PrimaryManager { get; set; }

    public virtual Campaign? Campaign { get; set; }
}
