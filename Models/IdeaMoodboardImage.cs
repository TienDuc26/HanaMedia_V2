namespace HanaMedia.Models;

public sealed class IdeaMoodboardImage
{
    public long Id { get; set; }
    public int IdeaId { get; set; }
    public string FileUrl { get; set; } = null!;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public Idea Idea { get; set; } = null!;
}
