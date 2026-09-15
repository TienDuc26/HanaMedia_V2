using HanaMedia.Constants;
using HanaMedia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.HumanResourcesManager + "," + AppRoles.HumanResourcesStaff)]
    public class ManageHumanController : Controller
    {
        public IActionResult HumanResources()
        {
            ViewBag.IsStaff = User.IsInRole(AppRoles.HumanResourcesStaff)
                              && !User.IsInRole(AppRoles.HumanResourcesManager);
            return View();
        }

        public IActionResult AssignTasks()
        {
            return RedirectToAction("Index", "WorkTasks", new { module = WorkTaskModules.HumanResources });
        }

        public IActionResult Repoted() => RedirectToAction("Index", "Reports", new { type = ReportTypes.HumanResources });
    }
}
