using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
    [Authorize]
    public class AdminITController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult Account()
        {
            return View();
        }

        public IActionResult AuditLog()
        {
            return View();
        }

        public IActionResult ConfigSystem()
        {
            return View();
        }
    }
}
