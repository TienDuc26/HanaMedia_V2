using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Services.Campaigns;
using HanaMedia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers;

[Authorize(Roles = AppRoles.Director + "," + AppRoles.BookingManager + "," + AppRoles.BookingStaff + "," + AppRoles.IdeaManager + "," + AppRoles.IdeaStaff)]
[Route("Campaigns")]
public sealed class CampaignsController : Controller
{
    private readonly ICampaignService _service;
    public CampaignsController(ICampaignService service) => _service = service;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out _, out var role)) return Challenge();
        return View(await _service.GetPageAsync(role, search, status, page, cancellationToken));
    }

    [HttpPost("Create")]
    [Authorize(Roles = AppRoles.BookingManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCampaignInputModel input, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var result = ModelState.IsValid
            ? await _service.CreateAsync(input, userId, role, cancellationToken)
            : CampaignOperationResult.Failure("Dữ liệu chiến dịch chưa hợp lệ.");
        SetMessage(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Update")]
    [Authorize(Roles = AppRoles.BookingManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateCampaignInputModel input, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var result = ModelState.IsValid
            ? await _service.UpdateAsync(input, userId, role, cancellationToken)
            : CampaignOperationResult.Failure("Dữ liệu cập nhật chưa hợp lệ.");
        SetMessage(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete")]
    [Authorize(Roles = AppRoles.BookingManager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(DeleteCampaignInputModel input, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var result = ModelState.IsValid
            ? await _service.DeleteAsync(input, userId, role, cancellationToken)
            : CampaignOperationResult.Failure("Yêu cầu xóa không hợp lệ.");
        SetMessage(result);
        return RedirectToAction(nameof(Index));
    }

    private bool TryGetIdentity(out int userId, out string role)
    {
        role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
            CultureInfo.InvariantCulture, out userId);
    }

    private void SetMessage(CampaignOperationResult result)
        => TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
}
