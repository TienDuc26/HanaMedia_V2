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

    /// <summary>
    /// Liên kết task với một ý tưởng cụ thể (Module 13). Nullable: task không gắn với ý tư�ng nào.
    /// Khi QL Ý tưởng giao task t� màn hình chi tiết ý tưởng, cột này được gán tự động.
    /// </summary>
    public int? IdeaId { get; set; }

    /// <summary>
    /// Hạng mục công việc trong ý tưởng (Module 13). VD: "concept", "script", "moodboard",
    /// "reference", "content", "full", "other". Null = không gắn với hạng mục cụ thể nào.
    /// </summary>
    public string? WorkCategory { get; set; }

    public Employee AssignedEmployee { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User ReviewerUser { get; set; } = null!;
    public Idea? Idea { get; set; }
    public ICollection<WorkTaskHistory> History { get; set; } = new List<WorkTaskHistory>();
}
