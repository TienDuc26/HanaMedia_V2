using System;
using System.Globalization;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using HanaMedia.Constants;
using HanaMedia.Services.Tasks;
using HanaMedia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HanaMedia.Models;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Controllers;

[Authorize(Roles = AppRoles.Director + "," + AppRoles.HumanResourcesManager + "," + AppRoles.HumanResourcesStaff + "," +
                   AppRoles.BookingManager + "," + AppRoles.BookingStaff + "," + AppRoles.IdeaManager + "," + AppRoles.IdeaStaff)]
public sealed class WorkTasksController : Controller
{
    private readonly IWorkTaskService _service;
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _context;

    public WorkTasksController(IWorkTaskService service, IWebHostEnvironment env, ApplicationDbContext context)
    {
        _service = service;
        _env = env;
        _context = context;
    }

    [HttpGet("Tasks")]
    public async Task<IActionResult> Index(
        string? module, string? search, int page = 1, int? employeeId = null,
        bool create = false, string? status = null, int? reviewerId = null,
        bool? overdue = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var model = await _service.GetPageAsync(userId, role, module, search, page, employeeId, create, status, reviewerId, overdue, cancellationToken);
        
        ViewBag.Campaigns = await _context.Campaigns
            .Where(c => c.Status != "cancelled")
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return View(model);
    }

    [HttpPost("Tasks/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateWorkTaskInputModel input, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        WorkTaskOperationResult result;
        if (!ModelState.IsValid)
        {
            result = WorkTaskOperationResult.Failure("Dữ liệu giao việc chưa hợp lệ. Vui lòng kiểm tra lại.");
        }
        else
        {
            result = await _service.CreateAsync(input, userId, role, cancellationToken);
        }
        SetMessage(result);
        return RedirectToAction(nameof(Index), new { module = input.Module });
    }

    [HttpPost("Tasks/Transition")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(TransitionWorkTaskInputModel input, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var result = ModelState.IsValid
            ? await _service.TransitionAsync(input, userId, role, cancellationToken)
            : WorkTaskOperationResult.Failure("Yêu cầu cập nhật trạng thái không hợp lệ.");
        SetMessage(result);
        return RedirectToAction(nameof(Index), new { module = input.Module });
    }

    [HttpGet("Tasks/Workspace/{id:int}")]
    public async Task<IActionResult> Workspace(int id, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        try
        {
            var model = await _service.GetWorkspaceDetailsAsync(id, userId, role, cancellationToken);
            return View(model);
        }
        catch (KeyNotFoundException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost("Tasks/SaveDraft")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft(int taskId, string draftDataJson, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Json(new { success = false, message = "Chưa đăng nhập." });
        var result = await _service.SaveDraftAsync(taskId, draftDataJson, userId, cancellationToken);
        return Json(new { success = result.Succeeded, message = result.Message });
    }

    [HttpPost("Tasks/Submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int taskId, string result, string? notes, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var operationResult = await _service.SubmitReviewAsync(taskId, result, notes ?? string.Empty, userId, cancellationToken);
        SetMessage(operationResult);
        return RedirectToAction(nameof(Workspace), new { id = taskId });
    }

    [HttpPost("Tasks/Review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int taskId, string targetStatus, string? feedback, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var operationResult = await _service.ReviewTaskAsync(taskId, targetStatus, feedback, userId, role, cancellationToken);
        SetMessage(operationResult);
        return RedirectToAction(nameof(Workspace), new { id = taskId });
    }

    [HttpPost("Tasks/UploadAttachment")]
    public async Task<IActionResult> UploadAttachment(int taskId, IFormFile file)
    {
        if (!TryGetIdentity(out var userId, out var role))
            return Json(new { success = false, message = "Chưa đăng nhập." });

        if (file == null || file.Length == 0)
            return Json(new { success = false, message = "Không nhận được file." });

        // Max 10MB
        if (file.Length > 10 * 1024 * 1024)
            return Json(new { success = false, message = "File vượt quá dung lượng cho phép (tối đa 10MB)." });

        try
        {
            var taskFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "tasks", taskId.ToString());
            Directory.CreateDirectory(taskFolder);

            var safeFileName = Path.GetFileName(file.FileName);
            var uniqueFileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 6)}_{safeFileName}";
            var fullPath = Path.Combine(taskFolder, uniqueFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/uploads/tasks/{taskId}/{uniqueFileName}";
            return Json(new { success = true, name = safeFileName, url = url, size = file.Length });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Lỗi upload file: {ex.Message}" });
        }
    }

    private bool TryGetIdentity(out int userId, out string role)
    {
        role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
            CultureInfo.InvariantCulture, out userId);
    }

    private void SetMessage(WorkTaskOperationResult result)
    {
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
    }
}
