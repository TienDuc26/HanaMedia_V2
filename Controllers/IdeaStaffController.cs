using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
    [Authorize]
    public class IdeaStaffController : Controller
    {
        public IActionResult Idea()
        {
            return View();
        }

        public IActionResult Reported()
        {
            return View();
        }
    }
}
