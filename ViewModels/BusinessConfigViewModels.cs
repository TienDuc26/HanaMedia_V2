using System.ComponentModel.DataAnnotations;

namespace HanaMedia.ViewModels;

public sealed class BusinessConfigViewModel
{
    [Range(0, 100_000_000_000, ErrorMessage = "Ngưỡng Booking phải từ 0 đến 100 tỷ đồng.")]
    public decimal BookingApprovalThreshold { get; set; } = 100_000_000m;

    [Range(0, 1_000_000_000_000, ErrorMessage = "Ngưỡng chiến dịch phải từ 0 đến 1.000 tỷ đồng.")]
    public decimal CampaignApprovalThreshold { get; set; } = 500_000_000m;

    [Range(1, 500, ErrorMessage = "Tỷ lệ thù lao phải từ 1% đến 500%.")]
    public decimal BookingWageLimitPercentage { get; set; } = 100m;

    public bool AllowBookingWageOverLimit { get; set; }

    [Range(1, 365, ErrorMessage = "Thời hạn bàn giao phải từ 1 đến 365 ngày.")]
    public int EmployeeHandoverDays { get; set; } = 30;

    public DateTime? UpdatedAt { get; init; }
}

public sealed record BusinessConfigUpdateResult(bool Succeeded, string Message);
