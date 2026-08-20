using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
    [Authorize]
    public class ManageBookingController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult Booking()
        {
            return View();
        }

        public IActionResult KOL_KOC()
        {
            return View();
        }

        public IActionResult Reported()
        {
            return View();
        }

        public IActionResult StaffHuman()
        {
            return View();
        }
    }
}
