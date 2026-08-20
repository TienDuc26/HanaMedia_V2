using HanaMedia.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.HumanResourcesStaff)]
    public class HumanResourcesStaffController : Controller
    {
        public IActionResult HumanResources()
        {
            return View();
        }

        public IActionResult Reported()
        {
            return View();
        }
    }
}
