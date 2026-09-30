using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class Campaign
{
    public int Id { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByUserId { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public int? AcceptedByUserId { get; set; }
    public string Name { get; set; } = null!;
    public string Client { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Budget { get; set; }
    public int ManagerEmployeeId { get; set; }
    public string Status { get; set; } = "planning";
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Employee? ManagerEmployee { get; set; }
    public virtual ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Idea> Ideas { get; set; } = new List<Idea>();
}
