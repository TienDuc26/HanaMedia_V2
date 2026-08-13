using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class SystemAuditLog
{
    public long Id { get; set; }

    public int? UserId { get; set; }

    public string ActionType { get; set; } = null!;

    public string Module { get; set; } = null!;

    public string LogDetail { get; set; } = null!;

    public string IpAddress { get; set; } = null!;

    public string? DeviceInfo { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
