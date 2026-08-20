using HanaMedia.Models;

namespace HanaMedia.Services.Security;

public enum PasswordVerificationStatus
{
    Failed,
    Success
}

public interface IAccountPasswordService
{
    string HashPassword(User user, string password);

    PasswordVerificationStatus VerifyPassword(User user, string password);

    string GenerateTemporaryPassword();
}
