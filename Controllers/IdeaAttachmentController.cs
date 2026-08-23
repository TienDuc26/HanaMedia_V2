using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Controllers;

/// <summary>
/// API riêng cho upload nhiều ảnh moodboard + xoá từng ảnh (Vấn đề 2 — Moodboard nhiều ảnh).
/// Chỉ QL Ý tưởng và NV Ý tưởng (chủ sở hữu ý tưởng) được upload/xoá; Giám đốc/Booking chỉ xem.
/// Trả JSON để JS trong view ManageIdea/Idea.cshtml render gallery + nút xoá từng ảnh.
/// </summary>
[Authorize(Roles = AppRoles.IdeaManager + "," + AppRoles.IdeaStaff)]
public class IdeaAttachmentController : Controller
{
    private const long MaxBytesPerFile = 5L * 1024 * 1024;
    private const int MaxFilesPerRequest = 12;

    private readonly ApplicationDbContext _context;
    private readonly IdeaAttachmentService _attachments;

    public IdeaAttachmentController(ApplicationDbContext context, IdeaAttachmentService attachments)
    {
        _context = context;
        _attachments = attachments;
    }

    [HttpPost("Ideas/UploadMoodboardImages")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(60_000_000)]
    public async Task<IActionResult> UploadMoodboardImages(int ideaId, IFormFile[]? files,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out var userId, out var role, out var employeeId))
            return Challenge();

        if (files is null || files.Length == 0)
            return BadRequest(new { success = false, message = "Vui lòng chọn ít nhất 1 ảnh." });
        if (files.Length > MaxFilesPerRequest)
            return BadRequest(new { success = false, message = $"Chỉ được upload tối đa {MaxFilesPerRequest} ảnh một lần." });

        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == ideaId, cancellationToken);
        if (idea is null)
            return NotFound(new { success = false, message = "Không tìm thấy ý tưởng." });

        if (!CanEditThisIdea(idea, role, employeeId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, message = "Bạn không có quyền upload ảnh cho ý tưởng này." });

        // Không cho upload khi ý tưởng đã ở trạng thái sau duyệt.
        if (idea.Status is IdeaStatuses.Approved or IdeaStatuses.InProduction or IdeaStatuses.Done)
            return BadRequest(new { success = false, message = "Ý tưởng đã ở trạng thái sau duyệt — không thể thêm ảnh moodboard." });

        var uploaded = new List<IdeaMoodboardImage>(files.Length);
        var existingMax = await _context.IdeaMoodboardImages
            .Where(m => m.IdeaId == ideaId)
            .Select(m => (int?)m.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        try
        {
            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                if (file is null || file.Length == 0) continue;
                if (file.Length > MaxBytesPerFile)
                    throw new InvalidOperationException(
                        $"Ảnh '{file.FileName}' vượt quá {MaxBytesPerFile / 1024 / 1024} MB.");

                await using var ms = new MemoryStream();
                await file.CopyToAsync(ms, cancellationToken);
                var url = await _attachments.SaveAsync(
                    ideaId, "mood",
                    ms.ToArray(),
                    file.ContentType ?? "application/octet-stream",
                    file.FileName);

                uploaded.Add(new IdeaMoodboardImage
                {
                    IdeaId = ideaId,
                    FileUrl = url,
                    SortOrder = existingMax + 1 + i,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (uploaded.Count == 0)
                return BadRequest(new { success = false, message = "Không có ảnh hợp lệ nào được upload." });

            _context.IdeaMoodboardImages.AddRange(uploaded);
            idea.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                message = $"Đã upload {uploaded.Count} ảnh moodboard.",
                images = uploaded.Select(u => new { id = u.Id, url = u.FileUrl, sortOrder = u.SortOrder })
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("Ideas/DeleteMoodboardImage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMoodboardImage(long imageId, CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out _, out var role, out var employeeId))
            return Challenge();

        var img = await _context.IdeaMoodboardImages
            .FirstOrDefaultAsync(m => m.Id == imageId, cancellationToken);
        if (img is null)
            return NotFound(new { success = false, message = "Không tìm thấy ảnh." });

        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == img.IdeaId, cancellationToken);
        if (idea is null)
            return NotFound(new { success = false, message = "Không tìm thấy ý tưởng." });

        if (!CanEditThisIdea(idea, role, employeeId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, message = "Bạn không có quyền xoá ảnh này." });

        _attachments.Delete(img.FileUrl);
        _context.IdeaMoodboardImages.Remove(img);
        idea.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Đã xoá ảnh." });
    }

    private static bool CanEditThisIdea(Idea idea, string role, int? employeeId)
    {
        if (role == AppRoles.IdeaManager) return true;
        if (role == AppRoles.IdeaStaff && employeeId.HasValue)
        {
            return idea.CreatorEmployeeId == employeeId.Value
                || idea.PrimaryStaffId == employeeId.Value;
        }
        return false;
    }

    private bool TryGetIdentity(out int userId, out string role, out int? employeeId)
    {
        role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        employeeId = int.TryParse(User.FindFirstValue("employee_id"), NumberStyles.None,
            CultureInfo.InvariantCulture, out var empId) ? empId : null;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
            CultureInfo.InvariantCulture, out userId);
    }
}