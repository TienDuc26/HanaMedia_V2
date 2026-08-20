using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Dashboard;

public sealed class AdminITDashboardService : IAdminITDashboardService
{
    private const string AccountModule = "Tai_Khoan";
    private const string LoginSucceededAction = "login_succeeded";
    private const string NetworkBlockedAction = "login_blocked_network";

    private readonly ApplicationDbContext _context;

    public AdminITDashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminITDashboardViewModel> GetAsync(
        string? period,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var normalizedPeriod = string.Equals(period, "week", StringComparison.OrdinalIgnoreCase)
            ? "week"
            : "today";
        var (startAt, endAt, previousStartAt) = GetPeriodRange(normalizedPeriod, now);

        var periodLogs = _context.SystemAuditLogs
            .AsNoTracking()
            .Where(log =>
                log.Module == AccountModule &&
                log.CreatedAt >= startAt &&
                log.CreatedAt < endAt);

        var loginCount = await periodLogs.CountAsync(
            log => log.ActionType == LoginSucceededAction,
            cancellationToken);
        var previousLoginCount = await _context.SystemAuditLogs
            .AsNoTracking()
            .CountAsync(
                log =>
                    log.Module == AccountModule &&
                    log.ActionType == LoginSucceededAction &&
                    log.CreatedAt >= previousStartAt &&
                    log.CreatedAt < startAt,
                cancellationToken);
        var activeUserCount = await periodLogs
            .Where(log =>
                log.ActionType == LoginSucceededAction &&
                log.UserId.HasValue)
            .Select(log => log.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
        var blockedNetworkCount = await periodLogs.CountAsync(
            log => log.ActionType == NetworkBlockedAction,
            cancellationToken);

        var temporarilyLockedUsers = await _context.Users
            .AsNoTracking()
            .Include(user => user.Employee)
            .Where(user => user.LockedUntil.HasValue && user.LockedUntil > now)
            .OrderByDescending(user => user.LockedUntil)
            .ToListAsync(cancellationToken);
        var accountsNearLockout = await _context.Users
            .AsNoTracking()
            .Include(user => user.Employee)
            .Where(user =>
                user.FailedLoginAttempts > 0 &&
                (!user.LockedUntil.HasValue || user.LockedUntil <= now))
            .OrderByDescending(user => user.FailedLoginAttempts)
            .ThenBy(user => user.Username)
            .ToListAsync(cancellationToken);

        var latestNetworkBlock = blockedNetworkCount == 0
            ? null
            : await periodLogs
                .Where(log => log.ActionType == NetworkBlockedAction)
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.Id)
                .FirstOrDefaultAsync(cancellationToken);

        var recentLoginRows = await _context.SystemAuditLogs
            .AsNoTracking()
            .Include(log => log.User)
                .ThenInclude(user => user!.Employee)
            .Where(log =>
                log.Module == AccountModule &&
                log.ActionType == LoginSucceededAction)
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Take(5)
            .ToListAsync(cancellationToken);

        var alerts = BuildAlerts(
            temporarilyLockedUsers,
            accountsNearLockout,
            blockedNetworkCount,
            latestNetworkBlock);

        return new AdminITDashboardViewModel
        {
            Period = normalizedPeriod,
            PeriodLabel = normalizedPeriod == "week" ? "Tuần này" : "Hôm nay",
            LoginCount = loginCount,
            LoginComparisonText = BuildComparisonText(loginCount, previousLoginCount, normalizedPeriod),
            IsLoginComparisonUp = loginCount >= previousLoginCount,
            ActiveUserCount = activeUserCount,
            BlockedNetworkCount = blockedNetworkCount,
            SecurityAlertCount = alerts.Count,
            SecurityAlerts = alerts,
            RecentSessions = recentLoginRows.Select(log => new AdminITRecentSessionViewModel
            {
                DisplayName = log.User?.Employee?.FullName ?? log.User?.Username ?? "Tài khoản đã xóa",
                RoleName = GetRoleLabel(log.User?.Role),
                IpAddress = string.IsNullOrWhiteSpace(log.IpAddress) ? "Không rõ" : log.IpAddress,
                TimeLabel = FormatRelativeTime(log.CreatedAt, now)
            }).ToList()
        };
    }

