namespace HanaMedia.Models;

public sealed class WorkTask
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string Module { get; set; } = null!;
    public int AssignedEmployeeId { get; set; }
    public int CreatedByUserId { get; set; }
    public int ReviewerUserId { get; set; }
    public DateTime Deadline { get; set; }
    public string Status { get; set; } = "todo";
    public string? RelatedType { get; set; }
    public int? RelatedId { get; set; }
    public int? CampaignId { get; set; }
    public string? DraftData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Employee AssignedEmployee { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User ReviewerUser { get; set; } = null!;
    public Campaign? Campaign { get; set; }
    public ICollection<WorkTaskHistory> History { get; set; } = new List<WorkTaskHistory>();
    public ICollection<WorkTaskSubmission> Submissions { get; set; } = new List<WorkTaskSubmission>();
}

public static class WorkTaskRelatedTypes
{
    public const string Booking = "Booking";
    public const string Idea = "Idea";
    public const string Employee = "Employee";
    public const string None = "";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { Booking, Idea, Employee });

    public static bool IsValid(string? type) => type is null || type == "" || All.Contains(type);

    public static string GetLabel(string? type) => type switch
    {
        Booking => "Booking",
        Idea => "Ý tưởng",
        Employee => "Nhân sự",
        _ => "Không liên kết"
    };
}
