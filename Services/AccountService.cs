using System.Security.Cryptography;
using System.Text;
using HanaMedia.Models;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services
{
    public enum LoginResult
    {
        Success,
        UserNotFound,
        AccountInactive,
        AccountLockedTemporarily,
        WrongPassword
    }

    public class AccountService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountService> _logger;

        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        public AccountService(ApplicationDbContext context, ILogger<AccountService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(LoginResult Result, User? User, string? Message)> AuthenticateAsync(
            string usernameOrEmail, string password)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == usernameOrEmail || u.Email == usernameOrEmail);

                if (user == null)
                {
                    return (LoginResult.UserNotFound, null, "Tài khoản không tồn tại.");
                }

                if (user.Status != "active")
                {
                    return (LoginResult.AccountInactive, null, "Tài khoản đang bị khóa.");
                }

                // Kiểm tra khóa tạm TRƯỚC khi check mật khẩu
                if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
                {
                    var minutesLeft = (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
                    _logger.LogWarning("[Lockout] Account {Username} is locked until {LockedUntil} ({MinutesLeft} min left)",
                        user.Username, user.LockedUntil, minutesLeft);

                    return (LoginResult.AccountLockedTemporarily, user,
                        $"Tài khoản tạm thời bị khóa do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau {minutesLeft} phút hoặc liên hệ quản trị viên.");
                }

                // Verify password
                bool isPasswordMatch = password == user.PasswordHash
                                       || ComputeSha256Hash(password) == user.PasswordHash;

                if (!isPasswordMatch)
                {
                    await IncrementFailedAttemptsAsync(user);
                    return (LoginResult.WrongPassword, null, "Mật khẩu không chính xác.");
                }

                // Đăng nhập thành công → reset bộ đếm
                await ResetFailedAttemptsAsync(user);

                return (LoginResult.Success, user, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Authenticate] Error during authentication for {Username}", usernameOrEmail);
                return (LoginResult.UserNotFound, null, $"Lỗi hệ thống: {ex.Message}");
            }
        }

        private async Task IncrementFailedAttemptsAsync(User user)
        {
            try
            {
                user.FailedLoginAttempts += 1;
                _logger.LogInformation("[Lockout] Failed attempt #{Count} for {Username}", user.FailedLoginAttempts, user.Username);

                if (user.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    user.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);

                    _logger.LogWarning("[Lockout] Account {Username} auto-locked until {LockedUntil} after {Count} failed attempts",
                        user.Username, user.LockedUntil, user.FailedLoginAttempts);

                    // TODO: Khi Module 3 (Audit Log) hoàn thành, gọi qua AuditLogService ở đây
                    // _auditLogService.LogSecurityWarning("AUTO_LOCKOUT", user.Username, ...);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Lockout] Failed to increment counter for {Username}", user.Username);
            }
        }

        private async Task ResetFailedAttemptsAsync(User user)
        {
            try
            {
                if (user.FailedLoginAttempts > 0 || user.LockedUntil.HasValue)
                {
                    user.FailedLoginAttempts = 0;
                    user.LockedUntil = null;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("[Lockout] Reset failed attempts for {Username} after successful login", user.Username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Lockout] Failed to reset counter for {Username}", user.Username);
            }
        }

        public static string ComputeSha256Hash(string rawData)
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