    private static (DateTime StartAt, DateTime EndAt, DateTime PreviousStartAt)
        GetPeriodRange(string period, DateTime now)
    {
        if (period == "week")
        {
            var daysFromMonday = ((int)now.DayOfWeek + 6) % 7;
            var startOfWeek = now.Date.AddDays(-daysFromMonday);
            return (startOfWeek, startOfWeek.AddDays(7), startOfWeek.AddDays(-7));
        }

        var today = now.Date;
        return (today, today.AddDays(1), today.AddDays(-1));
    }

    private static List<AdminITSecurityAlertViewModel> BuildAlerts(
        IReadOnlyList<User> temporarilyLockedUsers,
        IReadOnlyList<User> accountsNearLockout,
        int blockedNetworkCount,
        SystemAuditLog? latestNetworkBlock)
    {
        var alerts = new List<AdminITSecurityAlertViewModel>();

        foreach (var user in temporarilyLockedUsers.Take(3))
        {
            var displayName = user.Employee?.FullName ?? user.Username;
            alerts.Add(new AdminITSecurityAlertViewModel
            {
                Icon = "ti-lock",
                Color = "var(--red)",
                IsCritical = true,
                Title = $"Tài khoản {user.Username} đang bị khóa tạm thời",
                Description = $"{displayName} đã đăng nhập sai {user.FailedLoginAttempts} lần. Khóa đến {user.LockedUntil:HH:mm dd/MM/yyyy}."
            });
        }

        if (accountsNearLockout.Count > 0)
        {
            var accountSummary = string.Join(
                ", ",
                accountsNearLockout
                    .Take(5)
                    .Select(user => $"{user.Username} ({user.FailedLoginAttempts}/5)"));
            alerts.Add(new AdminITSecurityAlertViewModel
            {
                Icon = "ti-shield-alert",
                Color = "var(--amber)",
                Title = $"{accountsNearLockout.Count} tài khoản có lần đăng nhập sai",
                Description = $"Số lần sai hiện tại: {accountSummary}."
            });
        }

        if (blockedNetworkCount > 0)
        {
            alerts.Add(new AdminITSecurityAlertViewModel
            {
                Icon = "ti-shield-lock",
                Color = "var(--amber)",
                Title = $"Đã chặn {blockedNetworkCount} lần đăng nhập ngoài mạng cho phép",
                Description = latestNetworkBlock?.LogDetail ?? "Các yêu cầu ngoài IP whitelist đã bị từ chối."
            });
        }

        return alerts;
    }

    private static string BuildComparisonText(int current, int previous, string period)
    {
        var comparisonLabel = period == "week" ? "tuần trước" : "hôm qua";
        if (previous == 0)
        {
            return current == 0
                ? $"Không phát sinh so với {comparisonLabel}"
                : $"+{current} lượt so với {comparisonLabel}";
        }

        var percentage = (int)Math.Round((current - previous) * 100d / previous);
        return $"{percentage:+#;-#;0}% so với {comparisonLabel}";
    }

    private static string GetRoleLabel(string? role)
        => role is not null && AppRoles.TryGetLabel(role, out var label)
            ? label
            : "Không xác định";

    private static string FormatRelativeTime(DateTime? occurredAt, DateTime now)
    {
        if (!occurredAt.HasValue)
        {
            return "Không rõ";
        }

        var elapsed = now - occurredAt.Value;
        if (elapsed < TimeSpan.Zero || elapsed < TimeSpan.FromMinutes(1))
        {
            return "Vừa mới đây";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{Math.Max(1, (int)elapsed.TotalMinutes)} phút trước";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return $"{Math.Max(1, (int)elapsed.TotalHours)} giờ trước";
        }

        return occurredAt.Value.ToString("dd/MM/yyyy HH:mm");
    }
}
