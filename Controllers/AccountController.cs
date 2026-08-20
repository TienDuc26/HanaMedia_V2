using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.Services.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Controllers;

public sealed class AccountController : Controller
{
    private const string InvalidCredentialsMessage = "Tên đăng nhập hoặc mật khẩu không chính xác.";

    private readonly ApplicationDbContext _context;
    private readonly IAccountPasswordService _passwordService;
    private readonly ISystemAuditService _auditService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        ApplicationDbContext context,
        IAccountPasswordService passwordService,
        ISystemAuditService auditService,
        ILogger<AccountController> logger)
    {
        _context = context;
        _passwordService = passwordService;
        _auditService = auditService;
        _logger = logger;
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

        User? user;
        try
        {
            user = await _context.Users.FirstOrDefaultAsync(
                account => account.Username == username || account.Email == username,
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            ViewBag.Error = "Không thể kết nối cơ sở dữ liệu. Vui lòng thử lại sau.";
            return View();
        }

        if (user is null)
        {
            await TryWriteAuditAsync(
                null,
                "login_failed",
                $"Đăng nhập thất bại với định danh không tồn tại: {Truncate(username, 100)}.",
                cancellationToken);
            ViewBag.Error = InvalidCredentialsMessage;
            return View();
        }

        if (!string.Equals(user.Status, AccountStatuses.Active, StringComparison.Ordinal))
        {
            await TryWriteAuditAsync(
                user.Id,
                "login_failed",
                $"Tài khoản {user.Username} đang bị khóa.",
                cancellationToken);
            ViewBag.Error = "Tài khoản đang bị khóa.";
            return View();
        }

        if (_passwordService.VerifyPassword(user, password) == PasswordVerificationStatus.Failed)
        {
            await TryWriteAuditAsync(
                user.Id,
                "login_failed",
                $"Sai mật khẩu tài khoản {user.Username}.",
                cancellationToken);
            ViewBag.Error = InvalidCredentialsMessage;
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(SecurityClaimTypes.SecurityStamp, user.SecurityStamp.ToString("D"))
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

        await TryWriteAuditAsync(
            user.Id,
            "login_succeeded",
            $"Tài khoản {user.Username} đăng nhập thành công.",
            cancellationToken);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return RedirectToDashboard(user.Role);
    }

    [Authorize]
    [Route("Logout")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var identifier = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(identifier, NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
        {
            await TryWriteAuditAsync(
                userId,
                "logout",
                $"Tài khoản {User.Identity?.Name} đăng xuất.",
                cancellationToken);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [Route("AccessDenied")]
    [HttpGet]
    public IActionResult AccessDenied() => View();

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
            // Authentication must remain available even when audit persistence is temporarily unavailable.
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
