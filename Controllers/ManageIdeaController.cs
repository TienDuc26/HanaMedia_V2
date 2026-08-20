using HanaMedia.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.IdeaManager)]
    public class ManageIdeaController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult HumanStaff()
        {
            return View();
        }

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
