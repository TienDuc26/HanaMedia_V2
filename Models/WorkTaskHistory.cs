namespace HanaMedia.Models;

public sealed class WorkTaskHistory
{
    public long Id { get; set; }
    public int WorkTaskId { get; set; }
    public int? ActorUserId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = null!;
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public WorkTask WorkTask { get; set; } = null!;
    public User? ActorUser { get; set; }
}
