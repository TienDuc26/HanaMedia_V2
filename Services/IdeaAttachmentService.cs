using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace HanaMedia.Services;

/// <summary>
/// Helper lưu file đính kèm (reference / moodboard) cho ý tưởng vào wwwroot/uploads/ideas/.
/// Tương tự EmployeeAvatarService nhưng cho phép nhiều MIME/extension hơn (ảnh + PDF + file tài liệu).
/// </summary>
public class IdeaAttachmentService
{
    private const string UploadFolder = "uploads/ideas";
    private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private static readonly HashSet<string> AllowedImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    private static readonly HashSet<string> AllowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt"
    };

    private static readonly HashSet<string> AllowedDocumentMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain"
    };

    private readonly IWebHostEnvironment _env;

    public IdeaAttachmentService(IWebHostEnvironment env)
    {
        _env = env;
    }

    /// <summary>
    /// Lưu file upload cho một ý tưởng. Trả về URL public (ví dụ: /uploads/ideas/idea_5_xxx.png)
    /// hoặc throw InvalidOperationException nếu không hợp lệ.
    /// </summary>
    public async Task<string> SaveAsync(int ideaId, string field, byte[] bytes, string mimeType, string originalFileName)
    {
        if (bytes is null || bytes.Length == 0)
            throw new InvalidOperationException("Tệp đính kèm rỗng.");
        if (bytes.Length > MaxBytes)
            throw new InvalidOperationException(
                $"Tệp vượt quá dung lượng cho phép ({MaxBytes / 1024 / 1024} MB).");

        var ext = Path.GetExtension(originalFileName)?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(ext)) ext = ".bin";

        bool isImage = AllowedImageExtensions.Contains(ext) || AllowedImageMimeTypes.Contains(mimeType);
        bool isDoc = AllowedDocumentExtensions.Contains(ext) || AllowedDocumentMimeTypes.Contains(mimeType);
        if (!isImage && !isDoc)
        {
            throw new InvalidOperationException(
                "Định dạng tệp không được hỗ trợ (chỉ chấp nhận ảnh JPG/PNG/WEBP/GIF hoặc tài liệu PDF/DOC/XLS/PPT/TXT).");
        }

        var uploadsRoot = Path.Combine(
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            UploadFolder);
        Directory.CreateDirectory(uploadsRoot);

        var safeField = field.Equals("reference", StringComparison.OrdinalIgnoreCase) ? "ref" : "mood";
        var fileName = $"idea_{ideaId}_{safeField}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadsRoot, fileName);
        await File.WriteAllBytesAsync(fullPath, bytes);

        return $"/{UploadFolder}/{fileName}";
    }

    /// <summary>
    /// Xoá file đính kèm cũ nếu tồn tại. Bỏ qua lỗi file đang bị lock.
    /// </summary>
    public void Delete(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return;
        if (!relativeUrl.StartsWith($"/{UploadFolder}/", StringComparison.OrdinalIgnoreCase)) return;

        var physical = Path.Combine(
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(physical))
        {
            try { File.Delete(physical); } catch { /* bỏ qua nếu đang bị lock */ }
        }
    }

    /// <summary>
    /// Lưu nhiều ảnh moodboard cho 1 ý tưởng. Mỗi ảnh phải là định dạng ảnh hợp lệ (jpg/png/webp/gif), dung lượng tối đa 5MB/ảnh.
    /// Trả về danh sách URL public theo thứ tự file nhận được.
    /// </summary>
    public async Task<IReadOnlyList<string>> SaveManyAsync(
        int ideaId,
        string field,
        IReadOnlyList<(byte[] Bytes, string MimeType, string FileName)> files)
    {
        if (files is null || files.Count == 0)
            throw new InvalidOperationException("Không có ảnh nào được chọn.");

        var results = new List<string>(files.Count);
        foreach (var file in files)
        {
            results.Add(await SaveAsync(ideaId, field, file.Bytes, file.MimeType, file.FileName));
        }
        return results;
    }
}
