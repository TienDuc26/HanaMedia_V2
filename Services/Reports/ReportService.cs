using System.Globalization;
using System.Text;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Reports;

public sealed class ReportService : IReportService
{
    private static readonly string[] ActiveEmployeeStatuses = ["dang_lam_viec", "thu_viec", "cho_duyet_nghi"];
    private readonly ApplicationDbContext _context;

    public ReportService(ApplicationDbContext context) => _context = context;

    public async Task<ReportPageViewModel?> GetAsync(
        string? reportType,
        string? period,
        int actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var type = NormalizeType(reportType, actorRole);
        if (!CanView(type, actorRole)) return null;

        var normalizedPeriod = NormalizePeriod(period);
        var (startAt, endAt, label) = GetPeriodRange(normalizedPeriod, DateTime.Now);
        var limited = actorRole is AppRoles.HumanResourcesStaff or AppRoles.BookingStaff or AppRoles.IdeaStaff;
        return new ReportPageViewModel
        {
            ReportType = type,
            Period = normalizedPeriod,
            PeriodLabel = label,
            IsDirector = actorRole == AppRoles.Director,
            IsLimited = limited,
            HumanResources = type == ReportTypes.HumanResources
                ? await GetHumanResourcesAsync(startAt, endAt, limited, cancellationToken)
                : null,
            Booking = type == ReportTypes.Booking
                ? await GetBookingAsync(startAt, endAt, actorUserId, limited, cancellationToken)
                : null,
            Ideas = type == ReportTypes.Ideas
                ? await GetIdeasAsync(startAt, endAt, actorUserId, limited, cancellationToken)
                : null
        };
    }

    public async Task<byte[]?> ExportCsvAsync(
        string? reportType,
        string? period,
        int actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var model = await GetAsync(reportType, period, actorUserId, actorRole, cancellationToken);
        if (model is null) return null;

        var csv = new StringBuilder();
        csv.AppendLine($"Bao cao,{Csv(model.ReportType)},{Csv(model.PeriodLabel)}");
        if (model.HumanResources is { } hr)
        {
            csv.AppendLine("Phong ban,So nhan su,Ty le");
            foreach (var row in hr.Departments) csv.AppendLine($"{Csv(row.Label)},{row.Count},{row.Percentage.ToString(CultureInfo.InvariantCulture)}%");
            if (!model.IsLimited)
            {
                csv.AppendLine();
                csv.AppendLine("ID,Nhan su,Phong ban,Tong task,Hoan thanh,Qua han,Ty le hoan thanh");
                foreach (var row in hr.EmployeePerformance) csv.AppendLine($"{row.EmployeeId},{Csv(row.EmployeeName)},{Csv(row.DepartmentName)},{row.TotalTasks},{row.CompletedTasks},{row.OverdueTasks},{row.CompletionRate.ToString(CultureInfo.InvariantCulture)}%");
            }
        }
        else if (model.Booking is { } booking)
        {
            csv.AppendLine(model.IsLimited
                ? "ID,Client va chien dich,KOL KOC,Trang thai,Deadline,Thu lao cua toi"
                : "ID,Client va chien dich,KOL KOC,Trang thai,Deadline,Doanh thu,Chi phi,Loi nhuan,Thu lao da phan bo");
            foreach (var row in booking.Rows)
            {
                csv.AppendLine(model.IsLimited
                    ? $"{row.Id},{Csv(row.ClientCampaign)},{Csv(row.KolName)},{Csv(row.StatusLabel)},{row.Deadline:yyyy-MM-dd},{row.MyWage.ToString(CultureInfo.InvariantCulture)}"
                    : $"{row.Id},{Csv(row.ClientCampaign)},{Csv(row.KolName)},{Csv(row.StatusLabel)},{row.Deadline:yyyy-MM-dd},{row.Revenue.ToString(CultureInfo.InvariantCulture)},{row.Cost.ToString(CultureInfo.InvariantCulture)},{(row.Revenue - row.Cost).ToString(CultureInfo.InvariantCulture)},{row.AllocatedWage.ToString(CultureInfo.InvariantCulture)}");
            }
        }
        else if (model.Ideas is { } ideas)
        {
            csv.AppendLine("ID,Y tuong,Client va chien dich,Nguoi phu trach,Trang thai,Duyet Giam doc,Deadline");
            foreach (var row in ideas.Rows) csv.AppendLine($"{row.Id},{Csv(row.Title)},{Csv(row.ClientCampaign)},{Csv(row.OwnerName)},{Csv(row.StatusLabel)},{Csv(row.DirectorStatusLabel)},{row.Deadline:yyyy-MM-dd}");
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(csv.ToString())];
    }

