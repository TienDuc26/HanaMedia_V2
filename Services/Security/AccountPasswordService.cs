using System.Security.Cryptography;
using System.Text;
using HanaMedia.Models;
using Microsoft.AspNetCore.Identity;

namespace HanaMedia.Services.Security;

public sealed class AccountPasswordService : IAccountPasswordService
{
    private const int TemporaryPasswordLength = 16;
    private const string LowercaseCharacters = "abcdefghijkmnopqrstuvwxyz";
    private const string UppercaseCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string DigitCharacters = "23456789";
    private const string SymbolCharacters = "!@$?_-";
    private const string AllCharacters =
        LowercaseCharacters + UppercaseCharacters + DigitCharacters + SymbolCharacters;

    private readonly IPasswordHasher<User> _identityCompatibilityHasher;

    public AccountPasswordService(IPasswordHasher<User> identityCompatibilityHasher)
    {
        _identityCompatibilityHasher = identityCompatibilityHasher ??
            throw new ArgumentNullException(nameof(identityCompatibilityHasher));
    }

    public string HashPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        // Module 1 and the existing database share this SHA-256 format.
        // Do not migrate hashes implicitly during account creation or sign-in.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public PasswordVerificationStatus VerifyPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return PasswordVerificationStatus.Failed;
        }

        if (IsSha256Hash(user.PasswordHash))
        {
            // Legacy compatibility: the original login accepted the stored SHA-256
            // value itself as a credential. Keep that behavior without rewriting it.
            var isPasswordMatch = MatchesStoredValue(user.PasswordHash, password)
                || VerifySha256(user.PasswordHash, password);

            return isPasswordMatch
                ? PasswordVerificationStatus.Success
                : PasswordVerificationStatus.Failed;
        }

        try
        {
            var result = _identityCompatibilityHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                password);

            return result switch
            {
                PasswordVerificationResult.Success => PasswordVerificationStatus.Success,
                // Existing Identity hashes remain readable but are never rewritten here.
                PasswordVerificationResult.SuccessRehashNeeded =>
                    PasswordVerificationStatus.Success,
                _ => PasswordVerificationStatus.Failed
            };
        }
        catch (FormatException)
        {
            return PasswordVerificationStatus.Failed;
        }
        catch (CryptographicException)
        {
            return PasswordVerificationStatus.Failed;
        }
    }

    public string GenerateTemporaryPassword()
    {
        var password = new char[TemporaryPasswordLength];
        password[0] = GetRandomCharacter(LowercaseCharacters);
        password[1] = GetRandomCharacter(UppercaseCharacters);
        password[2] = GetRandomCharacter(DigitCharacters);
        password[3] = GetRandomCharacter(SymbolCharacters);

        for (var index = 4; index < password.Length; index++)
        {
            password[index] = GetRandomCharacter(AllCharacters);
        }

        for (var index = password.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (password[index], password[swapIndex]) = (password[swapIndex], password[index]);
        }

        return new string(password);
    }

    private static bool IsSha256Hash(string storedHash)
    {
        if (storedHash.Length != 64)
        {
            return false;
        }

        foreach (var character in storedHash)
        {
            if (character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')
                and not (>= 'A' and <= 'F'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool VerifySha256(string storedHash, string password)
    {
        var expectedHash = Convert.FromHexString(storedHash);
        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(password));

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private static bool MatchesStoredValue(string storedHash, string suppliedValue)
    {
        var storedBytes = Encoding.UTF8.GetBytes(storedHash);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedValue);
        return CryptographicOperations.FixedTimeEquals(storedBytes, suppliedBytes);
    }

    private static char GetRandomCharacter(string characters) =>
        characters[RandomNumberGenerator.GetInt32(characters.Length)];
}
