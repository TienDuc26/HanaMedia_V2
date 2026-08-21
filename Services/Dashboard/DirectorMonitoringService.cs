using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Dashboard;

public sealed class DirectorMonitoringService : IDirectorMonitoringService
{
    private static readonly IReadOnlyDictionary<string, string> ModuleLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AuditModules.HumanResources] = "Nhân sự",
            [AuditModules.Booking] = "Booking / KOL",
            [AuditModules.Ideas] = "Ý tưởng sáng tạo",
            [AuditModules.Accounts] = "Tài khoản & truy cập",
            [AuditModules.Configuration] = "Cấu hình"
        };

    private static readonly IReadOnlyDictionary<string, string> ActionLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AuditActions.LoginSucceeded] = "Đăng nhập",
            [AuditActions.LoginFailed] = "Đăng nhập thất bại",
            [AuditActions.LoginBlockedNetwork] = "Chặn truy cập ngoài mạng",
            [AuditActions.Logout] = "Đăng xuất",
            [AuditActions.AccountAutoLocked] = "Tự động khóa tài khoản",
            [AuditActions.BulkDelete] = "Xóa hàng loạt",
            ["account_created"] = "Tạo tài khoản",
            ["role_changed"] = "Đổi quyền tài khoản",
            ["account_locked"] = "Khóa tài khoản",
            ["account_unlocked"] = "Mở khóa tài khoản",
            ["password_reset"] = "Đặt lại mật khẩu",
            ["create"] = "Tạo mới",
            ["update"] = "Cập nhật",
            ["delete"] = "Xóa dữ liệu",
            ["approve"] = "Phê duyệt"
        };

    private readonly ApplicationDbContext _context;

    public DirectorMonitoringService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DirectorMonitoringViewModel> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var today = now.Date;
        var tomorrow = today.AddDays(1);
        var todayLogs = _context.SystemAuditLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= today && log.CreatedAt < tomorrow);

        var loginCount = await todayLogs.CountAsync(
            log => log.ActionType == AuditActions.LoginSucceeded,
            cancellationToken);
        var recentSince = now.AddMinutes(-15);
        var recentActiveUsers = await _context.SystemAuditLogs
            .AsNoTracking()
            .Where(log =>
                log.ActionType == AuditActions.LoginSucceeded &&
                log.UserId.HasValue &&
                log.CreatedAt >= recentSince &&
                log.CreatedAt <= now)
            .Select(log => log.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
        var blockedNetworkCount = await todayLogs.CountAsync(
            log => log.ActionType == AuditActions.LoginBlockedNetwork,
            cancellationToken);
        var securityAlertCount = await todayLogs.CountAsync(
            log =>
                log.ActionType == AuditActions.LoginFailed ||
                log.ActionType == AuditActions.LoginBlockedNetwork ||
                log.ActionType == AuditActions.AccountAutoLocked ||
                log.ActionType == AuditActions.BulkDelete,
            cancellationToken);

        var alertCounts = await todayLogs
            .Where(log =>
                log.ActionType == AuditActions.AccountAutoLocked ||
                log.ActionType == AuditActions.LoginBlockedNetwork ||
                log.ActionType == AuditActions.BulkDelete)
            .GroupBy(log => log.ActionType)
            .Select(group => new { Action = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Action, item => item.Count, cancellationToken);

        var since = today.AddDays(-29);
        var moduleActionCounts = await _context.SystemAuditLogs
            .AsNoTracking()
            .Where(log => log.CreatedAt >= since && log.CreatedAt < tomorrow)
            .GroupBy(log => new { log.Module, log.ActionType })
            .Select(group => new
            {
                group.Key.Module,
                group.Key.ActionType,
                Count = group.Count(),
                LastUpdatedAt = group.Max(log => log.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        var modules = ModuleLabels.Select(entry =>
        {
            var actions = moduleActionCounts
                .Where(item => item.Module == entry.Key)
                .ToList();
            var common = actions
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.ActionType, StringComparer.Ordinal)
                .FirstOrDefault();
            return new DirectorModuleActivityViewModel(
                entry.Value,
                actions.Sum(item => item.Count),
                common is null ? "Chưa có hoạt động" : GetActionLabel(common.ActionType),
                actions.Max(item => item.LastUpdatedAt));
        }).ToList();

        return new DirectorMonitoringViewModel
        {
            TodayLoginCount = loginCount,
            RecentActiveUserCount = recentActiveUsers,
            TodayBlockedNetworkCount = blockedNetworkCount,
            TodaySecurityAlertCount = securityAlertCount,
            Alerts = BuildAlerts(alertCounts),
            Modules = modules
        };
    }

    private static IReadOnlyList<DirectorMonitoringAlertViewModel> BuildAlerts(
        IReadOnlyDictionary<string, int> counts)
    {
        var alerts = new List<DirectorMonitoringAlertViewModel>();
        if (counts.TryGetValue(AuditActions.AccountAutoLocked, out var locked) && locked > 0)
        {
            alerts.Add(new("Tài khoản bị tự động khóa", $"{locked} trường hợp trong hôm nay.", "ti-lock"));
        }

        if (counts.TryGetValue(AuditActions.LoginBlockedNetwork, out var blocked) && blocked > 0)
        {
            alerts.Add(new("Truy cập ngoài mạng nội bộ", $"{blocked} yêu cầu đã bị chặn trong hôm nay.", "ti-shield-lock"));
        }

        if (counts.TryGetValue(AuditActions.BulkDelete, out var deleted) && deleted > 0)
        {
            alerts.Add(new("Xóa dữ liệu hàng loạt", $"{deleted} thao tác cần AdminIT kiểm tra chi tiết.", "ti-trash"));
        }

        return alerts;
    }

    private static string GetActionLabel(string action)
        => ActionLabels.TryGetValue(action, out var label)
            ? label
            : action.Replace('_', ' ');
}
