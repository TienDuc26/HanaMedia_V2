using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class Booking
{
    public int Id { get; set; }

    public int FinanceVersion { get; set; }
    public decimal CompanyPercent { get; set; }
    public decimal CommissionPercent { get; set; }
    public decimal CastPercent { get; set; }
    public string? AcceptanceFileUrl { get; set; }
    public int ContractRevision { get; set; }
    public int? LegalApprovedRevision { get; set; }
    public int? LegalReviewedByUserId { get; set; }
    public DateTime? LegalReviewedAt { get; set; }
    public string? LegalFeedback { get; set; }
    public virtual ICollection<BookingKol> BookingKols { get; set; } = new List<BookingKol>();
    public virtual ICollection<BookingPayment> Payments { get; set; } = new List<BookingPayment>();
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal CommissionPool => decimal.Round(BookingPrice * CommissionPercent / 100m, 2, MidpointRounding.AwayFromZero);
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal CastPool => decimal.Round(BookingPrice * CastPercent / 100m, 2, MidpointRounding.AwayFromZero);
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string KolNames => BookingKols.Count > 0 ? string.Join(", ", BookingKols.Select(k => k.Kol?.Name ?? $"KOL #{k.KolId}")) : Kol?.Name ?? "—";

    public string ClientName { get; set; } = null!;

    public string CampaignName { get; set; } = null!;

    public int? KolId { get; set; }

    public string? JobDescription { get; set; }

    public DateOnly Deadline { get; set; }

    public DateOnly? PostingDate { get; set; }

    public decimal BookingPrice { get; set; }

    public decimal ActualCost { get; set; }

    public int? PrimaryManagerId { get; set; }

    public string? Status { get; set; }

    public string? ContractStatus { get; set; }

    public DateTime? ContractApprovedAt { get; set; }

    public int? ContractApprovedById { get; set; }

    public DateTime? ContractSignedAt { get; set; }

    public int? ContractSignedById { get; set; }

    public string? RejectionReason { get; set; }

    public string? ContractFileUrl { get; set; }

    public string? QuotationFileUrl { get; set; }

    public string? PostLink { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? CampaignId { get; set; }

    public virtual ICollection<BookingWageAuditLog> BookingWageAuditLogs { get; set; } = new List<BookingWageAuditLog>();

    public virtual ICollection<BookingWage> BookingWages { get; set; } = new List<BookingWage>();

    public virtual Kol? Kol { get; set; }

    public virtual Employee? PrimaryManager { get; set; }

    public virtual Employee? ContractApprovedBy { get; set; }

    public virtual Employee? ContractSignedBy { get; set; }

    public virtual Campaign? Campaign { get; set; }
}
