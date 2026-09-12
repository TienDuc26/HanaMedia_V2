using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Services.Ideas;
using HanaMedia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers;

[ApiController]
[Route("api/idea-library")]
[Authorize(Roles = AppRoles.Director + "," + AppRoles.IdeaManager + "," + AppRoles.IdeaStaff)]
public sealed class IdeaLibraryController : ControllerBase
{
    private readonly IIdeaLibraryService _service;

    public IdeaLibraryController(IIdeaLibraryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IdeaLibraryPageViewModel>> GetPage(
        [FromQuery] IdeaLibraryQueryModel query,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        try
        {
            return Ok(await _service.GetPageAsync(userId, role, query, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<IdeaLibraryItemViewModel>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var item = await _service.GetByIdAsync(id, userId, role, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:int}/classification")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateClassification(
        int id,
        [FromBody] UpdateIdeaClassificationInputModel input,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Challenge();
        var result = await _service.UpdateClassificationAsync(id, input, userId, role, cancellationToken);
        return result.Succeeded
            ? Ok(new { success = true, message = result.Message })
            : BadRequest(new { success = false, message = result.Message });
    }

    private bool TryGetIdentity(out int userId, out string role)
    {
        role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out userId);
    }
}
