using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Services;
using HanaMedia.Services.Ideas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers;

[Authorize(Roles = AppRoles.IdeaManager)]
public class ManageIdeaController : Controller
{
    private readonly IIdeaService _ideaService;

    public ManageIdeaController(IIdeaService ideaService) => _ideaService = ideaService;

    public IActionResult Dashboard() => View();

    public IActionResult HumanStaff()
    {
        return RedirectToAction("Index", "WorkTasks", new { module = WorkTaskModules.Ideas });
    }

    [HttpGet("ManageIdea/Idea")]
    public async Task<IActionResult> Idea(
        string? search, string? status, string? client, int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out var userId, out var role, out var employeeId))
            return Challenge();

        var model = await _ideaService.GetPageAsync(
            userId, role, employeeId, search, status, client, page, cancellationToken);
        return View(model);
    }

    [HttpPost("ManageIdea/Idea/UploadReference")]
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
            // Cập nhật entity idea
            var detail = await _ideaService.GetDetailAsync(ideaId, userId, role, employeeId, cancellationToken);
            if (detail is null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ý tưởng.";
                return RedirectToAction(nameof(Idea));
            }
            var db = HttpContext.RequestServices.GetRequiredService<Models.ApplicationDbContext>();
            var idea = await db.Ideas.FindAsync(new object?[] { ideaId }, cancellationToken);
            if (idea is null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ý tưởng.";
                return RedirectToAction(nameof(Idea));
            }
            // Xoá file cũ nếu có
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

    [HttpPost("ManageIdea/Idea/UploadMoodboard")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadMoodboard(int ideaId, IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out var userId, out var role, out var employeeId))
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

    public IActionResult Reported() => View();

    private bool TryGetIdentity(out int userId, out string role, out int? employeeId)
    {
        role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        employeeId = int.TryParse(User.FindFirstValue("employee_id"), NumberStyles.None,
            CultureInfo.InvariantCulture, out var empId) ? empId : null;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
            CultureInfo.InvariantCulture, out userId);
    }
}
