using HanaMedia.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.BookingManager)]
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
            return RedirectToAction("Index", "Kols");
        }

        public IActionResult Reported()
        {
            return View();
        }

        public IActionResult StaffHuman()
        {
            return RedirectToAction("Index", "WorkTasks", new { module = WorkTaskModules.Booking });
        }
    }
}
