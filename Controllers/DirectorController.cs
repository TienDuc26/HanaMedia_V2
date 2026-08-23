using HanaMedia.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HanaMedia.Services.Dashboard;
using HanaMedia.Services.Ideas;
using HanaMedia.Models;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.Director)]
    public class DirectorController : Controller
    {
    private readonly IDirectorMonitoringService _monitoringService;
    private readonly ApplicationDbContext _context;
    private readonly IIdeaService _ideaService;

    public DirectorController(IDirectorMonitoringService monitoringService, ApplicationDbContext context, IIdeaService ideaService)
    {
        _monitoringService = monitoringService;
        _context = context;
        _ideaService = ideaService;
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

        [Authorize(Roles = AppRoles.Director + "," + AppRoles.BookingManager + "," + AppRoles.BookingStaff)]
        public async Task<IActionResult> Idea(
            string? search, string? status, string? client, int page = 1,
            CancellationToken cancellationToken = default)
        {
            // View-only cho Giám đốc / QL Booking / NV Booking — dùng chung IdeaService (chỉ đọc).
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0", CultureInfo.InvariantCulture);
            int? employeeId = int.TryParse(User.FindFirstValue("employee_id"), NumberStyles.None,
                CultureInfo.InvariantCulture, out var empId) ? empId : null;
            var role = User.FindFirstValue(ClaimTypes.Role) ?? AppRoles.Director;
            var model = await _ideaService.GetPageAsync(
                userId, role, employeeId, search, status, client, page, cancellationToken);
            return View(model);
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
    }
}
