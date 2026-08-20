using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Security;

public sealed class AccountCookieEvents : CookieAuthenticationEvents
{
    private readonly ApplicationDbContext _context;

    public AccountCookieEvents(ApplicationDbContext context)
    {
        _context = context;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal is null)
        {
            await RejectAndSignOutAsync(context);
            return;
        }

        var identifier = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        User? account;

        if (!string.IsNullOrWhiteSpace(identifier))
        {
            if (!int.TryParse(
                    identifier,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var userId) ||
                userId <= 0)
            {
                await RejectAndSignOutAsync(context);
                return;
            }

            account = await _context.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    user => user.Id == userId,
                    context.HttpContext.RequestAborted);
        }
        else
        {
            var username = principal.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrWhiteSpace(username))
            {
                await RejectAndSignOutAsync(context);
                return;
            }

            account = await _context.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    user => user.Username == username,
                    context.HttpContext.RequestAborted);
        }

        if (account is null ||
            !string.Equals(account.Status, AccountStatuses.Active, StringComparison.Ordinal))
        {
            await RejectAndSignOutAsync(context);
            return;
        }

        var roleClaims = principal.FindAll(ClaimTypes.Role).ToArray();
        if (roleClaims.Length != 1 ||
            !string.Equals(roleClaims[0].Value, account.Role, StringComparison.Ordinal))
        {
            await RejectAndSignOutAsync(context);
            return;
        }

        var securityStampClaim = principal.FindFirst(SecurityClaimTypes.SecurityStamp)?.Value;
        if (account.SecurityStamp == Guid.Empty ||
            !Guid.TryParseExact(securityStampClaim, "D", out var securityStamp) ||
            securityStamp != account.SecurityStamp)
        {
            await RejectAndSignOutAsync(context);
        }
    }

    private static async Task RejectAndSignOutAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
    }
}

