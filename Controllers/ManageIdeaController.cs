using HanaMedia.Constants;
using HanaMedia.Services.Ideas;
using HanaMedia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.IdeaManager)]
    public class ManageIdeaController : Controller
    {
        private readonly IIdeaService _ideaService;

        public ManageIdeaController(IIdeaService ideaService)
        {
            _ideaService = ideaService;
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult HumanStaff()
        {
            return RedirectToAction("Index", "WorkTasks", new { module = WorkTaskModules.Ideas });
        }

        [HttpGet]
        public async Task<IActionResult> Idea(string? search, string? status, int page = 1, CancellationToken cancellationToken = default)
        {
            if (!TryGetUserId(out var userId)) return Challenge();
            return View(await _ideaService.GetPageAsync(userId, AppRoles.IdeaManager, search, status, page, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(42 * 1024 * 1024)]
        public async Task<IActionResult> CreateIdea(SaveIdeaInputModel input, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Challenge();
            var result = ModelState.IsValid
                ? await _ideaService.CreateAsync(input, userId, AppRoles.IdeaManager, cancellationToken)
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
                ? await _ideaService.UpdateAsync(input, userId, AppRoles.IdeaManager, cancellationToken)
                : IdeaOperationResult.Failure(GetModelErrors());
            SetMessage(result);
            return RedirectToAction(nameof(Idea));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitIdea(int id, CancellationToken cancellationToken)
            => await Run(id, (service, userId) => service.SubmitAsync(id, userId, AppRoles.IdeaManager, cancellationToken));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewIdea(int id, string decision, string? feedback, CancellationToken cancellationToken)
            => await Run(id, (service, userId) => service.ReviewAsync(id, decision, feedback, userId, AppRoles.IdeaManager, cancellationToken));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AdvanceIdea(int id, CancellationToken cancellationToken)
            => await Run(id, (service, userId) => service.AdvanceAsync(id, userId, AppRoles.IdeaManager, cancellationToken));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int id, string? content, CancellationToken cancellationToken)
            => await Run(id, (service, userId) => service.AddCommentAsync(id, content, userId, AppRoles.IdeaManager, cancellationToken));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteIdea(int id, CancellationToken cancellationToken)
            => await Run(id, (service, userId) => service.DeleteAsync(id, userId, AppRoles.IdeaManager, cancellationToken));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMoodboardImage(long imageId, int ideaId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Challenge();
            SetMessage(await _ideaService.DeleteMoodboardImageAsync(imageId, userId, AppRoles.IdeaManager, cancellationToken));
            return RedirectToAction(nameof(Idea), new { focus = ideaId });
        }

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
