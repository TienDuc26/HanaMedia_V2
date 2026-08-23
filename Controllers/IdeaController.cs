using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Services.Ideas;
using HanaMedia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers;

/// <summary>
/// API endpoint cho Module 13: CRUD + chuyển trạng thái + comment + xem chi tiết ý tưởng.
/// Phân quyền:
///  - Create/Update/Transition/Comment: chỉ QL Ý tư�ng / NV Ý tưởng
///  - Detail (GET): mở cho tất cả role được xem (Giám đốc, QL/NV Booking) — chỉ đọc.
/// </summary>
[Authorize(Roles = AppRoles.IdeaManager + "," + AppRoles.IdeaStaff + "," + AppRoles.Director + "," +
                   AppRoles.BookingManager + "," + AppRoles.BookingStaff)]
public sealed class IdeaController : Controller
{
    private readonly IIdeaService _service;

    public IdeaController(IIdeaService service) => _service = service;

    [HttpGet("Ideas/Detail/{ideaId:int}")]
    public async Task<IActionResult> Detail(int ideaId, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role, out var employeeId))
            return Challenge();

        var detail = await _service.GetDetailAsync(ideaId, userId, role, employeeId, cancellationToken);
        if (detail is null)
        {
            return Json(new { success = false, message = "Không tìm thấy ý tưởng hoặc bạn không có quyền xem." });
        }
        return Json(new { success = true, idea = detail });
    }

    [HttpPost("Ideas/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] IdeaUpsertInputModel input,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role, out var employeeId))
            return Challenge();

        var result = ModelState.IsValid
            ? await _service.CreateAsync(input, userId, role, employeeId, cancellationToken)
            : IdeaOperationResult.Failure("Dữ liệu ý tưởng chưa hợp lệ. Vui lòng kiểm tra lại.");

        SetMessage(result);
        return RedirectToAction("Idea", RoleHomeController(role));
    }

    [HttpPost("Ideas/Update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        [FromForm] IdeaUpsertInputModel input,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role, out var employeeId))
            return Challenge();

        var result = ModelState.IsValid
            ? await _service.UpdateAsync(input, userId, role, employeeId, cancellationToken)
            : IdeaOperationResult.Failure("Dữ liệu cập nhật ý tưởng chưa hợp lệ.");

        SetMessage(result);
        return RedirectToAction("Idea", RoleHomeController(role));
    }

    [HttpPost("Ideas/Transition")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(
        [FromForm] IdeaTransitionInputModel input,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role, out var employeeId))
            return Challenge();

        var result = ModelState.IsValid
            ? await _service.TransitionAsync(input, userId, role, employeeId, cancellationToken)
            : IdeaOperationResult.Failure("Yêu cầu chuyển trạng thái không hợp lệ.");

        SetMessage(result);
        return RedirectToAction("Idea", RoleHomeController(role));
    }

    [HttpPost("Ideas/Comment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Comment(
        [FromForm] IdeaCommentInputModel input,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role, out _))
            return Challenge();

        var result = ModelState.IsValid
            ? await _service.AddCommentAsync(input, userId, role, cancellationToken)
            : IdeaOperationResult.Failure("Nội dung bình luận chưa hợp lệ.");

        SetMessage(result);
        return RedirectToAction("Idea", RoleHomeController(role));
    }

    private bool TryGetIdentity(out int userId, out string role, out int? employeeId)
    {
        role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        employeeId = int.TryParse(User.FindFirstValue("employee_id"), NumberStyles.None,
            CultureInfo.InvariantCulture, out var empId) ? empId : null;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
            CultureInfo.InvariantCulture, out userId);
    }

    private void SetMessage(IdeaOperationResult result)
    {
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
    }

    private static string RoleHomeController(string role) => role switch
    {
        AppRoles.IdeaManager => "ManageIdea",
        AppRoles.IdeaStaff => "IdeaStaff",
        _ => "Home"
    };
}
