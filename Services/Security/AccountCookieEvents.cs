using System.Globalization;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
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
        if (string.IsNullOrWhiteSpace(account.SecurityStamp) ||
            securityStampClaim != account.SecurityStamp)
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

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        // Nếu là request API (XHR/fetch với Accept JSON hoặc X-Requested-With),
        // trả 401 JSON thay vì redirect 302 về trang Login.
        if (IsApiRequest(context.HttpContext.Request))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "Bạn chưa đăng nhập hoặc phiên đã hết hạn.",
                code = "UNAUTHORIZED"
            });
        }
        return base.RedirectToLogin(context);
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (IsApiRequest(context.HttpContext.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "Bạn không có quyền thực hiện thao tác này.",
                code = "FORBIDDEN"
            });
        }
        return base.RedirectToAccessDenied(context);
    }

    private static bool IsApiRequest(HttpRequest request)
    {
        if (string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return true;
        var accept = request.Headers["Accept"].ToString();
        if (!string.IsNullOrEmpty(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return true;
        if (request.Path.StartsWithSegments("/Director/Department", StringComparison.OrdinalIgnoreCase) &&
            request.Path.Value!.Contains("/Api", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}

