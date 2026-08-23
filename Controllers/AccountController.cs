using System.Globalization;
using System.Net;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services;
using HanaMedia.Services.Auditing;
using HanaMedia.Services.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Controllers;

public sealed class AccountController : Controller
{
    private const string InvalidCredentialsMessage =
        "Tên đăng nhập hoặc mật khẩu không chính xác.";

    private readonly ApplicationDbContext _context;
    private readonly AccountService _accountService;
    private readonly IAccountPasswordService _passwordService;
    private readonly IConfiguration _configuration;
    private readonly ISystemAuditService _auditService;
    private readonly ILogger<AccountController> _logger;
    private readonly IWebHostEnvironment _environment;

    public AccountController(
        ApplicationDbContext context,
        AccountService accountService,
        IAccountPasswordService passwordService,
        IConfiguration configuration,
        ISystemAuditService auditService,
        ILogger<AccountController> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _accountService = accountService;
        _passwordService = passwordService;
        _configuration = configuration;
        _auditService = auditService;
        _logger = logger;
        _environment = environment;
    }

    [AllowAnonymous]
    [Route("")]
    [Route("Login")]
    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDashboard(User.FindFirstValue(ClaimTypes.Role));
        }

        return View();
    }

    [AllowAnonymous]
    [Route("Login")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        username = username?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            ViewBag.Error = "Tên đăng nhập và mật khẩu không được để trống.";
            return View();
        }

        var clientIp = GetClientIpAddress();
        var allowedCidrConfig = _configuration["AllowedNetworkCidr"] ?? "192.168.110.0/24";
        var allowedCidrs = allowedCidrConfig
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _logger.LogInformation(
            "[NetworkCheck] Client IP: {ClientIp}, Allowed CIDRs: {AllowedCidrs}",
            clientIp,
            string.Join(", ", allowedCidrs));

        var inAnyRange = allowedCidrs.Any(cidr => IsIpInRange(clientIp, cidr));
        if (!inAnyRange)
        {
            _logger.LogWarning(
                "[NetworkCheck] BLOCKED - IP {ClientIp} not in any of ranges {AllowedCidrs}",
                clientIp,
                string.Join(", ", allowedCidrs));
            await TryWriteAuditAsync(
                null,
                AuditActions.LoginBlockedNetwork,
                $"Chặn đăng nhập từ IP ngoài mạng nội bộ: {Truncate(clientIp, 45)}.",
                cancellationToken);
            ViewBag.NetworkError =
                "Vui lòng kết nối mạng nội bộ công ty để sử dụng hệ thống.";
            return View();
        }

        _logger.LogInformation(
            "[NetworkCheck] PASSED - IP {ClientIp} is in allowed ranges",
            clientIp);

        var (result, user, message) =
            await _accountService.AuthenticateAsync(username, password);

        switch (result)
        {
            case LoginResult.Success:
                await TryWriteAuditAsync(
                    user!.Id,
                    AuditActions.LoginSucceeded,
                    $"Tài khoản {user.Username} đăng nhập thành công.",
                    cancellationToken);

                var employee = await _context.Employees.AsNoTracking()
                    .FirstOrDefaultAsync(e => e.UserId == user.Id, cancellationToken);

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
                    new(ClaimTypes.Name, user.Username),
                    new(ClaimTypes.Email, user.Email),
                    new(ClaimTypes.Role, user.Role),
                    new(SecurityClaimTypes.SecurityStamp, user.SecurityStamp)
                };
                if (employee is not null)
                {
                    claims.Add(new Claim("employee_id", employee.Id.ToString(CultureInfo.InvariantCulture)));
                }
                var claimsIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2),
                    AllowRefresh = true
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);
                return RedirectToDashboard(user.Role);

            case LoginResult.AccountLockedTemporarily:
                await TryWriteAuditAsync(
                    user?.Id,
                    AuditActions.LoginFailed,
                    $"Tài khoản {Truncate(username, 100)} đang bị khóa tạm thời.",
                    cancellationToken);
                ViewBag.LockoutError = message;
                return View();

            case LoginResult.AccountInactive:
                await TryWriteAuditAsync(
                    user?.Id,
                    AuditActions.LoginFailed,
                    $"Tài khoản {Truncate(username, 100)} đang bị khóa.",
                    cancellationToken);
                ViewBag.Error = message;
                return View();

            case LoginResult.UserNotFound:
            case LoginResult.WrongPassword:
            default:
                await TryWriteAuditAsync(
                    user?.Id,
                    AuditActions.LoginFailed,
                    $"Đăng nhập thất bại với định danh {Truncate(username, 100)}.",
                    cancellationToken);
                ViewBag.Error = string.IsNullOrWhiteSpace(message)
                    ? InvalidCredentialsMessage
                    : message;
                return View();
        }
    }

    [Authorize]
    [Route("Logout")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Logout(CancellationToken cancellationToken)
        => SignOutCurrentUserAsync(cancellationToken);

    /// <summary>
    /// Endpoint CHỈ DÀNH CHO MÔI TRƯỜNG DEVELOPMENT phục vụ test tự động (Module 13 QA).
    /// Reset mật khẩu của một user bất kỳ thành một giá trị cho trước — KHÔNG dùng ở production.
    /// Bảo vệ bằng cờ config <c>Dev:EnablePasswordResetEndpoint=true</c>.
    /// </summary>
    [AllowAnonymous]
    [Route("Dev/ResetPassword")]
    [HttpPost]
    public async Task<IActionResult> DevResetPassword(
        [FromBody] DevResetPasswordRequest input,
        CancellationToken cancellationToken)
    {
        var rawValue = _configuration["dev:enablePasswordResetEndpoint"];
        var trimmedValue = rawValue?.Trim();
        var enabled = string.Equals(
            trimmedValue,
            "true",
            StringComparison.OrdinalIgnoreCase);
        _logger.LogWarning("[DevResetPassword] rawValue='{Raw}' (trimmed='{Trimmed}'), enabled={Enabled}, env={Env}",
            rawValue ?? "<null>", trimmedValue ?? "<null>", enabled, Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
        if (!enabled)
        {
            return NotFound(new { success = false, message = "Endpoint không khả dụng." });
        }

        if (input is null
            || string.IsNullOrWhiteSpace(input.Username)
            || string.IsNullOrWhiteSpace(input.NewPassword))
        {
            return BadRequest(new { success = false, message = "Thiếu username hoặc newPassword." });
        }

        var username = input.Username.Trim();
        var user = await _context.Users.FirstOrDefaultAsync(
            u => u.Username == username, cancellationToken);
        if (user is null)
        {
            return NotFound(new { success = false, message = $"Không tìm thấy user '{username}'." });
        }

        user.PasswordHash = _passwordService.HashPassword(user, input.NewPassword);
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "[DevResetPassword] Reset password for user '{Username}' (id={UserId}) to '{NewPassword}'.",
            username, user.Id, input.NewPassword);

        return Ok(new
        {
            success = true,
            userId = user.Id,
            username = user.Username,
            role = user.Role
        });
    }

    public sealed class DevResetPasswordRequest
    {
        public string? Username { get; set; }
        public string? NewPassword { get; set; }
    }

    // Giữ tương thích với các view cũ của develop đang dùng liên kết GET /Logout.
    [Authorize]
    [Route("Logout")]
    [HttpGet]
    public Task<IActionResult> LogoutLegacy(CancellationToken cancellationToken)
        => SignOutCurrentUserAsync(cancellationToken);

    [AllowAnonymous]
    [Route("AccessDenied")]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    // POST: /Account/ChangePassword
    // Body: { currentPassword, newPassword, confirmPassword }
    [Authorize]
    [Route("ChangePassword")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest? input,
        CancellationToken cancellationToken)
    {
        if (input == null
            || string.IsNullOrEmpty(input.CurrentPassword)
            || string.IsNullOrEmpty(input.NewPassword)
            || string.IsNullOrEmpty(input.ConfirmPassword))
        {
            return BadRequest(new
            {
                success = false,
                message = "Vui lòng nhập đầy đủ cả 3 trường."
            });
        }

        if (input.NewPassword.Length < 8)
        {
            return BadRequest(new
            {
                success = false,
                message = "Mật khẩu mới phải có ít nhất 8 ký tự."
            });
        }

        if (input.NewPassword != input.ConfirmPassword)
        {
            return BadRequest(new
            {
                success = false,
                message = "Mật khẩu mới và Xác nhận mật khẩu không khớp nhau."
            });
        }

        if (input.NewPassword == input.CurrentPassword)
        {
            return BadRequest(new
            {
                success = false,
                message = "Mật khẩu mới phải khác mật khẩu hiện tại."
            });
        }

        var identifier = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(
                identifier,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Phiên đăng nhập không hợp lệ."
            });
        }

        var user = await _context.Users.FirstOrDefaultAsync(
            u => u.Id == userId,
            cancellationToken);
        if (user == null)
        {
            return Unauthorized(new
            {
                success = false,
                message = "Không tìm thấy tài khoản."
            });
        }

        var verification = _passwordService.VerifyPassword(user, input.CurrentPassword);
        if (verification != PasswordVerificationStatus.Success)
        {
            return BadRequest(new
            {
                success = false,
                message = "Mật khẩu hiện tại không chính xác."
            });
        }

        user.PasswordHash = _passwordService.HashPassword(user, input.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await TryWriteAuditAsync(
            userId,
            "change_password",
            $"Tài khoản {user.Username} đổi mật khẩu.",
            cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Đổi mật khẩu thành công."
        });
    }

    public sealed class ChangePasswordRequest
    {
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmPassword { get; set; }
    }

    private async Task<IActionResult> SignOutCurrentUserAsync(
        CancellationToken cancellationToken)
    {
        var identifier = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(
                identifier,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var userId))
        {
            await TryWriteAuditAsync(
                userId,
                AuditActions.Logout,
                $"Tài khoản {User.Identity?.Name} đăng xuất.",
                cancellationToken);
        }

        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private string GetClientIpAddress()
    {
        // CHỈ dùng RemoteIpAddress - không tin header X-Forwarded-For/X-Real-IP
        // để tránh IP spoofing attack
        return NormalizeIpAddress(
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }

    private static string NormalizeIpAddress(string ip)
        => ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase)
            ? ip[7..]
            : ip;

    private bool IsIpInRange(string ipAddress, string cidr)
    {
        try
        {
            if (!cidr.Contains('/'))
            {
                return string.Equals(
                    ipAddress.Trim(),
                    cidr.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }

            var parts = cidr.Split('/');
            var networkAddress = IPAddress.Parse(parts[0].Trim());
            var prefixLength = int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
            var clientIp = IPAddress.Parse(ipAddress);
            var ipBytes = clientIp.GetAddressBytes();
            var networkBytes = networkAddress.GetAddressBytes();

            if (ipBytes.Length != 4 || networkBytes.Length != 4 ||
                prefixLength is < 0 or > 32)
            {
                return false;
            }

            var ipValue = BitConverter.ToUInt32(ipBytes.Reverse().ToArray(), 0);
            var networkValue = BitConverter.ToUInt32(networkBytes.Reverse().ToArray(), 0);
            var mask = prefixLength == 0
                ? 0U
                : uint.MaxValue << (32 - prefixLength);
            return (ipValue & mask) == (networkValue & mask);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "[IpCheck] Error parsing IP range: {Cidr}, IP: {Ip}",
                cidr,
                ipAddress);
            return false;
        }
    }

    private IActionResult RedirectToDashboard(string? role)
        => role switch
        {
            AppRoles.Director => RedirectToAction("Dashboard", "Director"),
            AppRoles.AdminIT => RedirectToAction("Dashboard", "AdminIT"),
            AppRoles.HumanResourcesManager => RedirectToAction("HumanResources", "ManageHuman"),
            AppRoles.HumanResourcesStaff => RedirectToAction("HumanResources", "HumanResourcesStaff"),
            AppRoles.BookingManager => RedirectToAction("Dashboard", "ManageBooking"),
            AppRoles.BookingStaff => RedirectToAction("Booking", "BookingStaff"),
            AppRoles.IdeaManager => RedirectToAction("Dashboard", "ManageIdea"),
            AppRoles.IdeaStaff => RedirectToAction("Idea", "IdeaStaff"),
            _ => RedirectToAction("Index", "Home")
        };

    private async Task TryWriteAuditAsync(
        int? userId,
        string actionType,
        string detail,
        CancellationToken cancellationToken)
    {
        try
        {
            _auditService.AddAccountEvent(userId, actionType, detail);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "Không thể ghi audit xác thực {ActionType} cho user {UserId}.",
                actionType,
                userId);
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
