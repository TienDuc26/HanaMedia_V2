using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class Department
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string Status { get; set; } = "active";

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
