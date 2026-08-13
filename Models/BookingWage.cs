using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class BookingWage
{
    public int BookingId { get; set; }

    public int EmployeeId { get; set; }

    public decimal AllocatedWage { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual Employee Employee { get; set; } = null!;
}
