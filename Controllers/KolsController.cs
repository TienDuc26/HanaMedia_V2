using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Services.Kols;
using HanaMedia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers;

[Authorize(Roles = AppRoles.Director + "," + AppRoles.BookingManager + "," + AppRoles.BookingStaff)]
[Route("Kols")]
public sealed class KolsController : Controller
{
    private readonly IKolService _service;
    public KolsController(IKolService service) => _service = service;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? platform, string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        return View(await _service.GetPageAsync(userId, role, search, platform, status, page, cancellationToken));
    }

    [HttpPost("Create")]
    [Authorize(Roles = AppRoles.BookingManager + "," + AppRoles.BookingStaff)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateKolInputModel input, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var result = ModelState.IsValid
            ? await _service.CreateAsync(input, userId, role, cancellationToken)
            : KolOperationResult.Failure("Dữ liệu KOL/KOC chưa hợp lệ.");
        SetMessage(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Update")]
    [Authorize(Roles = AppRoles.BookingManager + "," + AppRoles.BookingStaff)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateKolInputModel input, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var result = ModelState.IsValid
            ? await _service.UpdateAsync(input, userId, role, cancellationToken)
            : KolOperationResult.Failure("Dữ liệu cập nhật chưa hợp lệ.");
        SetMessage(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete")]
    [Authorize(Roles = AppRoles.BookingManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(DeleteKolInputModel input, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var result = ModelState.IsValid
            ? await _service.DeleteAsync(input, userId, role, cancellationToken)
            : KolOperationResult.Failure("Yêu cầu xóa không hợp lệ.");
        SetMessage(result);
        return RedirectToAction(nameof(Index));
    }

    private bool TryGetIdentity(out int userId, out string role)
    {
        role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
            CultureInfo.InvariantCulture, out userId);
    }

    private void SetMessage(KolOperationResult result)
        => TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
}
