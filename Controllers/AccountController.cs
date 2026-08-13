using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HanaMedia.Models;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
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

            User? user = null;

            try
            {
                user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == username || u.Email == username);
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Lỗi kết nối cơ sở dữ liệu: {ex.Message}";
                return View();
            }

            if (user == null)
            {
                ViewBag.Error = "Tài khoản không tồn tại.";
                return View();
            }

            if (user.Status != "active")
            {
                ViewBag.Error = "Tài khoản đang bị khóa.";
                return View();
            }

            // Verify password (plain text or SHA256)
            bool isPasswordMatch = password == user.PasswordHash || ComputeSha256Hash(password) == user.PasswordHash;

            if (!isPasswordMatch)
            {
                ViewBag.Error = "Mật khẩu không chính xác.";
                return View();
            }

            // Sign in
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
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
        }

        [Route("Logout")]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
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

        private string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
