using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Services.Tasks;
using HanaMedia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers;

[Authorize(Roles = AppRoles.Director + "," + AppRoles.HumanResourcesManager + "," + AppRoles.HumanResourcesStaff + "," +
                   AppRoles.BookingManager + "," + AppRoles.BookingStaff + "," + AppRoles.IdeaManager + "," + AppRoles.IdeaStaff)]
public sealed class WorkTasksController : Controller
{
    private readonly IWorkTaskService _service;

    public WorkTasksController(IWorkTaskService service) => _service = service;

    [HttpGet("Tasks")]
    public async Task<IActionResult> Index(string? module, string? search, int page = 1, int? employeeId = null, bool create = false, CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var model = await _service.GetPageAsync(userId, role, module, search, page, employeeId, create, cancellationToken);
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
