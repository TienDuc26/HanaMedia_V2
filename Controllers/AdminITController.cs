using System.Globalization;
using System.Security.Claims;
using System.Text;
using HanaMedia.Constants;
using HanaMedia.Services.Accounts;
using HanaMedia.Services.Auditing;
using HanaMedia.Services.Dashboard;
using HanaMedia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using HanaMedia.Models;
using HanaMedia.Services.Config;
using HanaMedia.Services.Security;

namespace HanaMedia.Controllers;

[Authorize(Roles = AppRoles.AdminIT)]
public sealed class AdminITController : Controller
{
    private const string SuccessMessageKey = "AccountSuccessMessage";
    private const string ErrorMessageKey = "AccountErrorMessage";
    private const string TemporaryPasswordKey = "AccountTemporaryPassword";
    private const string TemporaryPasswordUsernameKey = "AccountTemporaryPasswordUsername";

    private readonly IAccountManagementService _accountManagementService;
    private readonly IAuditLogQueryService _auditLogQueryService;
    private readonly IAdminITDashboardService _dashboardService;
    private readonly ISystemConfigService _configService;
    private readonly IIpAccessControlService _ipAccessControlService;
    private readonly IClientIpResolver _ipResolver;
    private readonly ISystemAuditService _auditService;

    public AdminITController(
        IAccountManagementService accountManagementService,
        IAuditLogQueryService auditLogQueryService,
        IAdminITDashboardService dashboardService,
        ISystemConfigService configService,
        IIpAccessControlService ipAccessControlService,
        IClientIpResolver ipResolver,
        ISystemAuditService auditService)
    {
        _accountManagementService = accountManagementService;
        _auditLogQueryService = auditLogQueryService;
        _dashboardService = dashboardService;
        _configService = configService;
        _ipAccessControlService = ipAccessControlService;
        _ipResolver = ipResolver;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard(
        string? period,
        CancellationToken cancellationToken)
        => View(await _dashboardService.GetAsync(period, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Account(string? q, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Challenge();
        }

        var model = await _accountManagementService.GetPageAsync(q, currentUserId, cancellationToken);
        model.SuccessMessage = TempData[SuccessMessageKey] as string;
        model.ErrorMessage = TempData[ErrorMessageKey] as string;
        model.TemporaryPassword = TempData[TemporaryPasswordKey] as string;
        model.TemporaryPasswordUsername = TempData[TemporaryPasswordUsernameKey] as string;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAccount(CreateAccountInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SetModelStateError();
            return RedirectToAction(nameof(Account));
        }

        var actorUserId = GetCurrentUserId();
        if (!actorUserId.HasValue)
        {
            return Challenge();
        }

        ApplyResult(await _accountManagementService.CreateAccountAsync(input, actorUserId.Value, cancellationToken));
        return RedirectToAction(nameof(Account));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(ChangeRoleInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SetModelStateError();
            return RedirectToAction(nameof(Account));
        }

        var actorUserId = GetCurrentUserId();
        if (!actorUserId.HasValue)
        {
            return Challenge();
        }

        ApplyResult(await _accountManagementService.ChangeRoleAsync(input, actorUserId.Value, cancellationToken));
        return RedirectToAction(nameof(Account));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAccountStatus(AccountStatusInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SetModelStateError();
            return RedirectToAction(nameof(Account));
        }

        var actorUserId = GetCurrentUserId();
        if (!actorUserId.HasValue)
        {
            return Challenge();
        }

        ApplyResult(await _accountManagementService.SetStatusAsync(
            input.UserId,
            input.Status,
            input.ExpectedSecurityStamp,
            actorUserId.Value,
            cancellationToken));
        return RedirectToAction(nameof(Account));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(AccountIdInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SetModelStateError();
            return RedirectToAction(nameof(Account));
        }

        var actorUserId = GetCurrentUserId();
        if (!actorUserId.HasValue)
        {
            return Challenge();
        }

        ApplyResult(await _accountManagementService.ResetPasswordAsync(
            input.UserId,
            input.ExpectedSecurityStamp,
            actorUserId.Value,
            cancellationToken));
        return RedirectToAction(nameof(Account));
    }

    [HttpGet]
    public async Task<IActionResult> LoginHistory(
        int userId,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return BadRequest(new { message = "Mã tài khoản không hợp lệ." });
        }

        var history = await _accountManagementService.GetLoginHistoryAsync(userId, page, cancellationToken);
        return history is null
            ? NotFound(new { message = "Không tìm thấy tài khoản." })
            : Json(history);
    }

    [HttpGet]
    public async Task<IActionResult> AuditLog(
        [FromQuery] AuditLogFilterInputModel filter,
        CancellationToken cancellationToken)
    {
        var validationMessage = GetAuditFilterValidationMessage(filter);
        var queryFilter = validationMessage is null ? filter : CreateNoResultAuditFilter(filter);
        var model = await _auditLogQueryService.GetPageAsync(queryFilter, cancellationToken);
        if (validationMessage is not null)
        {
            model.Filter = filter;
        }
        model.ValidationMessage = validationMessage;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportAuditLog(
        [FromQuery] AuditLogFilterInputModel filter,
        CancellationToken cancellationToken)
    {
        var validationMessage = GetAuditFilterValidationMessage(filter);
        if (validationMessage is not null)
        {
            return BadRequest(validationMessage);
        }

        var export = await _auditLogQueryService.GetExportRowsAsync(filter, cancellationToken);
        if (export.ExceedsLimit)
        {
            return BadRequest("Báo cáo vượt quá 10.000 bản ghi. Vui lòng dùng bộ lọc để thu hẹp dữ liệu trước khi xuất.");
        }

        var csv = BuildAuditLogCsv(export.Rows);
        var body = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(csv);
        byte[] preamble = [0xEF, 0xBB, 0xBF];
        var fileBytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, fileBytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, fileBytes, preamble.Length, body.Length);

        var fileName = $"audit-log-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        return File(fileBytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ConfigSystem(CancellationToken cancellationToken)
    {
        var config = await _configService.GetConfigAsync(cancellationToken);
        var rules = await _ipAccessControlService.GetRulesAsync(cancellationToken);
        var clientIp = _ipResolver.GetClientIp(HttpContext);
        var accessDecision = await _ipAccessControlService.EvaluateAccessAsync(clientIp, cancellationToken);

        config.IpRestrictionEnabled = await _ipAccessControlService.IsRestrictionEnabledAsync();
        config.AllowLoopback = await _ipAccessControlService.IsAllowLoopbackEnabledAsync();
        config.CurrentClientIpInfo = accessDecision;
        config.WhitelistRules = rules.Where(r => string.Equals(r.RuleType, IpRuleType.Allow, StringComparison.OrdinalIgnoreCase)).ToList();
        config.BlacklistRules = rules.Where(r => string.Equals(r.RuleType, IpRuleType.Deny, StringComparison.OrdinalIgnoreCase)).ToList();

        ViewBag.SuccessMessage = TempData["SuccessMessage"] as string;
        ViewBag.ErrorMessage = TempData["ErrorMessage"] as string;
        return View(config);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveConfigSystem(
        int maxUploadSizeMb,
        int sessionTimeoutMinutes,
        CancellationToken cancellationToken)
    {
        if (maxUploadSizeMb <= 0)
        {
            TempData["ErrorMessage"] = "Dung lượng upload file phải lớn hơn 0 MB.";
            return RedirectToAction(nameof(ConfigSystem));
        }

        if (sessionTimeoutMinutes is < 1 or > 1440)
        {
            TempData["ErrorMessage"] = "Thời gian hết hạn phiên làm việc phải nằm trong khoảng từ 1 đến 1440 phút (tối đa 24 giờ).";
            return RedirectToAction(nameof(ConfigSystem));
        }

        var currentUserId = GetCurrentUserId();
        var currentUserName = User.Identity?.Name ?? "AdminIT";

        var config = await _configService.GetConfigAsync(cancellationToken);
        config.MaxUploadSizeMb = maxUploadSizeMb;
        config.SessionTimeoutMinutes = sessionTimeoutMinutes;

        await _configService.SaveConfigAsync(config, currentUserName, cancellationToken);

        if (currentUserId.HasValue)
        {
            await _auditService.WriteAsync(new AuditEvent(
                AuditModules.Configuration,
                AuditActions.Updated,
                $"Đã cập nhật tham số hạ tầng (Upload: {maxUploadSizeMb}MB, Session Timeout: {sessionTimeoutMinutes} phút)",
                currentUserId.Value,
                "SystemConfig",
                "1"
            ), cancellationToken);
        }

        TempData["SuccessMessage"] = "Đã lưu thiết lập tham số hạ tầng thành công!";
        return RedirectToAction(nameof(ConfigSystem));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddIpRule(string ipRange, string ruleType, string? description, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ipRange))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập IP hoặc dải IP hợp lệ.";
            return RedirectToAction(nameof(ConfigSystem));
        }

        var currentUserId = GetCurrentUserId();
        var currentUserName = User.Identity?.Name ?? "AdminIT";

        var (success, message, rule) = await _ipAccessControlService.AddRuleAsync(
            ipRange,
            ruleType,
            description,
            currentUserName,
            cancellationToken);

        if (success)
        {
            if (currentUserId.HasValue && rule != null)
            {
                var typeLabel = rule.RuleType == IpRuleType.Deny ? "Blacklist" : "Whitelist";
                await _auditService.WriteAsync(new AuditEvent(
                    AuditModules.Configuration,
                    AuditActions.Updated,
                    $"Đã thêm quy tắc IP [{rule.Cidr}] vào {typeLabel}",
                    currentUserId.Value,
                    "IpAccessRule",
                    rule.Id.ToString(CultureInfo.InvariantCulture)
                ), cancellationToken);
            }
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(ConfigSystem));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddIpRange(string ipRange, CancellationToken cancellationToken)
    {
        return await AddIpRule(ipRange, IpRuleType.Allow, null, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleIpRule(int ruleId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserName = User.Identity?.Name ?? "AdminIT";

        var (success, message) = await _ipAccessControlService.ToggleRuleAsync(ruleId, currentUserName, cancellationToken);
        if (success)
        {
            if (currentUserId.HasValue)
            {
                await _auditService.WriteAsync(new AuditEvent(
                    AuditModules.Configuration,
                    AuditActions.Updated,
                    $"Đã thay đổi trạng thái quy tắc IP ID={ruleId}",
                    currentUserId.Value,
                    "IpAccessRule",
                    ruleId.ToString(CultureInfo.InvariantCulture)
                ), cancellationToken);
            }
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(ConfigSystem));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteIpRule(int ruleId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserName = User.Identity?.Name ?? "AdminIT";

        var (success, message) = await _ipAccessControlService.DeleteRuleAsync(ruleId, currentUserName, cancellationToken);
        if (success)
        {
            if (currentUserId.HasValue)
            {
                await _auditService.WriteAsync(new AuditEvent(
                    AuditModules.Configuration,
                    AuditActions.Updated,
                    $"Đã xóa quy tắc IP ID={ruleId}",
                    currentUserId.Value,
                    "IpAccessRule",
                    ruleId.ToString(CultureInfo.InvariantCulture)
                ), cancellationToken);
            }
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(ConfigSystem));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveIpRange(string ipRange, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ipRange))
        {
            TempData["ErrorMessage"] = "Vui lòng chọn IP cần xóa.";
            return RedirectToAction(nameof(ConfigSystem));
        }

        var rules = await _ipAccessControlService.GetRulesAsync(cancellationToken);
        var targetRule = rules.FirstOrDefault(r => string.Equals(r.Cidr, ipRange.Trim(), StringComparison.OrdinalIgnoreCase));

        if (targetRule != null)
        {
            return await DeleteIpRule(targetRule.Id, cancellationToken);
        }

        TempData["ErrorMessage"] = $"Không tìm thấy quy tắc IP [{ipRange.Trim()}] để xóa.";
        return RedirectToAction(nameof(ConfigSystem));
    }

    #region RESTful API Endpoints for IP Rules Management

    [HttpGet("/api/admin/ip-rules")]
    public async Task<IActionResult> GetIpRulesApi(CancellationToken cancellationToken)
    {
        var rules = await _ipAccessControlService.GetRulesAsync(cancellationToken);
        var clientIp = _ipResolver.GetClientIp(HttpContext);
        var currentDecision = await _ipAccessControlService.EvaluateAccessAsync(clientIp, cancellationToken);

        return Ok(new
        {
            clientIp,
            currentDecision,
            rules
        });
    }

    [HttpPost("/api/admin/ip-rules")]
    public async Task<IActionResult> CreateIpRuleApi([FromBody] CreateIpRuleRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Cidr))
        {
            return BadRequest(new { message = "Cidr không được để trống." });
        }

        var userName = User.Identity?.Name ?? "AdminIT";
        var (success, message, rule) = await _ipAccessControlService.AddRuleAsync(
            request.Cidr,
            request.RuleType,
            request.Description,
            userName,
            cancellationToken);

        if (!success)
        {
            return BadRequest(new { message });
        }

        return Ok(new { message, rule });
    }

    [HttpPut("/api/admin/ip-rules/{id:int}/toggle")]
    public async Task<IActionResult> ToggleIpRuleApi(int id, CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name ?? "AdminIT";
        var (success, message) = await _ipAccessControlService.ToggleRuleAsync(id, userName, cancellationToken);
        if (!success)
        {
            return NotFound(new { message });
        }

        return Ok(new { message });
    }

    [HttpDelete("/api/admin/ip-rules/{id:int}")]
    public async Task<IActionResult> DeleteIpRuleApi(int id, CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name ?? "AdminIT";
        var (success, message) = await _ipAccessControlService.DeleteRuleAsync(id, userName, cancellationToken);
        if (!success)
        {
            return NotFound(new { message });
        }

        return Ok(new { message });
    }

    public class CreateIpRuleRequest
    {
        public string Cidr { get; set; } = string.Empty;
        public string RuleType { get; set; } = IpRuleType.Allow;
        public string? Description { get; set; }
    }

    #endregion

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            ? userId
            : null;
    }

    private void ApplyResult(AccountOperationResult result)
    {
        TempData[result.Succeeded ? SuccessMessageKey : ErrorMessageKey] = result.Message;
        if (result.Succeeded && !string.IsNullOrEmpty(result.TemporaryPassword))
        {
            TempData[TemporaryPasswordKey] = result.TemporaryPassword;
            TempData[TemporaryPasswordUsernameKey] = result.Username;
        }
    }

    private void SetModelStateError()
    {
        var message = ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(error => !string.IsNullOrWhiteSpace(error));

        TempData[ErrorMessageKey] = message ?? "Dữ liệu gửi lên không hợp lệ.";
    }

    private static string BuildAuditLogCsv(IReadOnlyList<AuditLogItemViewModel> rows)
    {
        var csv = new StringBuilder();
        AppendCsvRow(csv,
        [
            "Thời gian",
            "Người thực hiện",
            "Vai trò hiện tại",
            "Hành động",
            "Mã hành động",
            "Phân hệ",
            "Nội dung chi tiết",
            "IP nguồn"
        ]);

        foreach (var row in rows)
        {
            AppendCsvRow(csv,
            [
                !row.OccurredAt.HasValue
                    ? string.Empty
                    : row.OccurredAt.Value.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                row.ActorDisplayName,
                row.RoleName,
                row.ActionLabel,
                row.ActionType,
                row.ModuleLabel,
                row.Detail,
                row.IpAddress
            ]);
        }

        return csv.ToString();
    }

    private static void AppendCsvRow(StringBuilder csv, IReadOnlyList<string> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0)
            {
                csv.Append(',');
            }

            csv.Append(EscapeCsvCell(values[index]));
        }

