using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class User
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string Status { get; set; } = "active";

    public string SecurityStamp { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int FailedLoginAttempts { get; set; } = 0;

    public DateTime? LockedUntil { get; set; }

    public virtual ICollection<BookingWageAuditLog> BookingWageAuditLogs { get; set; } = new List<BookingWageAuditLog>();

    public virtual Employee? Employee { get; set; }

    public virtual ICollection<SystemAuditLog> SystemAuditLogs { get; set; } = new List<SystemAuditLog>();

    public virtual ICollection<WorkTask> CreatedWorkTasks { get; set; } = new List<WorkTask>();

    public virtual ICollection<WorkTask> ReviewedWorkTasks { get; set; } = new List<WorkTask>();

    public virtual ICollection<WorkTaskHistory> WorkTaskHistoryEntries { get; set; } = new List<WorkTaskHistory>();
}
