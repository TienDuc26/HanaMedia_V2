using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class SystemConfig
{
    public string ConfigKey { get; set; } = null!;

    public string ConfigValue { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
