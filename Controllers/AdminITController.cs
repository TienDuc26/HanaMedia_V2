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

    public AdminITController(
        IAccountManagementService accountManagementService,
        IAuditLogQueryService auditLogQueryService,
        IAdminITDashboardService dashboardService)
    {
        _accountManagementService = accountManagementService;
        _auditLogQueryService = auditLogQueryService;
        _dashboardService = dashboardService;
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

    public IActionResult ConfigSystem() => View();

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