    private async Task<HumanResourcesReportViewModel> GetHumanResourcesAsync(
        DateTime startAt,
        DateTime endAt,
        bool limited,
        CancellationToken cancellationToken)
    {
        var startDate = DateOnly.FromDateTime(startAt);
        var endDate = DateOnly.FromDateTime(endAt);
        var departments = await _context.Departments.AsNoTracking().ToDictionaryAsync(item => item.Code, item => item.Name, cancellationToken);
        var employees = await _context.Employees.AsNoTracking().ToListAsync(cancellationToken);
        var active = employees.Where(item => ActiveEmployeeStatuses.Contains(item.Status ?? string.Empty)).ToList();
        var newCount = employees.Count(item => item.JoinedDate >= startDate && item.JoinedDate < endDate);
        var departedCount = employees.Count(item => item.Status == "ngung_hoat_dong" && item.UpdatedAt >= startAt && item.UpdatedAt < endAt);
        var departmentRows = Breakdown(active.GroupBy(item => item.Department).Select(group => (group.Key, Name(group.Key, departments), group.Count())), active.Count);
        var contractRows = Breakdown(active.GroupBy(item => item.ContractType).Select(group => (group.Key, ContractLabel(group.Key), group.Count())), active.Count);

        var performance = new List<EmployeePerformanceReportRowViewModel>();
        if (!limited)
        {
            var tasks = await _context.WorkTasks.AsNoTracking()
                .Include(item => item.AssignedEmployee)
                .Where(item => item.Deadline >= startAt && item.Deadline < endAt)
                .ToListAsync(cancellationToken);
            performance = tasks.GroupBy(item => item.AssignedEmployee)
                .Select(group => new EmployeePerformanceReportRowViewModel
                {
                    EmployeeId = group.Key.Id,
                    EmployeeName = group.Key.FullName,
                    DepartmentName = Name(group.Key.Department, departments),
                    TotalTasks = group.Count(),
                    CompletedTasks = group.Count(item => item.Status == WorkTaskStatuses.Done),
                    OverdueTasks = group.Count(item => item.Deadline < DateTime.Now && item.Status != WorkTaskStatuses.Done),
                    CompletionRate = Percent(group.Count(item => item.Status == WorkTaskStatuses.Done), group.Count())
                }).OrderByDescending(item => item.CompletionRate).ThenBy(item => item.EmployeeName).ToList();
        }

        return new HumanResourcesReportViewModel
        {
            TotalEmployees = active.Count,
            NewEmployees = newCount,
            DepartedEmployees = departedCount,
            RetentionRate = active.Count + departedCount == 0 ? 100 : Math.Round(active.Count * 100m / (active.Count + departedCount), 1),
            Departments = departmentRows,
            ContractTypes = contractRows,
            EmployeePerformance = performance
        };
    }

