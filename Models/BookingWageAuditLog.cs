using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class BookingWageAuditLog
{
    public int Id { get; set; }

    public int? BookingId { get; set; }

    public int? PerformedByUserId { get; set; }

    public string LogDetail { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual Booking? Booking { get; set; }

    public virtual User? PerformedByUser { get; set; }
}
