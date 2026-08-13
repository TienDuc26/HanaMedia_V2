using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
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