    private async Task<BookingReportViewModel> GetBookingAsync(
        DateTime startAt,
        DateTime endAt,
        int actorUserId,
        bool limited,
        CancellationToken cancellationToken)
    {
        var query = _context.Bookings.AsNoTracking()
            .Include(item => item.Campaign).Include(item => item.Kol)
            .Include(item => item.BookingWages).ThenInclude(item => item.Employee)
            .Where(item => item.CreatedAt >= startAt && item.CreatedAt < endAt);
        if (limited)
            query = query.Where(item => item.BookingWages.Any(wage => wage.Employee.UserId == actorUserId) || item.PrimaryManager!.UserId == actorUserId);
        var bookings = await query.OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        var valid = bookings.Where(item => item.Status != "huy").ToList();
        var rows = bookings.Select(item => new BookingReportRowViewModel
        {
            Id = item.Id,
            ClientCampaign = $"{item.ClientName} — {item.Campaign?.Name ?? item.CampaignName}",
            KolName = item.Kol?.Name ?? "Chưa chọn",
            StatusLabel = BookingStatusLabel(item.Status),
            Deadline = item.Deadline,
            Revenue = item.BookingPrice,
            Cost = item.ActualCost,
            AllocatedWage = item.BookingWages.Sum(wage => wage.AllocatedWage),
            MyWage = item.BookingWages.Where(wage => wage.Employee.UserId == actorUserId).Sum(wage => wage.AllocatedWage)
        }).ToList();
        return new BookingReportViewModel
        {
            TotalBookings = bookings.Count,
            CompletedBookings = bookings.Count(item => item.Status == "hoan_thanh"),
            OverdueBookings = bookings.Count(item => item.Deadline < DateOnly.FromDateTime(DateTime.Today) && item.Status is not ("hoan_thanh" or "huy")),
            Revenue = limited ? 0 : valid.Sum(item => item.BookingPrice),
            Cost = limited ? 0 : valid.Sum(item => item.ActualCost),
            MyWage = rows.Sum(item => item.MyWage),
            Statuses = Breakdown(bookings.GroupBy(item => item.Status ?? string.Empty).Select(group => (group.Key, BookingStatusLabel(group.Key), group.Count())), bookings.Count),
            Rows = rows
        };
    }

