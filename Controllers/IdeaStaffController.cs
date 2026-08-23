using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Services;
using HanaMedia.Services.Ideas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers;

[Authorize(Roles = AppRoles.IdeaStaff)]
public class IdeaStaffController : Controller
{
    private readonly IIdeaService _ideaService;

    public IdeaStaffController(IIdeaService ideaService) => _ideaService = ideaService;

    [HttpGet("IdeaStaff/Idea")]
    public async Task<IActionResult> Idea(
        string? search, string? status, string? client, int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out var userId, out var role, out var employeeId))
            return Challenge();

        var model = await _ideaService.GetPageAsync(
            userId, role, employeeId, search, status, client, page, cancellationToken);
        return View("~/Views/ManageIdea/Idea.cshtml", model);
    }

    public IActionResult Reported() => View();

    [HttpPost("IdeaStaff/Idea/UploadReference")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadReference(int ideaId, IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out var userId, out var role, out var employeeId))
            return Challenge();
        if (file is null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn tệp đính kèm.";
            return RedirectToAction(nameof(Idea));
        }
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);
        var service = HttpContext.RequestServices.GetRequiredService<IdeaAttachmentService>();
        try
        {
            var url = await service.SaveAsync(ideaId, "reference",
                ms.ToArray(), file.ContentType ?? "application/octet-stream", file.FileName);
            var db = HttpContext.RequestServices.GetRequiredService<Models.ApplicationDbContext>();
            var idea = await db.Ideas.FindAsync(new object?[] { ideaId }, cancellationToken);
            if (idea is null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ý tưởng.";
                return RedirectToAction(nameof(Idea));
            }
            // NV chỉ được upload file cho ý tưởng của mình.
            if (idea.CreatorEmployeeId != employeeId && idea.PrimaryStaffId != employeeId)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền upload file cho ý tưởng này.";
                return RedirectToAction(nameof(Idea));
            }
            service.Delete(idea.ReferenceFileUrl);
            idea.ReferenceFileUrl = url;
            idea.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = "Đã upload tệp tham khảo.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Idea));
    }

    [HttpPost("IdeaStaff/Idea/UploadMoodboard")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadMoodboard(int ideaId, IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out _, out var role, out var employeeId))
            return Challenge();
        if (file is null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn tệp moodboard.";
            return RedirectToAction(nameof(Idea));
        }
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);
        var service = HttpContext.RequestServices.GetRequiredService<IdeaAttachmentService>();
        try
        {
            var url = await service.SaveAsync(ideaId, "moodboard",
                ms.ToArray(), file.ContentType ?? "application/octet-stream", file.FileName);
            var db = HttpContext.RequestServices.GetRequiredService<Models.ApplicationDbContext>();
            var idea = await db.Ideas.FindAsync(new object?[] { ideaId }, cancellationToken);
            if (idea is null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ý tưởng.";
                return RedirectToAction(nameof(Idea));
            }
            if (idea.CreatorEmployeeId != employeeId && idea.PrimaryStaffId != employeeId)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền upload file cho ý tưởng này.";
                return RedirectToAction(nameof(Idea));
            }
            service.Delete(idea.MoodboardFileUrl);
            idea.MoodboardFileUrl = url;
            idea.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = "Đã upload moodboard.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Idea));
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
