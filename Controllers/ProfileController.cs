using System;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Models.Dto;
using HanaMedia.Services;
using HanaMedia.Services.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace HanaMedia.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IProfileService _profileService;
        private readonly ApplicationDbContext _db;
        private readonly UserMediaService _userMedia;

        public ProfileController(
            IProfileService profileService,
            ApplicationDbContext db,
            UserMediaService userMedia)
        {
            _profileService = profileService;
            _db = db;
            _userMedia = userMedia;
        }

        // GET /Profile — hồ sơ của chính user đang đăng nhập
        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var employee = await _db.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.UserId == userId, ct);

            ProfileViewDto profile;

            if (employee != null)
            {
                var p = await _profileService.GetProfileForEmployeeAsync(employee.Id, ct);
                if (p == null)
                    return RedirectToAction("AccessDenied", "Account");
                profile = p;
            }
            else
            {
                // Tài khoản chưa có Employee record — vẫn cho xem hồ sơ cơ bản
                profile = new ProfileViewDto
                {
                    EmployeeId = 0,
                    FullName = user.Username,
                    Email = user.Email,
                    AvatarUrl = user.AvatarUrl,
                    QrCodeUrl = user.QrCodeUrl,
                    StatusLabel = user.Status,
                    ContractTypeLabel = "",
                    DepartmentName = "",
                    Position = user.Role
                };
            }

            // Mọi user đều có quyền sửa thông tin cá nhân + bank account của chính mình
            profile.CanEditPersonalInfo = true;
            profile.CanEditBankAccount = true;
            profile.CanViewBankAccount = true;

            return View(profile);
        }

        // GET /Profile/Employee/{id} — Giám đốc & HCNS xem hồ sơ bất kỳ nhân viên nào
        [HttpGet("Profile/Employee/{employeeId}")]
        public async Task<IActionResult> ViewEmployee(int employeeId, CancellationToken ct = default)
        {
            if (!CanViewEmployeeProfile(employeeId))
                return Forbid();

            var profile = await _profileService.GetProfileForEmployeeAsync(employeeId, ct);
            if (profile == null)
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ nhân viên." });

            var currentUserId = GetCurrentUserId();
            var currentEmployee = currentUserId != null
                ? await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == currentUserId, ct)
                : null;

            bool isOwner = currentEmployee?.Id == employeeId;

            if (!CanViewBankAccount() && !isOwner)
                profile.BankAccount = null;

            profile.CanEditPersonalInfo = false;
            profile.CanEditBankAccount = false;
            profile.CanViewBankAccount = CanViewBankAccount() || isOwner;

            return View("Index", profile);
        }

        // GET /Profile/ByEmployee/{id} — API JSON
        [HttpGet("Profile/ByEmployee/{employeeId}")]
        public async Task<IActionResult> GetByEmployee(int employeeId, CancellationToken ct = default)
        {
            if (!CanViewEmployeeProfile(employeeId))
                return Forbid();

            var profile = await _profileService.GetProfileForEmployeeAsync(employeeId, ct);
            if (profile == null)
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ nhân viên." });

            var currentUserId = GetCurrentUserId();
            var currentEmployee = currentUserId != null
                ? await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == currentUserId, ct)
                : null;

            bool isOwner = currentEmployee?.Id == employeeId;

            if (!CanViewBankAccount() && !isOwner)
                profile.BankAccount = null;

            profile.CanEditPersonalInfo = false;
            profile.CanEditBankAccount = false;
            profile.CanViewBankAccount = CanViewBankAccount() || isOwner;

            return Json(new { success = true, data = profile });
        }

        // PUT /Profile/UpdatePersonalInfo — cập nhật thông tin cá nhân (chính chủ)
        [HttpPut("Profile/UpdatePersonalInfo")]
        public async Task<IActionResult> UpdatePersonalInfo(
            [FromBody] EmployeePersonalInfoUpdateDto input,
            CancellationToken ct = default)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId, ct);

            if (employee == null)
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ nhân viên." });

            if (!string.IsNullOrWhiteSpace(input.FullName))
            {
                var trimmed = input.FullName.Trim();
                if (trimmed.Length < 2 || trimmed.Length > 100)
                    return BadRequest(new { success = false, message = "Họ và tên phải từ 2 đến 100 ký tự." });
            }

            if (!string.IsNullOrWhiteSpace(input.Phone))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(input.Phone, @"^[\d\s\+\-\.]{6,20}$"))
                    return BadRequest(new { success = false, message = "Số điện thoại không hợp lệ." });
            }

            // Email: validate + duplicate check (cùng chỗ với update thông tin cá nhân)
            string? normalizedEmail = null;
            if (!string.IsNullOrWhiteSpace(input.Email))
            {
                var trimmed = input.Email.Trim();
                if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(trimmed))
                    return BadRequest(new { success = false, message = "Email không hợp lệ." });

                var currentUsersEmail = (await _db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync(ct)) ?? "";

                if (!string.Equals(trimmed, currentUsersEmail, StringComparison.OrdinalIgnoreCase))
                {
                    var emailTaken = await _db.Users.AsNoTracking()
                        .AnyAsync(u => u.Id != userId
                            && u.Email != null
                            && u.Email.ToLower() == trimmed.ToLower(), ct);
                    if (emailTaken)
                        return Conflict(new { success = false, message = "Email này đã được sử dụng. Vui lòng chọn email khác." });
                    normalizedEmail = trimmed;
                }
            }

            if (input.Dob.HasValue && input.Dob.Value > DateOnly.FromDateTime(DateTime.Today))
                return BadRequest(new { success = false, message = "Ngày sinh không được lớn hơn ngày hiện tại." });

            try
            {
                var ok = await _profileService.UpdatePersonalInfoAsync(employee.Id, input, normalizedEmail, ct);
                if (!ok)
                    return BadRequest(new { success = false, message = "Cập nhật thất bại." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (DbUpdateException ex) when (IsDuplicateEmailViolation(ex))
            {
                return Conflict(new { success = false, message = "Email này đã được sử dụng. Vui lòng chọn email khác." });
            }
            catch (DbUpdateException ex)
            {
                var logger = HttpContext.RequestServices.GetService(typeof(ILogger<ProfileController>)) as ILogger<ProfileController>;
                logger?.LogError(ex, "UpdatePersonalInfo SaveChanges failed for userId={UserId}", userId);
                return StatusCode(500, new { success = false, message = "Không thể lưu thông tin lúc này. Vui lòng thử lại." });
            }

            return Json(new { success = true, message = "Cập nhật thông tin cá nhân thành công." });
        }

        // PUT /Profile/UpdateBankAccount — cập nhật tài khoản nhận lương (chính chủ)
        [HttpPut("Profile/UpdateBankAccount")]
        public async Task<IActionResult> UpdateBankAccount(
            [FromBody] EmployeeBankAccountUpdateDto input,
            CancellationToken ct = default)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId, ct);

            if (employee == null)
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ nhân viên." });

            if (string.IsNullOrWhiteSpace(input.BankName))
                return BadRequest(new { success = false, message = "Tên ngân hàng không được để trống." });
            if (string.IsNullOrWhiteSpace(input.AccountNumber))
                return BadRequest(new { success = false, message = "Số tài khoản không được để trống." });
            if (string.IsNullOrWhiteSpace(input.AccountHolderName))
                return BadRequest(new { success = false, message = "Tên chủ tài khoản không được để trống." });
            if (input.AccountNumber.Length > 50)
                return BadRequest(new { success = false, message = "Số tài khoản quá dài." });

            var ok = await _profileService.UpdateBankAccountAsync(employee.Id, input, ct);
            if (!ok)
                return BadRequest(new { success = false, message = "Cập nhật thất bại." });

            return Json(new { success = true, message = "Cập nhật tài khoản nhận lương thành công." });
        }

        // PUT /Profile/UpdateUser — cập nhật avatar, QR code (chính chủ, không cần Employee record)
        // Email được UpdatePersonalInfo xử lý riêng để check trùng trước SaveChanges.
        [HttpPut("Profile/UpdateUser")]
        public async Task<IActionResult> UpdateUser(
            [FromBody] UserUpdateDto input,
            CancellationToken ct = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var swStep = new System.Diagnostics.Stopwatch();

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

            swStep.Restart();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
                return NotFound(new { success = false, message = "Không tìm thấy tài khoản." });

            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == userId, ct);
            swStep.Stop();
            LogTiming("LoadUser", swStep.ElapsedMilliseconds);

            // ---- Avatar: chỉ xử lý nếu user upload file mới ----
            if (!string.IsNullOrWhiteSpace(input.AvatarBase64))
            {
                swStep.Restart();
                try
                {
                    var url = await _userMedia.SaveAvatarAsync(userId!.Value, input.AvatarBase64);
                    swStep.Stop();
                    LogTiming("AvatarProcess", swStep.ElapsedMilliseconds);

                    if (url != null)
                    {
                        user.AvatarUrl = url;
                        if (employee != null)
                        {
                            employee.AvatarUrl = url;
                            employee.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
                catch (InvalidOperationException ex)
                {
                    return BadRequest(new { success = false, message = ex.Message });
                }
            }

            // ---- QR: chỉ xử lý nếu user upload file mới ----
            if (!string.IsNullOrWhiteSpace(input.QrCodeBase64))
            {
                swStep.Restart();
                try
                {
                    var url = await _userMedia.SaveQrCodeAsync(userId!.Value, input.QrCodeBase64);
                    swStep.Stop();
                    LogTiming("QrProcess", swStep.ElapsedMilliseconds);

                    if (url != null)
                        user.QrCodeUrl = url;
                }
                catch (InvalidOperationException ex)
                {
                    return BadRequest(new { success = false, message = ex.Message });
                }
            }

            user.UpdatedAt = DateTime.UtcNow;

            // EF sẽ tự phát hiện không có change nếu user không upload gì
            await _db.SaveChangesAsync(ct);
            LogTiming("SaveChanges", swStep.ElapsedMilliseconds);

            sw.Stop();
            LogTiming("Total", sw.ElapsedMilliseconds);

            return Json(new { success = true, message = "Cập nhật tài khoản thành công." });
        }

        private void LogTiming(string step, long ms)
        {
            if (HttpContext.RequestServices.GetService(typeof(IWebHostEnvironment)) is not IWebHostEnvironment env) return;
            if (!env.IsDevelopment()) return;
            var logger = HttpContext.RequestServices.GetService(typeof(ILogger<ProfileController>)) as ILogger<ProfileController>;
            logger?.LogDebug("[ProfileUpdate] {Step}: {Ms}ms", step, ms);
        }

        private static bool IsDuplicateEmailViolation(DbUpdateException ex)
        {
            // SQL Server: 2627 (unique constraint), 2601 (duplicate key)
            // Index name "UX_users_email" trên table "users"
            var inner = ex.InnerException;
            while (inner != null)
            {
                if (inner is SqlException sqlEx)
                {
                    if (sqlEx.Number == 2627 || sqlEx.Number == 2601)
                    {
                        var msg = sqlEx.Message ?? "";
                        if (msg.IndexOf("users", StringComparison.OrdinalIgnoreCase) >= 0
                            || msg.IndexOf("UX_users_email", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }
                inner = inner.InnerException;
            }
            return false;
        }

        private int? GetCurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idStr, out var id) ? id : null;
        }

        private bool CanViewBankAccount()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            return role == AppRoles.Director ||
                   role == AppRoles.HumanResourcesManager ||
                   role == AppRoles.HumanResourcesStaff;
        }

        private bool CanViewEmployeeProfile(int employeeId)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return false;

            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";

            if (role == AppRoles.Director ||
                role == AppRoles.HumanResourcesManager ||
                role == AppRoles.HumanResourcesStaff)
                return true;

            var currentEmployee = _db.Employees
                .AsNoTracking()
                .FirstOrDefault(e => e.UserId == userId);
            return currentEmployee?.Id == employeeId;
        }
    }
}
