using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers;

[Authorize(Roles = AppRoles.Director + "," + AppRoles.HumanResourcesManager + "," +
    AppRoles.HumanResourcesStaff + "," + AppRoles.BookingManager + "," +
    AppRoles.BookingStaff + "," + AppRoles.IdeaManager + "," + AppRoles.IdeaStaff)]
public sealed class ReportsController : Controller
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService) => _reportService = reportService;

    [HttpGet("Reports")]
    public async Task<IActionResult> Index(string? type, string? period, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role)) return Challenge();
        var model = await _reportService.GetAsync(type, period, userId, role, cancellationToken);
        return model is null ? Forbid() : View(model);
    }

    [HttpGet("Reports/Export")]
    public async Task<IActionResult> Export(string? type, string? period, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var role)) return Challenge();
        var content = await _reportService.ExportCsvAsync(type, period, userId, role, cancellationToken);
        if (content is null) return Forbid();
        var safeType = type is "booking" or "ideas" ? type : "human_resources";
        return File(content, "text/csv; charset=utf-8", $"HanaMedia_{safeType}_{DateTime.Now:yyyyMMdd}.csv");
    }

    private bool TryGetActor(out int userId, out string role)
    {
        role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out userId);
    }
}
