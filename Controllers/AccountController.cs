using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;
using HanaMedia.Models;
using HanaMedia.Services;

namespace HanaMedia.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly AccountService _accountService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            AccountService accountService,
            IConfiguration configuration,
            ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _configuration = configuration;
            _logger = logger;
        }

        [Route("")]
        [Route("Login")]
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                return RedirectToDashboard(role);
            }
            return View();
        }

        [Route("Login")]
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Tên đăng nhập và mật khẩu không được để trống.";
                return View();
            }

            // Kiểm tra IP trước khi kiểm tra tài khoản
            var clientIp = GetClientIpAddress();
            var allowedCidr = _configuration["AllowedNetworkCidr"] ?? "192.168.110.0/24";

            _logger.LogInformation("[NetworkCheck] Client IP: {ClientIp}, Allowed CIDR: {AllowedCidr}", clientIp, allowedCidr);

            if (!IsIpInRange(clientIp, allowedCidr))
            {
                _logger.LogWarning("[NetworkCheck] BLOCKED - IP {ClientIp} not in range {AllowedCidr}", clientIp, allowedCidr);
                ViewBag.NetworkError = "Vui lòng kết nối mạng nội bộ công ty để sử dụng hệ thống.";
                return View();
            }

            _logger.LogInformation("[NetworkCheck] PASSED - IP {ClientIp} is in range {AllowedCidr}", clientIp, allowedCidr);

            // Gọi AccountService để xác thực (đã bao gồm logic lockout)
            var (result, user, message) = await _accountService.AuthenticateAsync(username, password);

            switch (result)
            {
                case LoginResult.Success:
                    // Sign in
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user!.Username),
                        new Claim(ClaimTypes.Role, user.Role)
                    };
                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
                    };
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
                    return RedirectToDashboard(user.Role);

                case LoginResult.AccountLockedTemporarily:
                    // Popup riêng biệt - khác với popup sai mật khẩu và popup sai mạng
                    ViewBag.LockoutError = message;
                    return View();

                case LoginResult.AccountInactive:
                case LoginResult.UserNotFound:
                case LoginResult.WrongPassword:
                default:
                    ViewBag.Error = message;
                    return View();
            }
        }

        [Route("Logout")]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        private string GetClientIpAddress()
        {
            // Kiểm tra X-Forwarded-For header (khi deploy qua proxy/load balancer)
            var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ip = forwardedFor.Split(',')[0].Trim();
                _logger.LogInformation("[GetClientIp] From X-Forwarded-For: {Ip}", ip);
                return NormalizeIpAddress(ip);
            }

            // Kiểm tra X-Real-IP header
            var realIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                _logger.LogInformation("[GetClientIp] From X-Real-IP: {Ip}", realIp);
                return NormalizeIpAddress(realIp);
            }

            // Lấy IP trực tiếp từ connection
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogInformation("[GetClientIp] From RemoteIpAddress: {Ip}", remoteIp);
            return NormalizeIpAddress(remoteIp);
        }

        private string NormalizeIpAddress(string ip)
        {
            // Xử lý IPv4-mapped IPv6 (VD: ::ffff:192.168.1.1 → 192.168.1.1)
            if (ip.StartsWith("::ffff:"))
            {
                return ip.Substring(7);
            }
            return ip;
        }

        private bool IsIpInRange(string ipAddress, string cidr)
        {
            try
            {
                // Loopback luôn được cho phép (COMMENT TẠM ĐỂ TEST)
                // if (ipAddress == "127.0.0.1" || ipAddress == "::1" || ipAddress == "localhost")
                // {
                //     _logger.LogInformation("[IpCheck] Loopback IP allowed");
                //     return true;
                // }

                // Xử lý format CIDR
                if (!cidr.Contains('/'))
                {
                    // Single IP
                    return ipAddress.Trim() == cidr.Trim();
                }

                var parts = cidr.Split('/');
                var networkAddress = IPAddress.Parse(parts[0].Trim());
                var prefixLength = int.Parse(parts[1].Trim());
                var clientIp = IPAddress.Parse(ipAddress);

                var ipBytes = clientIp.GetAddressBytes();
                var networkBytes = networkAddress.GetAddressBytes();

                // IPv4
                if (ipBytes.Length == 4 && networkBytes.Length == 4)
                {
                    var ipInt = BitConverter.ToUInt32(ipBytes.Reverse().ToArray(), 0);
                    var networkInt = BitConverter.ToUInt32(networkBytes.Reverse().ToArray(), 0);
                    var mask = uint.MaxValue << (32 - prefixLength);

                    var result = (ipInt & mask) == (networkInt & mask);
                    _logger.LogInformation("[IpCheck] {Ip}/{Prefix} vs {Network} = {Result}", ipAddress, prefixLength, networkAddress, result);
                    return result;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[IpCheck] Error parsing IP range: {Cidr}, IP: {Ip}", cidr, ipAddress);
                return false;
            }
        }

        private IActionResult RedirectToDashboard(string? role)
        {
            switch (role)
            {
                case "giam_doc":
                    return RedirectToAction("Dashboard", "Director");
                case "admin_it":
                    return RedirectToAction("Dashboard", "AdminIT");
                case "ql_hcns":
                    return RedirectToAction("HumanResources", "ManageHuman");
                case "nv_hcns":
                    return RedirectToAction("HumanResources", "HumanResourcesStaff");
                case "ql_booking":
                    return RedirectToAction("Dashboard", "ManageBooking");
                case "nv_booking":
                    return RedirectToAction("Booking", "BookingStaff");
                case "ql_y_tuong":
                    return RedirectToAction("Dashboard", "ManageIdea");
                case "nv_y_tuong":
                    return RedirectToAction("Idea", "IdeaStaff");
                default:
                    return RedirectToAction("Index", "Home");
            }
        }
    }
}
