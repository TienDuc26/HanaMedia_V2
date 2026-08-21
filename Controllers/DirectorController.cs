using HanaMedia.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HanaMedia.Services.Dashboard;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.Director)]
    public class DirectorController : Controller
    {
        private readonly IDirectorMonitoringService _monitoringService;

        public DirectorController(IDirectorMonitoringService monitoringService)
        {
            _monitoringService = monitoringService;
        }
        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult Approve()
        {
            return View();
        }

        public IActionResult BookingCampaign()
        {
            return View();
        }

        public IActionResult Config()
        {
            return View();
        }

        public IActionResult Department()
        {
            return View();
        }

        public IActionResult HumanResources()
        {
            return View();
        }

        public IActionResult Idea()
        {
            return View();
        }

        public async Task<IActionResult> MonitoringSystem(CancellationToken cancellationToken)
        {
            return View(await _monitoringService.GetAsync(cancellationToken));
        }

        public IActionResult Report()
        {
            return View();
        }

        public IActionResult SignContract()
        {
            return View();
        }
    }
}
