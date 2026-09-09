namespace HanaMedia.Models;

public sealed class IdeaComment
{
    public long Id { get; set; }
    public int IdeaId { get; set; }
    public int? AuthorUserId { get; set; }
    public string CommentType { get; set; } = "general";
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public Idea Idea { get; set; } = null!;
    public User? AuthorUser { get; set; }
}
