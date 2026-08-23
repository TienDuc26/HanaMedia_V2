using HanaMedia.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.BookingStaff)]
    public class BookingStaffController : Controller
    {
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
    }
}
