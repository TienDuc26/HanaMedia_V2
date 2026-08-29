using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class Campaign
{
    public int Id { get; set; }
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
}
