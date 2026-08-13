using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
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
