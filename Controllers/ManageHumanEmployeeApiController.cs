using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Models.Dto;
using HanaMedia.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.HumanResourcesManager + "," + AppRoles.HumanResourcesStaff)]
    [Route("ManageHuman/ApiEmployee")]
    public class ManageHumanEmployeeApiController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ManageHumanEmployeeApiController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly EmployeeAvatarService _avatarService;

        public ManageHumanEmployeeApiController(
            ApplicationDbContext db,
            ILogger<ManageHumanEmployeeApiController> logger,
            IWebHostEnvironment env,
            EmployeeAvatarService avatarService)
        {
            _db = db;
            _logger = logger;
            _env = env;
            _avatarService = avatarService;
        }

        // UI status values: dang_lam_viec | tam_ngung | da_nghi
        // DB chỉ chấp nhận: dang_lam_viec, thu_viec, cho_duyet_nghi, ngung_hoat_dong
        // -> ánh xạ: tam_ngung/da_nghi -> ngung_hoat_dong
        private static string MapStatusToDb(string? uiStatus) => (uiStatus ?? "dang_lam_viec") switch
        {
            "tam_ngung" => "ngung_hoat_dong",
            "da_nghi" => "ngung_hoat_dong",
            "thu_viec" => "thu_viec",
            _ => "dang_lam_viec"
        };

        private static readonly HashSet<string> ContractTypes = new() { "thu_viec", "chinh_thuc_1_nam", "vo_thoi_han" };

    // Phải trùng với CHECK constraint [chk_emp_dept] trên bảng employees
    private static readonly HashSet<string> DepartmentCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "HCNS", "Booking", "Y_tuong", "IT"
    };

        // GET: /ManageHuman/ApiEmployee/ApiDebug
        // Debug: xem schema thật trong database
        [HttpGet("ApiDebug")]
        [AllowAnonymous]
        public async Task<IActionResult> ApiDebug()
        {
            try
            {
                var deptsCols = new List<string>();
                var empsCols = new List<string>();

                var conn = _db.Database.GetDbConnection();
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'departments' ORDER BY ORDINAL_POSITION";
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                        deptsCols.Add(r.GetString(0) + " (" + r.GetString(1) + ")");
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'employees' ORDER BY ORDINAL_POSITION";
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                        empsCols.Add(r.GetString(0) + " (" + r.GetString(1) + ")");
                }
                await conn.CloseAsync();

                // Thử SELECT thật
                var deptsRaw = new List<object>();
                try
                {
                    var rows = await _db.Departments.AsNoTracking()
                        .Select(d => new { d.Id, d.Code, d.Name, d.Status })
                        .ToListAsync();
                    deptsRaw = rows.Cast<object>().ToList();
                }
                catch (Exception ex) { return Ok(new { success = false, departments = deptsCols, employees = empsCols, error = "Lỗi SELECT departments: " + ex.Message, stack = ex.StackTrace }); }

                // Test ApiDepartments (filter active)
                var deptsApi = new List<object>();
                try
                {
                    var rows = await _db.Departments.AsNoTracking()
                        .Where(d => d.Status == "active")
                        .OrderBy(d => d.Code)
                        .Select(d => new { d.Code, d.Name })
                        .ToListAsync();
                    deptsApi = rows.Cast<object>().ToList();
                }
                catch (Exception ex)
                {
                    return Ok(new { success = false, error = "Lỗi ApiDepartments query: " + ex.Message, stack = ex.StackTrace, deptsRaw });
                }

                return Ok(new { success = true, deptsRaw = deptsRaw, deptsApi = deptsApi });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message, stack = ex.StackTrace });
            }
        }


        private string GetUserRole()
        {
            return User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value
                ?? User.Claims.FirstOrDefault(c => c.Type == "role")?.Value
                ?? "";
        }

        private bool IsManager() => GetUserRole() == AppRoles.HumanResourcesManager;
        private bool IsStaff() => GetUserRole() == AppRoles.HumanResourcesStaff;
        private bool CanEdit() => IsManager() || IsStaff();
        private bool CanDelete() => IsManager();
        private bool CanChangeDeptPosStatus() => IsManager();

        private static readonly HashSet<string> UiStatuses = new() { "dang_lam_viec", "tam_ngung", "da_nghi" };

        // GET: /ManageHuman/ApiEmployee/ApiList
        [HttpGet("ApiList")]
        public async Task<IActionResult> ApiList(string? q, string? status, string? department)
        {
            var query = _db.Employees.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var kw = q.Trim().ToLower();
                query = query.Where(e =>
                    e.FullName.ToLower().Contains(kw) ||
                    e.Email.ToLower().Contains(kw) ||
                    e.Phone.Contains(kw) ||
                    e.Position.ToLower().Contains(kw));
            }

            if (!string.IsNullOrWhiteSpace(status) && UiStatuses.Contains(status))
            {
                // UI statuses: dang_lam_viec | tam_ngung | da_nghi
                // DB check-constraint values: dang_lam_viec | thu_viec | cho_duyet_nghi | ngung_hoat_dong
                // Trong DB:
                //   dang_lam_viec | thu_viec     -> đang hoạt động (hiển thị "Đang làm việc")
                //   cho_duyet_nghi                -> chờ duyệt nghỉ
                //   ngung_hoat_dong              -> đã nghỉ / tạm ngừng
                // Map UI -> DB:
                //   dang_lam_viec -> ngung_hoat_dong? không; UI này giữ nguyên
                //   tam_ngung    -> ngung_hoat_dong
                //   da_nghi      -> ngung_hoat_dong
                var dbStatus = status switch
                {
                    "tam_ngung" => "ngung_hoat_dong",
                    "da_nghi" => "ngung_hoat_dong",
                    _ => status
                };
                query = query.Where(e => e.Status == dbStatus);
            }

            if (!string.IsNullOrWhiteSpace(department))
                query = query.Where(e => e.Department == department);

            var employees = await query.OrderBy(e => e.FullName).ToListAsync();

            var depts = await _db.Departments.AsNoTracking()
                .Where(d => d.Status == "active")
                .ToDictionaryAsync(d => d.Code, d => d.Name);

            var managerIds = employees.Where(e => e.ManagerId.HasValue).Select(e => e.ManagerId!.Value).Distinct().ToList();
            var managers = await _db.Employees.AsNoTracking()
                .Where(e => managerIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.FullName);

            var result = employees.Select(e =>
            {
                depts.TryGetValue(e.Department, out var deptName);
                managers.TryGetValue(e.ManagerId ?? 0, out var managerName);
                return new
                {
                    e.Id,
                    e.FullName,
                    e.AvatarUrl,
                    Dob = e.Dob.ToString("yyyy-MM-dd"),
                    e.Phone,
                    e.Email,
                    e.Department,
                    DepartmentName = deptName ?? e.Department,
                    e.Position,
                    ManagerName = e.ManagerId.HasValue ? managerName : null,
                    e.ContractType,
                    e.Status,
                    JoinedDate = e.JoinedDate.ToString("yyyy-MM-dd"),
                    e.CreatedAt
                };
            }).ToList();

            return Ok(new { success = true, data = result, total = result.Count });
        }

        // GET: /ManageHuman/ApiEmployee/ApiGet/{id}
        [HttpGet("ApiGet/{id:int}")]
        public async Task<IActionResult> ApiGet(int id)
        {
            var e = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(emp => emp.Id == id);
            if (e == null)
                return NotFound(new { success = false, message = "Không tìm thấy nhân viên." });

            var deptName = await _db.Departments.AsNoTracking()
                .Where(d => d.Code == e.Department)
                .Select(d => d.Name)
                .FirstOrDefaultAsync();

            string? managerName = null;
            if (e.ManagerId.HasValue)
            {
                managerName = await _db.Employees.AsNoTracking()
                    .Where(m => m.Id == e.ManagerId)
                    .Select(m => m.FullName)
                    .FirstOrDefaultAsync();
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    e.Id,
                    e.FullName,
                    e.AvatarUrl,
                    Dob = e.Dob.ToString("yyyy-MM-dd"),
                    e.Phone,
                    e.Email,
                    e.Address,
                    e.Department,
                    DepartmentName = deptName ?? e.Department,
                    e.Position,
                    e.ManagerId,
                    e.IsManager,
                    ManagerName = managerName,
                    e.ContractType,
                    e.Status,
                    JoinedDate = e.JoinedDate.ToString("yyyy-MM-dd"),
                    e.CreatedAt
                }
            });
        }

        // GET: /ManageHuman/ApiEmployee/ApiDepartments
        [HttpGet("ApiDepartments")]
        public async Task<IActionResult> ApiDepartments()
        {
            var depts = await _db.Departments.AsNoTracking()
                .Where(d => d.Status == "active")
                .OrderBy(d => d.Code)
                .Select(d => new { d.Code, d.Name })
                .ToListAsync();

            return Ok(new { success = true, data = depts });
        }

        // GET: /ManageHuman/ApiEmployee/ApiManagers
        // Chỉ trả về nhân sự có IsManager = true (cờ cấu hình thủ công ở DB, không hard-code chuỗi "Quản lý" / "Trưởng phòng")
        // và đang trong trạng thái hoạt động. Có thể truyền ?excludeId= để loại trừ nhân sự đang sửa.
        [HttpGet("ApiManagers")]
        public async Task<IActionResult> ApiManagers([FromQuery] int? excludeId = null)
        {
            var query = _db.Employees.AsNoTracking()
                .Where(e => e.IsManager && e.Status == "dang_lam_viec");

            if (excludeId.HasValue && excludeId.Value > 0)
                query = query.Where(e => e.Id != excludeId.Value);

            var managers = await query
                .OrderBy(e => e.FullName)
                .Select(e => new { e.Id, e.FullName, e.Position, e.Department })
                .ToListAsync();

            return Ok(new { success = true, data = managers });
        }

        // POST: /ManageHuman/ApiEmployee/ApiCreate
        [HttpPost("ApiCreate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApiCreate([FromBody] EmployeeCreateUpdateDto input)
        {
            if (!CanEdit())
                return Forbid();

            if (input == null)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            var errors = await ValidateInputAsync(input, excludeEmployeeId: null);
            if (errors.Count > 0)
                return BadRequest(new { success = false, message = errors[0], errors });

            var emp = new Employee
            {
                FullName = input.FullName.Trim(),
                Dob = input.Dob,
                Phone = input.Phone.Trim(),
                Email = input.Email.Trim(),
                Address = input.Address.Trim(),
                JoinedDate = input.JoinedDate,
                Department = input.Department,
                Position = input.Position.Trim(),
                IsManager = input.IsManager,
                ManagerId = input.ManagerId.HasValue && input.ManagerId.Value > 0 ? input.ManagerId : null,
                ContractType = input.ContractType,
                BasicSalary = 0,
                Allowance = 0,
                Status = MapStatusToDb(input.Status),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Employees.Add(emp);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                var msg = ResolveDbError(dbEx, out var status, input);
                return StatusCode(status, new { success = false, message = msg });
            }

            // Upload avatar (nếu FE gửi). Phải làm sau SaveChangesAsync vì cần emp.Id.
            string? avatarError = null;
            if (!string.IsNullOrWhiteSpace(input.AvatarBase64))
            {
                try
                {
                    var avatarUrl = await _avatarService.SaveAsync(emp.Id, input.AvatarBase64);
                    if (avatarUrl != null)
                    {
                        emp.AvatarUrl = avatarUrl;
                        await _db.SaveChangesAsync();
                    }
                }
                catch (InvalidOperationException ex)
                {
                    avatarError = ex.Message;
                    _logger?.LogWarning("Avatar upload failed (create emp {Id}): {Msg}", emp.Id, ex.Message);
                }
            }

            return Ok(new
            {
                success = true,
                message = string.IsNullOrEmpty(avatarError) ? "Thêm nhân viên thành công." : $"Thêm nhân viên thành công, nhưng ảnh đại diện chưa lưu: {avatarError}",
                data = new { id = emp.Id, avatarUrl = emp.AvatarUrl }
            });
        }

        // POST: /ManageHuman/ApiEmployee/ApiUpdate/{id}
        // PUT: /ManageHuman/ApiEmployee/ApiUpdate/{id}
        [HttpPost("ApiUpdate/{id:int}")]
        [HttpPut("ApiUpdate/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApiUpdate(int id, [FromBody] EmployeeCreateUpdateDto input)
        {
            if (!CanEdit())
                return Forbid();

            if (input == null)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            var errors = await ValidateInputAsync(input, excludeEmployeeId: id);
            if (errors.Count > 0)
                return BadRequest(new { success = false, message = errors[0], errors });

            var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id);
            if (emp == null)
                return NotFound(new { success = false, message = "Không tìm thấy nhân viên." });

            // Không cho phép tự làm quản lý của chính mình
            if (input.ManagerId.HasValue && input.ManagerId.Value == id)
                return BadRequest(new { success = false, message = "Người quản lý không hợp lệ (không thể tự quản lý chính mình)." });

            emp.FullName = input.FullName.Trim();
            emp.Dob = input.Dob;
            emp.Phone = input.Phone.Trim();
            emp.Email = input.Email.Trim();
            emp.Address = input.Address.Trim();
            emp.JoinedDate = input.JoinedDate;
            emp.Department = input.Department;
            emp.Position = input.Position.Trim();
            emp.ManagerId = input.ManagerId.HasValue && input.ManagerId.Value > 0 ? input.ManagerId : null;
            emp.ContractType = input.ContractType;
            emp.UpdatedAt = DateTime.UtcNow;

            // Cờ IsManager chỉ ql_hcns (manager role) được đổi.
            // nv_hcns (staff) gửi lên giá trị nào cũng giữ nguyên giá trị cũ.
            if (CanChangeDeptPosStatus())
                emp.IsManager = input.IsManager;

            if (CanChangeDeptPosStatus())
            {
                if (!string.IsNullOrWhiteSpace(input.Status) && UiStatuses.Contains(input.Status))
                    emp.Status = MapStatusToDb(input.Status);
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                var msg = ResolveDbError(dbEx, out var status, input);
                return StatusCode(status, new { success = false, message = msg });
            }

            // Upload avatar (nếu FE gửi base64 mới)
            string? avatarError = null;
            if (!string.IsNullOrWhiteSpace(input.AvatarBase64))
            {
                try
                {
                    var avatarUrl = await _avatarService.SaveAsync(emp.Id, input.AvatarBase64);
                    if (avatarUrl != null)
                    {
                        emp.AvatarUrl = avatarUrl;
                        await _db.SaveChangesAsync();
                    }
                }
                catch (InvalidOperationException ex)
                {
                    avatarError = ex.Message;
                    _logger?.LogWarning("Avatar upload failed (update emp {Id}): {Msg}", emp.Id, ex.Message);
                }
            }

            return Ok(new
            {
                success = true,
                message = string.IsNullOrEmpty(avatarError) ? "Cập nhật nhân viên thành công." : $"Cập nhật thành công, nhưng ảnh đại diện chưa lưu: {avatarError}",
                data = new { avatarUrl = emp.AvatarUrl }
            });
        }

        // POST: /ManageHuman/ApiEmployee/ApiDelete/{id}
        [HttpPost("ApiDelete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApiDelete(int id)
        {
            if (!CanDelete())
                return Forbid();

            var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id);
            if (emp == null)
                return NotFound(new { success = false, message = "Không tìm thấy nhân viên." });

            if (emp.Status == "ngung_hoat_dong")
                return BadRequest(new { success = false, message = "Nhân viên đã ở trạng thái ngừng hoạt động." });

            // DB CHECK constraint chỉ chấp nhận:
            // 'dang_lam_viec', 'thu_viec', 'cho_duyet_nghi', 'ngung_hoat_dong'
            // -> dùng 'ngung_hoat_dong' thay vì 'da_nghi' để không vi phạm [chk_emp_status]
            emp.Status = "ngung_hoat_dong";
            emp.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                var msg = ResolveDbError(dbEx, out var status);
                return StatusCode(status, new { success = false, message = msg });
            }

            return Ok(new { success = true, message = "Đã ngừng hoạt động nhân viên." });
        }

        // ===== Validation helpers =====
        private static readonly Regex PhoneRegex = new(@"^[0-9+\-\s()]{8,20}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private const int MinWorkingAge = 16;
        private const int MaxWorkingAge = 70;

        // Validation tổng hợp cho cả Create và Update.
        // - excludeEmployeeId: id nhân viên đang sửa (để loại trừ khi check trùng email/phone); null = đang tạo mới.
        private async Task<List<string>> ValidateInputAsync(EmployeeCreateUpdateDto input, int? excludeEmployeeId)
        {
            var errors = new List<string>();
            var today = DateOnly.FromDateTime(DateTime.Today);

            // ----- Họ tên -----
            if (string.IsNullOrWhiteSpace(input.FullName))
                errors.Add("Họ tên là bắt buộc.");
            else if (input.FullName.Trim().Length > 100)
                errors.Add("Họ tên tối đa 100 ký tự.");

            // ----- Ngày sinh -----
            if (input.Dob == default)
                errors.Add("Ngày sinh là bắt buộc.");
            else
            {
                if (input.Dob > today)
                    errors.Add("Ngày sinh không thể là ngày trong tương lai.");
                else
                {
                    var age = today.Year - input.Dob.Year - (today < input.Dob.AddYears(today.Year - input.Dob.Year) ? 1 : 0);
                    if (age < MinWorkingAge)
                        errors.Add($"Nhân viên phải từ {MinWorkingAge} tuổi trở lên (hiện {age} tuổi).");
                    if (age > MaxWorkingAge)
                        errors.Add($"Ngày sinh không hợp lệ (tuổi vượt quá {MaxWorkingAge}).");
                }
            }

            // ----- Ngày vào công ty -----
            if (input.JoinedDate == default)
                errors.Add("Ngày vào công ty là bắt buộc.");
            else
            {
                if (input.JoinedDate > today)
                    errors.Add("Ngày vào công ty không thể là ngày trong tương lai.");
                if (input.Dob != default && input.JoinedDate < input.Dob.AddYears(MinWorkingAge))
                    errors.Add("Ngày vào công ty không hợp lệ so với ngày sinh.");
            }

            // ----- Số điện thoại -----
            // Lưu ý: bảng employees KHÔNG có unique index trên phone, nên không kiểm tra trùng ở app.
            // Nếu sau này muốn enforce, phải thêm unique index qua migration trước rồi mới bật check này.
            if (string.IsNullOrWhiteSpace(input.Phone))
                errors.Add("Số điện thoại là bắt buộc.");
            else if (!PhoneRegex.IsMatch(input.Phone.Trim()))
                errors.Add("Số điện thoại không hợp lệ (chỉ gồm chữ số, '+', '-', khoảng trắng, dấu ngoặc; độ dài 8–20).");

            // ----- Email -----
            if (string.IsNullOrWhiteSpace(input.Email))
                errors.Add("Email là bắt buộc.");
            else if (!EmailRegex.IsMatch(input.Email.Trim()))
                errors.Add("Email không hợp lệ.");
            else
            {
                var emailLower = input.Email.Trim().ToLower();
                var dupEmail = await _db.Employees.AsNoTracking()
                    .AnyAsync(e => e.Email.ToLower() == emailLower && (!excludeEmployeeId.HasValue || e.Id != excludeEmployeeId.Value));
                if (dupEmail)
                    errors.Add("Email đã được sử dụng bởi nhân viên khác.");
            }

            // ----- Địa chỉ -----
            if (string.IsNullOrWhiteSpace(input.Address))
                errors.Add("Địa chỉ là bắt buộc.");
            else if (input.Address.Trim().Length > 255)
                errors.Add("Địa chỉ tối đa 255 ký tự.");

            // ----- Phòng ban -----
            if (string.IsNullOrWhiteSpace(input.Department))
                errors.Add("Phòng ban là bắt buộc.");
            else
            {
                if (!DepartmentCodes.Contains(input.Department))
                    errors.Add("Mã phòng ban không hợp lệ (chỉ chấp nhận: HCNS, Booking, Y_tuong, IT).");
                else
                {
                    var deptActive = await _db.Departments.AsNoTracking()
                        .AnyAsync(d => d.Code == input.Department && d.Status == "active");
                    if (!deptActive)
                        errors.Add("Phòng ban không tồn tại hoặc đã ngừng hoạt động.");
                }
            }

            // ----- Chức vụ -----
            if (string.IsNullOrWhiteSpace(input.Position))
                errors.Add("Chức vụ là bắt buộc.");
            else if (input.Position.Trim().Length > 100)
                errors.Add("Chức vụ tối đa 100 ký tự.");

            // ----- Loại hợp đồng -----
            if (string.IsNullOrWhiteSpace(input.ContractType) || !ContractTypes.Contains(input.ContractType))
                errors.Add("Loại hợp đồng không hợp lệ (chỉ chấp nhận: thu_viec, chinh_thuc_1_nam, vo_thoi_han).");

            // ----- Người quản lý -----
            if (input.ManagerId.HasValue && input.ManagerId.Value > 0)
            {
                if (excludeEmployeeId.HasValue && input.ManagerId.Value == excludeEmployeeId.Value)
                    errors.Add("Người quản lý không hợp lệ (không thể tự quản lý chính mình).");
                else
                {
                    // Kiểm tra Manager tồn tại VÀ có IsManager = true VÀ đang hoạt động.
                    // Đây là defense in-depth: FE phải populate dropdown từ ApiManagers
                    // (đã filter sẵn ở backend), nhưng nếu client gọi API trực tiếp
                    // với ManagerId của một nhân viên thường, server vẫn từ chối.
                    var manager = await _db.Employees.AsNoTracking()
                        .Where(m => m.Id == input.ManagerId.Value)
                        .Select(m => new { m.Id, m.IsManager, m.Status, m.FullName, m.Position })
                        .FirstOrDefaultAsync();

                    if (manager == null)
                        errors.Add("Người quản lý không tồn tại.");
                    else if (!manager.IsManager)
                        errors.Add($"Người được chọn không có chức vụ quản lý nên không thể làm người quản lý trực tiếp (hiện tại: \"{manager.Position}\").");
                    else if (manager.Status != "dang_lam_viec")
                        errors.Add($"Người quản lý \"{manager.FullName}\" hiện không đang làm việc, không thể chọn làm quản lý trực tiếp.");

                    // Chống vòng lặp chuỗi quản lý: nếu ManagerId đang sửa trỏ về một người
                    // mà về phía trên chuỗi lại trỏ tới excludeEmployeeId thì tạo vòng lặp.
                    if (excludeEmployeeId.HasValue && manager != null)
                    {
                        var hasCycle = await HasManagerCycleAsync(input.ManagerId.Value, excludeEmployeeId.Value);
                        if (hasCycle)
                            errors.Add("Thiết lập quản lý này sẽ tạo vòng lặp chuỗi quản lý, vui lòng chọn người khác.");
                    }
                }
            }

            return errors;
        }

        // ===== DB error resolver =====
        // Chuyển lỗi EF Core / SQL Server sang thông điệp tiếng Việt thân thiện với người dùng.
        private string ResolveDbError(DbUpdateException ex, out int statusCode, EmployeeCreateUpdateDto? input = null)
        {
            statusCode = StatusCodes.Status400BadRequest;
            var inner = ex.InnerException;
            var msg = inner?.Message ?? ex.Message;

            // Log toàn bộ raw error để debug — EF trả message khó đọc
            _logger?.LogWarning("DbUpdateException (employees): {Message}", msg);

            // Trích tên constraint từ SQL Server message, ví dụ:
            //   Violation of UNIQUE KEY constraint 'UQ__employee__6B19F4B6BF4CC83C'. ...
            //   Cannot insert duplicate key row in object 'dbo.employees' with unique index 'UX_employees_email'.
            var constraintName = ExtractUniqueName(msg);

            // Nếu biết tên, tra cứu cột mà nó nằm trên để trả message chính xác
            if (!string.IsNullOrEmpty(constraintName))
            {
                var column = ResolveConstraintColumn(constraintName);
                _logger?.LogWarning("Unique violation on constraint {Constraint}, column={Column}, input email={Email}, phone={Phone}",
                    constraintName, column ?? "(unknown)", input?.Email, input?.Phone);

                if (!string.IsNullOrEmpty(column))
                {
                    var cn = column.ToLower();
                    if (cn.Contains("email") && input != null)
                    {
                        var existing = FindEmployeeByEmail(input.Email.Trim());
                        if (existing != null)
                            return $"Email đã được sử dụng bởi nhân viên \"{existing.FullName}\" (Mã NV: {existing.Id}).";
                        return "Email đã được sử dụng bởi nhân viên khác.";
                    }
                    if (cn.Contains("phone") && input != null)
                    {
                        var existing = FindEmployeeByPhone(input.Phone.Trim());
                        if (existing != null)
                            return $"Số điện thoại đã được sử dụng bởi nhân viên \"{existing.FullName}\" (Mã NV: {existing.Id}).";
                        return "Số điện thoại đã được sử dụng bởi nhân viên khác.";
                    }
                    if (cn.Contains("user_id"))
                        return "Tài khoản đăng nhập đã được gắn với nhân viên khác.";
                }
            }

            // Fallback theo message (giữ để tương thích ngược)
            if (Regex.IsMatch(msg, @"UX_employees_email", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg, @"UQ__employee.*email", RegexOptions.IgnoreCase))
            {
                return "Email đã được sử dụng bởi nhân viên khác.";
            }

            if (Regex.IsMatch(msg, @"UX_employees_user_id", RegexOptions.IgnoreCase))
            {
                return "Tài khoản đăng nhập đã được gắn với nhân viên khác.";
            }

            if (Regex.IsMatch(msg, @"IX_employees_manager_id", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(msg, @"FK_employees_employees_manager_id", RegexOptions.IgnoreCase))
            {
                return "Người quản lý không hợp lệ.";
            }

            // Nhánh chung cuối: báo cho người dùng biết đúng nội dung lỗi
            if (msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrEmpty(constraintName)
                    ? $"Dữ liệu bị trùng theo ràng buộc '{constraintName}'. Vui lòng kiểm tra email / số điện thoại / tài khoản."
                    : "Dữ liệu bị trùng với một bản ghi đã tồn tại (email, số điện thoại hoặc tài khoản).";
            }

            if (msg.Contains("CHECK constraint", StringComparison.OrdinalIgnoreCase))
            {
                return "Dữ liệu không thỏa mãn ràng buộc (phòng ban / loại hợp đồng / trạng thái không hợp lệ).";
            }

            if (msg.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
            {
                return "Tham chiếu không hợp lệ (phòng ban hoặc người quản lý không tồn tại).";
            }

            statusCode = StatusCodes.Status400BadRequest;
            return "Không thể lưu dữ liệu. Vui lòng kiểm tra lại thông tin và thử lại.";
        }

        // Trích tên unique constraint/index từ SQL Server error message.
        // Các biến thể SQL Server hay gặp:
        //   Violation of UNIQUE KEY constraint 'XXX'. ...
        //   ... with unique index 'XXX'. ...
        private static string? ExtractUniqueName(string msg)
        {
            var m = Regex.Match(msg, @"unique\s+(?:key\s+)?constraint\s+'([^']+)'", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(msg, @"unique\s+index\s+'([^']+)'", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(msg, @"duplicate key\s+row[^']*unique index\s+'([^']+)'", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;
            return null;
        }

        // Tra cứu cột mà constraint/index nằm trên, dựa trên sys.indexes + sys.index_columns.
        // Trả về tên cột nếu tìm thấy; null nếu không có (vd constraint ở bảng khác).
        private string? ResolveConstraintColumn(string constraintName)
        {
            try
            {
                var sql = @"
                    SELECT TOP(1) c.name AS ColumnName
                    FROM sys.indexes i
                    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                    WHERE i.name = @name";
                var conn = _db.Database.GetDbConnection();
                var wasOpen = conn.State == System.Data.ConnectionState.Open;
                if (!wasOpen) conn.Open();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@name";
                    p.Value = constraintName;
                    cmd.Parameters.Add(p);
                    var result = cmd.ExecuteScalar() as string;
                    return result;
                }
                finally
                {
                    if (!wasOpen) conn.Close();
                }
            }
            catch
            {
                return null;
            }
        }

        // Duyệt chuỗi manager của proposedManagerId lên trên (tối đa 16 tầng).
        // Trả về true nếu gặp subjectId trong chuỗi → tạo vòng lặp.
        // Ví dụ:
        //   A.ManagerId = B, B.ManagerId = C, C.ManagerId = A  → cycle
        //   proposedManagerId = C, subjectId = A               → cycle
        private async Task<bool> HasManagerCycleAsync(int proposedManagerId, int subjectId)
        {
            var visited = new HashSet<int>();
            var currentId = proposedManagerId;
            // Giới hạn 16 tầng để chặn chuỗi vô tận trong data bẩn
            for (int i = 0; i < 16; i++)
            {
                if (currentId == subjectId) return true;
                if (!visited.Add(currentId)) return false; // đã lặp mà chưa gặp subject → an toàn

                var nextId = await _db.Employees.AsNoTracking()
                    .Where(e => e.Id == currentId)
                    .Select(e => e.ManagerId)
                    .FirstOrDefaultAsync();

                if (nextId == null) return false;
                currentId = nextId.Value;
            }
            return false;
        }
        // So sánh case-insensitive để khớp với DB (SQL Server collation mặc định CI).
        private Employee? FindEmployeeByEmail(string email)
        {
            try
            {
                var lower = email.ToLower();
                return _db.Employees.AsNoTracking()
                    .Where(e => e.Email.ToLower() == lower)
                    .Select(e => new Employee { Id = e.Id, FullName = e.FullName })
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        // Tra cứu nhân viên đang giữ số điện thoại đó.
        private Employee? FindEmployeeByPhone(string phone)
        {
            try
            {
                return _db.Employees.AsNoTracking()
                    .Where(e => e.Phone == phone)
                    .Select(e => new Employee { Id = e.Id, FullName = e.FullName })
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }
    }
}
