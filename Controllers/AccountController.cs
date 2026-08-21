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

namespace HanaMedia.Controllers;

public sealed class AccountController : Controller
{
    private const string InvalidCredentialsMessage =
        "Tên đăng nhập hoặc mật khẩu không chính xác.";

    private readonly ApplicationDbContext _context;
    private readonly AccountService _accountService;
    private readonly IConfiguration _configuration;
    private readonly ISystemAuditService _auditService;
    private readonly ILogger<AccountController> _logger;
    private readonly IWebHostEnvironment _environment;

    public AccountController(
        ApplicationDbContext context,
        AccountService accountService,
        IConfiguration configuration,
        ISystemAuditService auditService,
        ILogger<AccountController> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _accountService = accountService;
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
                "login_blocked_network",
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
                    "login_succeeded",
                    $"Tài khoản {user.Username} đăng nhập thành công.",
                    cancellationToken);

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
                    new(ClaimTypes.Name, user.Username),
                    new(ClaimTypes.Email, user.Email),
                    new(ClaimTypes.Role, user.Role),
                    new(SecurityClaimTypes.SecurityStamp, user.SecurityStamp)
                };
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
                    "login_failed",
                    $"Tài khoản {Truncate(username, 100)} đang bị khóa tạm thời.",
                    cancellationToken);
                ViewBag.LockoutError = message;
                return View();

            case LoginResult.AccountInactive:
                await TryWriteAuditAsync(
                    user?.Id,
                    "login_failed",
                    $"Tài khoản {Truncate(username, 100)} đang bị khóa.",
                    cancellationToken);
                ViewBag.Error = message;
                return View();

            case LoginResult.UserNotFound:
            case LoginResult.WrongPassword:
            default:
                await TryWriteAuditAsync(
                    user?.Id,
                    "login_failed",
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
                "logout",
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
