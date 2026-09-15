using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Dashboard;

public sealed class CompanyDashboardService : ICompanyDashboardService
{
    private static readonly string[] ActiveEmployeeStatuses = ["dang_lam_viec", "thu_viec", "cho_duyet_nghi"];
    private static readonly (string Status, string Label)[] BookingStatusOrder =
    [
        ("dang_cho", "Đang chờ"),
        ("thuong_luong", "Thương lượng"),
        ("da_chot", "Đã chốt"),
        ("dang_trien_khai", "Đang triển khai"),
        ("hoan_thanh", "Hoàn thành"),
        ("huy", "Đã hủy")
    ];

    private readonly ApplicationDbContext _context;

    public CompanyDashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CompanyDashboardViewModel> GetAsync(
        string? period,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var normalizedPeriod = NormalizePeriod(period);
        var (startAt, endAt, periodLabel) = GetPeriodRange(normalizedPeriod, now);
        var startDate = DateOnly.FromDateTime(startAt);
        var endDate = DateOnly.FromDateTime(endAt);

        var departments = await _context.Departments.AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new { item.Code, item.Name })
            .ToListAsync(cancellationToken);
        var departmentNames = departments.ToDictionary(item => item.Code, item => item.Name);

        var employees = await _context.Employees.AsNoTracking()
            .Select(item => new EmployeeMetricRow
            {
                Id = item.Id,
                FullName = item.FullName,
                Department = item.Department,
                Position = item.Position,
                JoinedDate = item.JoinedDate,
                Status = item.Status,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        var activeEmployees = employees
            .Where(item => ActiveEmployeeStatuses.Contains(item.Status ?? string.Empty))
            .ToList();

        var departmentRows = activeEmployees
            .GroupBy(item => item.Department)
            .Select(group => new CompanyDashboardDepartmentViewModel
            {
                Code = group.Key,
                Name = GetDepartmentName(group.Key, departmentNames),
                EmployeeCount = group.Count(),
                Percentage = activeEmployees.Count == 0
                    ? 0
                    : Math.Round(group.Count() * 100m / activeEmployees.Count, 1)
            })
            .OrderByDescending(item => item.EmployeeCount)
            .ThenBy(item => item.Name)
            .ToList();

        var bookings = await _context.Bookings.AsNoTracking()
            .Where(item => item.CreatedAt.HasValue && item.CreatedAt >= startAt && item.CreatedAt < endAt)
            .Select(item => new BookingMetricRow
            {
                Status = item.Status,
                BookingPrice = item.BookingPrice,
                ActualCost = item.ActualCost,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var validBookings = bookings.Where(item => item.Status != "huy").ToList();

        var bookingStatusCounts = BookingStatusOrder
            .Select(item => new CompanyDashboardBookingStatusViewModel
            {
                Status = item.Status,
                Label = item.Label,
                Count = bookings.Count(booking => booking.Status == item.Status)
            })
            .ToList();

        var taskRows = await _context.WorkTasks.AsNoTracking()
            .Where(item => item.Deadline >= startAt && item.Deadline < endAt)
            .Select(item => new TaskMetricRow
            {
                Status = item.Status,
                Department = item.AssignedEmployee.Department
            })
            .ToListAsync(cancellationToken);
        var departmentPerformance = taskRows
            .GroupBy(item => item.Department)
            .Select(group => new CompanyDashboardDepartmentPerformanceViewModel
            {
                Code = group.Key,
                Name = GetDepartmentName(group.Key, departmentNames),
                TotalTasks = group.Count(),
                CompletedTasks = group.Count(item => item.Status == WorkTaskStatuses.Done),
                CompletionRate = Math.Round(group.Count(item => item.Status == WorkTaskStatuses.Done) * 100m / group.Count(), 1)
            })
            .OrderByDescending(item => item.CompletionRate)
            .ThenBy(item => item.Name)
            .ToList();

        var employeeChanges = BuildEmployeeChanges(employees, departmentNames, startAt, endAt);

        return new CompanyDashboardViewModel
        {
            Period = normalizedPeriod,
            PeriodLabel = periodLabel,
            RangeStart = startAt,
            RangeEndExclusive = endAt,
            GeneratedAt = now,
            TotalEmployees = activeEmployees.Count,
            NewEmployees = employees.Count(item => item.JoinedDate >= startDate && item.JoinedDate < endDate),
            DepartedEmployees = employees.Count(item =>
                item.Status == "ngung_hoat_dong" &&
                item.UpdatedAt.HasValue && item.UpdatedAt >= startAt && item.UpdatedAt < endAt),
            BookingCount = bookings.Count,
            RunningBookings = bookings.Count(item => item.Status == "dang_trien_khai"),
            BookingRevenue = validBookings.Sum(item => item.BookingPrice),
            BookingCost = validBookings.Sum(item => item.ActualCost),
            RunningCampaigns = await _context.Campaigns.AsNoTracking()
                .CountAsync(item => item.Status == "running", cancellationToken),
            PendingIdeas = await _context.Ideas.AsNoTracking()
                .CountAsync(item =>
                    item.Status == IdeaStatuses.Review ||
                    (item.Status == IdeaStatuses.Approved &&
                     item.DirectorReviewStatus == DirectorIdeaReviewStatuses.Pending),
                    cancellationToken),
            OverdueTasks = await _context.WorkTasks.AsNoTracking()
                .CountAsync(item => item.Deadline < now && item.Status != WorkTaskStatuses.Done, cancellationToken),
            Departments = departmentRows,
            DepartmentPerformance = departmentPerformance,
            BookingStatuses = bookingStatusCounts,
            RevenueSeries = BuildRevenueSeries(bookings, normalizedPeriod, startAt, endAt),
            EmployeeChanges = employeeChanges
        };
    }

    private static string NormalizePeriod(string? period) => period?.ToLowerInvariant() switch
    {
        "day" => "day",
        "quarter" => "quarter",
        _ => "month"
    };

    private static (DateTime StartAt, DateTime EndAt, string Label) GetPeriodRange(string period, DateTime now)
    {
        if (period == "day")
        {
            return (now.Date, now.Date.AddDays(1), "Hôm nay");
        }

        if (period == "quarter")
        {
            var startMonth = ((now.Month - 1) / 3) * 3 + 1;
            var startOfQuarter = new DateTime(now.Year, startMonth, 1);
            return (startOfQuarter, startOfQuarter.AddMonths(3), $"Quý {(startMonth - 1) / 3 + 1}/{now.Year}");
        }

        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        return (startOfMonth, startOfMonth.AddMonths(1), $"Tháng {now.Month}/{now.Year}");
    }

    private static IReadOnlyList<CompanyDashboardRevenuePointViewModel> BuildRevenueSeries(
        IEnumerable<BookingMetricRow> bookingRows,
        string period,
        DateTime startAt,
        DateTime endAt)
    {
        var bookings = bookingRows
            .Where(item => item.Status != "huy" && item.CreatedAt is not null)
            .ToList();
        var result = new List<CompanyDashboardRevenuePointViewModel>();

        if (period == "quarter")
        {
            for (var cursor = startAt; cursor < endAt; cursor = cursor.AddMonths(1))
            {
                var bucketEnd = cursor.AddMonths(1);
                var rows = bookings.Where(item => item.CreatedAt!.Value >= cursor && item.CreatedAt.Value < bucketEnd).ToList();
                result.Add(new CompanyDashboardRevenuePointViewModel
                {
                    Label = $"T{cursor.Month}",
                    Revenue = rows.Sum(item => item.BookingPrice),
                    Cost = rows.Sum(item => item.ActualCost)
                });
            }

            return result;
        }

        if (period == "day")
        {
            result.Add(new CompanyDashboardRevenuePointViewModel
            {
                Label = "Hôm nay",
                Revenue = bookings.Sum(item => item.BookingPrice),
                Cost = bookings.Sum(item => item.ActualCost)
            });
            return result;
        }

        for (var cursor = startAt; cursor < endAt; cursor = cursor.AddDays(7))
        {
            var bucketEnd = cursor.AddDays(7) < endAt ? cursor.AddDays(7) : endAt;
            var rows = bookings.Where(item => item.CreatedAt!.Value >= cursor && item.CreatedAt.Value < bucketEnd).ToList();
            result.Add(new CompanyDashboardRevenuePointViewModel
            {
                Label = $"{cursor:dd/MM}",
                Revenue = rows.Sum(item => item.BookingPrice),
                Cost = rows.Sum(item => item.ActualCost)
            });
        }

        return result;
    }

    private static IReadOnlyList<CompanyDashboardEmployeeChangeViewModel> BuildEmployeeChanges(
        IEnumerable<EmployeeMetricRow> employeeRows,
        IReadOnlyDictionary<string, string> departmentNames,
        DateTime startAt,
        DateTime endAt)
    {
        var changes = new List<CompanyDashboardEmployeeChangeViewModel>();
        foreach (var employee in employeeRows)
        {
            var joinedAt = employee.JoinedDate.ToDateTime(TimeOnly.MinValue);
            if (joinedAt >= startAt && joinedAt < endAt)
            {
                changes.Add(new CompanyDashboardEmployeeChangeViewModel
                {
                    EmployeeId = employee.Id,
                    FullName = employee.FullName,
                    DepartmentName = GetDepartmentName(employee.Department, departmentNames),
                    Position = employee.Position,
                    EventDate = joinedAt,
                    ChangeType = "joined",
                    ChangeLabel = "Nhân sự mới"
                });
            }

            if (employee.Status == "ngung_hoat_dong" && employee.UpdatedAt is DateTime departedAt &&
                departedAt >= startAt && departedAt < endAt)
            {
                changes.Add(new CompanyDashboardEmployeeChangeViewModel
                {
                    EmployeeId = employee.Id,
                    FullName = employee.FullName,
                    DepartmentName = GetDepartmentName(employee.Department, departmentNames),
                    Position = employee.Position,
                    EventDate = departedAt,
                    ChangeType = "departed",
                    ChangeLabel = "Nghỉ việc"
                });
            }
        }

        return changes.OrderByDescending(item => item.EventDate).Take(8).ToList();
    }

    private static string GetDepartmentName(string code, IReadOnlyDictionary<string, string> departmentNames)
        => departmentNames.TryGetValue(code, out var name) ? name : code;

    private sealed class EmployeeMetricRow
    {
        public int Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Department { get; init; } = string.Empty;
        public string Position { get; init; } = string.Empty;
        public DateOnly JoinedDate { get; init; }
        public string? Status { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    private sealed class BookingMetricRow
    {
        public string? Status { get; init; }
        public decimal BookingPrice { get; init; }
        public decimal ActualCost { get; init; }
        public DateTime? CreatedAt { get; init; }
    }

    private sealed class TaskMetricRow
    {
        public string Status { get; init; } = string.Empty;
        public string Department { get; init; } = string.Empty;
    }
}
