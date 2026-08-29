using System;

namespace HanaMedia.Models;

public sealed class WorkTaskSubmission
{
    public int Id { get; set; }
    public int WorkTaskId { get; set; }
    public int Version { get; set; }
    public int SubmittedByUserId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string Result { get; set; } = null!;
    public string? Notes { get; set; }
    public string? FilesJson { get; set; } // JSON list of file metadata: Name, Url, Size, UploadedAt
    public string? Feedback { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string Status { get; set; } = "review"; // review, approved, need_revision

    public WorkTask WorkTask { get; set; } = null!;
    public User SubmittedByUser { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
}
