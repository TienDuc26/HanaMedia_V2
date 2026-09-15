using HanaMedia.Constants;
using HanaMedia.ViewModels;
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
            return View();
        }

        public IActionResult Reported() => RedirectToAction("Index", "Reports", new { type = ReportTypes.Booking });
    }
}