        csv.Append("\r\n");
    }

    private static string EscapeCsvCell(string? value)
    {
        value ??= string.Empty;
        if (CanBeInterpretedAsFormula(value))
        {
            value = $"'{value}";
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static bool CanBeInterpretedAsFormula(string value)
    {
        var index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index < value.Length && value[index] is '=' or '+' or '-' or '@';
    }

    private string? GetAuditFilterValidationMessage(AuditLogFilterInputModel filter)
    {
        if (HasModelStateErrorsFor(nameof(AuditLogFilterInputModel.From)) ||
            HasModelStateErrorsFor(nameof(AuditLogFilterInputModel.To)))
        {
            return "Ngày lọc không hợp lệ. Vui lòng chọn ngày theo đúng định dạng.";
        }

        return filter.From.HasValue && filter.To.HasValue && filter.From > filter.To
            ? "Ngày bắt đầu không được lớn hơn ngày kết thúc."
            : null;
    }

    private bool HasModelStateErrorsFor(string propertyName)
        => ModelState.Any(entry =>
            (string.Equals(entry.Key, propertyName, StringComparison.OrdinalIgnoreCase) ||
             entry.Key.EndsWith($".{propertyName}", StringComparison.OrdinalIgnoreCase)) &&
            entry.Value is { Errors.Count: > 0 });

    private static AuditLogFilterInputModel CreateNoResultAuditFilter(AuditLogFilterInputModel filter)
        => new()
        {
            User = filter.User,
            ActionType = filter.ActionType,
            Module = filter.Module,
            From = new DateOnly(9999, 12, 31),
            To = new DateOnly(9999, 12, 30),
            Page = 1
        };
}
