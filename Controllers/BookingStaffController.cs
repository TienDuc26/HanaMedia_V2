using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
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

        public IActionResult Reported()
        {
            return View();
        }
    }
}
