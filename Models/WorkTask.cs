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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Employee AssignedEmployee { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User ReviewerUser { get; set; } = null!;
    public ICollection<WorkTaskHistory> History { get; set; } = new List<WorkTaskHistory>();
}
