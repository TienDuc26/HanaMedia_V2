using HanaMedia.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HanaMedia.Services.Dashboard;
using HanaMedia.Models;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;
using HanaMedia.Services.Ideas;
using System.Globalization;
using System.Security.Claims;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.Director)]
    public class DirectorController : Controller
    {
        private readonly IDirectorMonitoringService _monitoringService;
        private readonly IDirectorIdeaService _ideaService;
        private readonly ApplicationDbContext _context;

        public DirectorController(
            IDirectorMonitoringService monitoringService,
            IDirectorIdeaService ideaService,
            ApplicationDbContext context)
        {
            _monitoringService = monitoringService;
            _ideaService = ideaService;
            _context = context;
        }
        public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
        {
            ViewBag.RunningCampaignsCount = await _context.Campaigns.CountAsync(c => c.Status == "running", cancellationToken);
            return View();
        }

        public async Task<IActionResult> Approve(CancellationToken cancellationToken)
        {
            var pendingBookings = await _context.Bookings
                .Include(b => b.Campaign)
                .Include(b => b.Kol)
                .Include(b => b.PrimaryManager)
                .Where(b => b.ContractStatus == "cho_duyet")
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync(cancellationToken);

            return View(pendingBookings);
        }

        public async Task<IActionResult> BookingCampaign(CancellationToken cancellationToken)
        {
            var bookings = await _context.Bookings
                .Include(b => b.Campaign)
                .Include(b => b.Kol)
                .Include(b => b.PrimaryManager)
                .Include(b => b.BookingWages)
                    .ThenInclude(bw => bw.Employee)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync(cancellationToken);

            return View(bookings);
        }

        public IActionResult Config()
        {
            return View();
        }

        public IActionResult Department()
        {
            return View();
        }

        public async Task<IActionResult> HumanResources(string? search, string? department, string? status, CancellationToken cancellationToken)
        {
            var normalizedSearch = search?.Trim();
            var query = _context.Employees.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                var pattern = $"%{normalizedSearch}%";
                query = query.Where(employee => EF.Functions.Like(employee.FullName, pattern) || EF.Functions.Like(employee.Position, pattern) || EF.Functions.Like(employee.Email, pattern));
            }
            if (!string.IsNullOrWhiteSpace(department)) query = query.Where(employee => employee.Department == department);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(employee => employee.Status == status);

            var departments = await _context.Departments.AsNoTracking()
                .Where(item => item.Status == "active").OrderBy(item => item.Name)
                .Select(item => new WorkTaskEmployeeDepartmentViewModel(item.Code, item.Name))
                .ToListAsync(cancellationToken);
            var departmentNames = departments.ToDictionary(item => item.Code, item => item.Name);
            var employees = await query.OrderBy(employee => employee.FullName).ToListAsync(cancellationToken);

            return View(new DirectorHumanResourcesViewModel
            {
                Search = normalizedSearch,
                Department = department,
                Status = status,
                Departments = departments,
                Employees = employees.Select(employee => new DirectorEmployeeListItemViewModel
                {
                    Id = employee.Id,
                    FullName = employee.FullName,
                    Department = employee.Department,
                    DepartmentName = departmentNames.GetValueOrDefault(employee.Department) ?? employee.Department,
                    Position = employee.Position,
                    JoinedDate = employee.JoinedDate,
                    SalaryAndAllowance = employee.BasicSalary + (employee.Allowance ?? 0),
                    Status = employee.Status ?? "ngung_hoat_dong",
                    StatusLabel = GetEmployeeStatusLabel(employee.Status),
                    HasAccount = employee.UserId.HasValue,
                    TaskModule = GetTaskModule(employee.Department)
                }).ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Idea(
            string? search,
            string? status,
            string? directorStatus,
            int page = 1,
            CancellationToken cancellationToken = default)
        {
            return View(await _ideaService.GetPageAsync(
                search, status, directorStatus, page, cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditIdea(
            DirectorEditIdeaInputModel input,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Challenge();
            var result = ModelState.IsValid
                ? await _ideaService.UpdateContentAsync(input, userId, cancellationToken)
                : IdeaOperationResult.Failure(GetModelErrors());
            SetIdeaMessage(result);
            return RedirectToAction(nameof(Idea), new { focus = input.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendIdeaFeedback(
            int id,
            string? feedback,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Challenge();
            var result = await _ideaService.SendFeedbackAsync(id, feedback, userId, cancellationToken);
            SetIdeaMessage(result);
            return RedirectToAction(nameof(Idea), new { focus = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DecideIdea(
            int id,
            string decision,
            string? reason,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Challenge();
            var result = await _ideaService.DecideAsync(id, decision, reason, userId, cancellationToken);
            SetIdeaMessage(result);
            return RedirectToAction(nameof(Idea), new { focus = id });
        }

        public async Task<IActionResult> MonitoringSystem(CancellationToken cancellationToken)
        {
            return View(await _monitoringService.GetAsync(cancellationToken));
        }

        public IActionResult Report()
        {
            return View();
        }

        public async Task<IActionResult> SignContract(CancellationToken cancellationToken)
        {
            var bookings = await _context.Bookings
                .Include(b => b.Campaign)
                .Include(b => b.Kol)
                .Include(b => b.PrimaryManager)
                .Include(b => b.ContractSignedBy)
                .Where(b => b.ContractStatus == "cho_ky" || b.ContractStatus == "da_ky" || b.ContractStatus == "tu_choi")
                .OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt)
                .ToListAsync(cancellationToken);

            return View(bookings);
        }

        private static string GetEmployeeStatusLabel(string? status) => status switch
        {
            "dang_lam_viec" => "Đang làm việc",
            "thu_viec" => "Thử việc",
            "cho_duyet_nghi" => "Chờ duyệt nghỉ",
            _ => "Ngừng hoạt động"
        };

        private static string? GetTaskModule(string department) => department switch
        {
            "HCNS" => WorkTaskModules.HumanResources,
            "Booking" => WorkTaskModules.Booking,
            "Y_tuong" => WorkTaskModules.Ideas,
            _ => null
        };

        private bool TryGetUserId(out int userId) => int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out userId);

        private string GetModelErrors() => string.Join(" ", ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? "Dữ liệu ý tưởng không hợp lệ."
                : error.ErrorMessage));

        private void SetIdeaMessage(IdeaOperationResult result) =>
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
    }
}