    private async Task<IdeaReportViewModel> GetIdeasAsync(
        DateTime startAt,
        DateTime endAt,
        int actorUserId,
        bool limited,
        CancellationToken cancellationToken)
    {
        var query = _context.Ideas.AsNoTracking()
            .Include(item => item.Campaign).Include(item => item.CreatorEmployee).Include(item => item.PrimaryStaff)
            .Where(item => item.CreatedAt >= startAt && item.CreatedAt < endAt);
        if (limited)
            query = query.Where(item => item.CreatorEmployee!.UserId == actorUserId || item.PrimaryStaff!.UserId == actorUserId);
        var ideas = await query.OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt).ToListAsync(cancellationToken);
        var approved = ideas.Count(item => item.Status is IdeaStatuses.Approved or IdeaStatuses.InProgress or IdeaStatuses.Done);
        var revisions = ideas.Count(item => item.Status == IdeaStatuses.NeedRevision || item.DirectorReviewStatus == DirectorIdeaReviewStatuses.RevisionRequested);
        var performance = limited ? [] : ideas.GroupBy(item => item.PrimaryStaff ?? item.CreatorEmployee)
            .Select(group => new IdeaPerformanceReportRowViewModel
            {
                EmployeeId = group.Key?.Id,
                EmployeeName = group.Key?.FullName ?? "Chưa phân công",
                TotalIdeas = group.Count(),
                ApprovedIdeas = group.Count(item => item.Status is IdeaStatuses.Approved or IdeaStatuses.InProgress or IdeaStatuses.Done),
                RevisionIdeas = group.Count(item => item.Status == IdeaStatuses.NeedRevision || item.DirectorReviewStatus == DirectorIdeaReviewStatuses.RevisionRequested),
                ApprovalRate = Percent(group.Count(item => item.Status is IdeaStatuses.Approved or IdeaStatuses.InProgress or IdeaStatuses.Done), group.Count())
            }).OrderByDescending(item => item.ApprovalRate).ThenBy(item => item.EmployeeName).ToList();
        return new IdeaReportViewModel
        {
            TotalIdeas = ideas.Count,
            ApprovedIdeas = approved,
            RevisionIdeas = revisions,
            PendingIdeas = ideas.Count(item => item.Status == IdeaStatuses.Review || (item.Status == IdeaStatuses.Approved && item.DirectorReviewStatus == DirectorIdeaReviewStatuses.Pending)),
            ApprovalRate = Percent(approved, ideas.Count),
            Statuses = Breakdown(ideas.GroupBy(item => item.Status ?? string.Empty).Select(group => (group.Key, IdeaStatuses.GetLabel(group.Key), group.Count())), ideas.Count),
            EmployeePerformance = performance,
            Rows = ideas.Select(item => new IdeaReportRowViewModel
            {
                Id = item.Id,
                Title = item.Title,
                ClientCampaign = $"{item.ClientName} — {item.Campaign?.Name ?? item.CampaignName ?? "Không có chiến dịch"}",
                OwnerName = item.PrimaryStaff?.FullName ?? item.CreatorEmployee?.FullName ?? "Chưa phân công",
                StatusLabel = IdeaStatuses.GetLabel(item.Status ?? IdeaStatuses.Idea),
                DirectorStatusLabel = DirectorIdeaReviewStatuses.GetLabel(item.DirectorReviewStatus),
                Deadline = item.Deadline
            }).ToList()
        };
    }

    private static List<ReportBreakdownRowViewModel> Breakdown(IEnumerable<(string Key, string Label, int Count)> source, int total)
        => source.OrderByDescending(item => item.Count).ThenBy(item => item.Label).Select(item => new ReportBreakdownRowViewModel
        {
            Key = item.Key,
            Label = item.Label,
            Count = item.Count,
            Percentage = Percent(item.Count, total)
        }).ToList();

    private static decimal Percent(int value, int total) => total == 0 ? 0 : Math.Round(value * 100m / total, 1);
    private static string Name(string code, IReadOnlyDictionary<string, string> names) => names.TryGetValue(code, out var name) ? name : code;
    private static string ContractLabel(string value) => value switch { "thu_viec" => "Thử việc", "chinh_thuc_1_nam" => "Chính thức 1 năm", "vo_thoi_han" => "Vô thời hạn", _ => value };
    private static string BookingStatusLabel(string? value) => value switch { "dang_cho" => "Đang chờ", "thuong_luong" => "Thương lượng", "da_chot" => "Đã chốt", "dang_trien_khai" => "Đang triển khai", "hoan_thanh" => "Hoàn thành", "huy" => "Đã hủy", _ => value ?? "Chưa xác định" };
    private static string NormalizePeriod(string? value) => value?.ToLowerInvariant() switch { "day" => "day", "quarter" => "quarter", _ => "month" };
    private static (DateTime Start, DateTime End, string Label) GetPeriodRange(string period, DateTime now)
    {
        if (period == "day") return (now.Date, now.Date.AddDays(1), "Hôm nay");
        if (period == "quarter") { var month = ((now.Month - 1) / 3) * 3 + 1; var start = new DateTime(now.Year, month, 1); return (start, start.AddMonths(3), $"Quý {(month - 1) / 3 + 1}/{now.Year}"); }
        var monthStart = new DateTime(now.Year, now.Month, 1); return (monthStart, monthStart.AddMonths(1), $"Tháng {now.Month}/{now.Year}");
    }
    private static string NormalizeType(string? value, string role)
    {
        if (value is ReportTypes.HumanResources or ReportTypes.Booking or ReportTypes.Ideas) return value;
        return role switch { AppRoles.BookingManager or AppRoles.BookingStaff => ReportTypes.Booking, AppRoles.IdeaManager or AppRoles.IdeaStaff => ReportTypes.Ideas, _ => ReportTypes.HumanResources };
    }
    private static bool CanView(string type, string role) => role == AppRoles.Director ||
        (type == ReportTypes.HumanResources && role is AppRoles.HumanResourcesManager or AppRoles.HumanResourcesStaff) ||
        (type == ReportTypes.Booking && role is AppRoles.BookingManager or AppRoles.BookingStaff) ||
        (type == ReportTypes.Ideas && role is AppRoles.IdeaManager or AppRoles.IdeaStaff);
    private static string Csv(string? value)
    {
        var safe = value ?? string.Empty;
        if (safe.Length > 0 && "=+-@".Contains(safe[0])) safe = "'" + safe;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
}
