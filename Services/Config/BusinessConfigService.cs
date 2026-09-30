using System.Globalization;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Config;

public sealed class BusinessConfigService : IBusinessConfigService
{
    public const string BookingApprovalThresholdKey = "booking_approval_threshold";
    public const string CampaignApprovalThresholdKey = "campaign_approval_threshold";
    public const string BookingWageLimitPercentageKey = "booking_wage_limit_percentage";
    public const string AllowBookingWageOverLimitKey = "allow_booking_wage_over_limit";
    public const string EmployeeHandoverDaysKey = "employee_handover_days";

    private readonly ApplicationDbContext _context;
    private readonly ISystemAuditService _auditService;

    public BusinessConfigService(ApplicationDbContext context, ISystemAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<BusinessConfigViewModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.BusinessConfigs.AsNoTracking().ToDictionaryAsync(item => item.ConfigKey, cancellationToken);
        return new BusinessConfigViewModel
        {
            CompanyPercent = Decimal(rows, "company_percent", 50m),
            CommissionPercent = Decimal(rows, "commission_percent", 10m),
            CastPercent = Decimal(rows, "cast_percent", 40m),
            BookingApprovalThreshold = Decimal(rows, BookingApprovalThresholdKey, 100_000_000m),
            CampaignApprovalThreshold = Decimal(rows, CampaignApprovalThresholdKey, 500_000_000m),
            BookingWageLimitPercentage = Decimal(rows, BookingWageLimitPercentageKey, 100m),
            AllowBookingWageOverLimit = Boolean(rows, AllowBookingWageOverLimitKey, false),
            EmployeeHandoverDays = Integer(rows, EmployeeHandoverDaysKey, 30),
            UpdatedAt = rows.Count == 0 ? null : rows.Values.Max(item => item.UpdatedAt)
        };
    }

    public async Task<BusinessConfigUpdateResult> UpdateAsync(BusinessConfigViewModel input, int actorUserId, CancellationToken cancellationToken = default)
    {
        if (input.CompanyPercent is < 0 or > 100 || input.CommissionPercent is < 0 or > 100 || input.CastPercent is < 0 or > 100 ||
            input.CompanyPercent + input.CommissionPercent + input.CastPercent != 100m ||
            new[] { input.CompanyPercent, input.CommissionPercent, input.CastPercent }.Any(x => decimal.Round(x, 2) != x))
            return new(false, "Ba tỷ lệ phải có tối đa 2 chữ số thập phân, không âm và tổng bằng 100%.");
        if (input.BookingApprovalThreshold is < 0 or > 100_000_000_000m || input.CampaignApprovalThreshold is < 0 or > 1_000_000_000_000m || input.BookingWageLimitPercentage is < 1 or > 500 || input.EmployeeHandoverDays is < 1 or > 365)
            return new(false, "Giá trị cấu hình nghiệp vụ không hợp lệ.");

        var now = DateTime.Now;
        await Upsert("company_percent", input.CompanyPercent.ToString(CultureInfo.InvariantCulture), "Phần công ty (%)", now, cancellationToken);
        await Upsert("commission_percent", input.CommissionPercent.ToString(CultureInfo.InvariantCulture), "Quỹ hoa hồng QL Booking (%)", now, cancellationToken);
        await Upsert("cast_percent", input.CastPercent.ToString(CultureInfo.InvariantCulture), "Quỹ cát-xê KOL (%)", now, cancellationToken);
        await Upsert(BookingApprovalThresholdKey, input.BookingApprovalThreshold.ToString(CultureInfo.InvariantCulture), "Ngưỡng giá trị Booking cần Giám đốc phê duyệt", now, cancellationToken);
        await Upsert(CampaignApprovalThresholdKey, input.CampaignApprovalThreshold.ToString(CultureInfo.InvariantCulture), "Ngưỡng ngân sách chiến dịch cần Giám đốc duyệt", now, cancellationToken);
        await Upsert(BookingWageLimitPercentageKey, input.BookingWageLimitPercentage.ToString(CultureInfo.InvariantCulture), "Tỷ lệ tối đa tổng thù lao trên giá trị Booking", now, cancellationToken);
        await Upsert(AllowBookingWageOverLimitKey, input.AllowBookingWageOverLimit ? "true" : "false", "Cho phép lưu phân bổ thù lao vượt giới hạn", now, cancellationToken);
        await Upsert(EmployeeHandoverDaysKey, input.EmployeeHandoverDays.ToString(CultureInfo.InvariantCulture), "Số ngày bàn giao hồ sơ khi nghỉ việc", now, cancellationToken);
        _auditService.AddEvent(new AuditEvent(AuditModules.Configuration, AuditActions.Updated,
            $"Cập nhật cấu hình nghiệp vụ: công ty/hoa hồng/cát-xê {input.CompanyPercent}/{input.CommissionPercent}/{input.CastPercent}%; ngưỡng Booking {input.BookingApprovalThreshold:N0}đ, ngưỡng chiến dịch {input.CampaignApprovalThreshold:N0}đ, giới hạn thù lao {input.BookingWageLimitPercentage}%, cho phép vượt: {(input.AllowBookingWageOverLimit ? "có" : "không")}, bàn giao {input.EmployeeHandoverDays} ngày.",
            actorUserId, "BusinessConfig", "module18"));
        await _context.SaveChangesAsync(cancellationToken);
        return new(true, "Đã lưu cấu hình nghiệp vụ.");
    }

    private async Task Upsert(string key, string value, string description, DateTime now, CancellationToken cancellationToken)
    {
        var row = await _context.BusinessConfigs.FirstOrDefaultAsync(item => item.ConfigKey == key, cancellationToken);
        if (row is null)
        {
            _context.BusinessConfigs.Add(new BusinessConfig { ConfigKey = key, ConfigValue = value, Description = description, UpdatedAt = now });
            return;
        }
        row.ConfigValue = value;
        row.Description = description;
        row.UpdatedAt = now;
    }

    private static decimal Decimal(IReadOnlyDictionary<string, BusinessConfig> rows, string key, decimal fallback)
        => rows.TryGetValue(key, out var row) && decimal.TryParse(row.ConfigValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static int Integer(IReadOnlyDictionary<string, BusinessConfig> rows, string key, int fallback)
        => rows.TryGetValue(key, out var row) && int.TryParse(row.ConfigValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static bool Boolean(IReadOnlyDictionary<string, BusinessConfig> rows, string key, bool fallback)
        => rows.TryGetValue(key, out var row) && bool.TryParse(row.ConfigValue, out var value) ? value : fallback;
}
