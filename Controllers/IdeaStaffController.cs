using HanaMedia.Constants;
using HanaMedia.Services.Ideas;
using HanaMedia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.IdeaStaff)]
    public class IdeaStaffController : Controller
    {
        private readonly IIdeaService _ideaService;

        public IdeaStaffController(IIdeaService ideaService)
        {
            _ideaService = ideaService;
        }

        [HttpGet]
        public async Task<IActionResult> Idea(string? search, string? status, int page = 1, CancellationToken cancellationToken = default)
        {
            if (!TryGetUserId(out var userId)) return Challenge();
            return View(await _ideaService.GetPageAsync(userId, AppRoles.IdeaStaff, search, status, page, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(42 * 1024 * 1024)]
        public async Task<IActionResult> CreateIdea(SaveIdeaInputModel input, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Challenge();
            var result = ModelState.IsValid
                ? await _ideaService.CreateAsync(input, userId, AppRoles.IdeaStaff, cancellationToken)
                : IdeaOperationResult.Failure(GetModelErrors());
            SetMessage(result);
            return RedirectToAction(nameof(Idea));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(42 * 1024 * 1024)]
        public async Task<IActionResult> UpdateIdea(SaveIdeaInputModel input, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Challenge();
            var result = ModelState.IsValid
                ? await _ideaService.UpdateAsync(input, userId, AppRoles.IdeaStaff, cancellationToken)
                : IdeaOperationResult.Failure(GetModelErrors());
            SetMessage(result);
            return RedirectToAction(nameof(Idea));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitIdea(int id, CancellationToken cancellationToken)
            => await Run(id, (service, userId) => service.SubmitAsync(id, userId, AppRoles.IdeaStaff, cancellationToken));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AdvanceIdea(int id, CancellationToken cancellationToken)
            => await Run(id, (service, userId) => service.AdvanceAsync(id, userId, AppRoles.IdeaStaff, cancellationToken));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int id, string? content, CancellationToken cancellationToken)
            => await Run(id, (service, userId) => service.AddCommentAsync(id, content, userId, AppRoles.IdeaStaff, cancellationToken));

        public IActionResult Reported()
        {
            return View();
        }

        private async Task<IActionResult> Run(int id, Func<IIdeaService, int, Task<IdeaOperationResult>> operation)
        {
            if (!TryGetUserId(out var userId)) return Challenge();
            SetMessage(await operation(_ideaService, userId));
            return RedirectToAction(nameof(Idea), new { focus = id });
        }

        private bool TryGetUserId(out int userId) => int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out userId);

        private string GetModelErrors() => string.Join(" ", ModelState.Values.SelectMany(v => v.Errors)
            .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Dữ liệu ý tưởng không hợp lệ." : e.ErrorMessage));

        private void SetMessage(IdeaOperationResult result) =>
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
    }
}
