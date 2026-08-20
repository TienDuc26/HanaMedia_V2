using HanaMedia.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.HumanResourcesManager)]
    public class ManageHumanController : Controller
    {
        public IActionResult HumanResources()
        {
            return View();
        }

        public IActionResult AssignTasks()
        {
            return View();
        }

        public IActionResult Repoted()
        {
            return View();
        }
    }
}
