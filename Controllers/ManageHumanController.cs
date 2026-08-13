using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
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
