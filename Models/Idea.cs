using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class Idea
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public int? CreatorEmployeeId { get; set; }

    public string ClientName { get; set; } = null!;

    public string CampaignName { get; set; } = null!;

    public int? CampaignId { get; set; }

    public string Industry { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? Insight { get; set; }

    public string? Concept { get; set; }

    public string? ContentDetails { get; set; }

    public string? ReferenceLink { get; set; }

    public string? MoodboardDesc { get; set; }

    public string? ScriptText { get; set; }

    public DateOnly Deadline { get; set; }

    public int? PrimaryStaffId { get; set; }

    public int? ReviewerEmployeeId { get; set; }

    public string? Status { get; set; }

    public string? FeedbackComment { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Employee? CreatorEmployee { get; set; }

    public virtual Employee? PrimaryStaff { get; set; }

    public virtual Employee? ReviewerEmployee { get; set; }

    public virtual Campaign? Campaign { get; set; }
}
