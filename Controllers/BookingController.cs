using System.Data;
using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.Services.Config;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Controllers;

[Authorize(Roles = AppRoles.Director + "," + AppRoles.BookingManager + "," + AppRoles.BookingStaff)]
public class BookingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ISystemAuditService _auditService;
    private readonly IBusinessConfigService _businessConfigService;
    public BookingController(ApplicationDbContext context, ISystemAuditService auditService, IBusinessConfigService businessConfigService)
    { _context = context; _auditService = auditService; _businessConfigService = businessConfigService; }
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? "";
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private int EmployeeId => _context.Employees.Where(e => e.UserId == UserId).Select(e => e.Id).FirstOrDefault();
    private bool Owns(Booking b) => Role == AppRoles.BookingManager && EmployeeId > 0 && b.PrimaryManagerId == EmployeeId;
    private IQueryable<Booking> Query() => _context.Bookings.Include(b => b.Campaign).Include(b => b.Kol)
        .Include(b => b.BookingKols).ThenInclude(k => k.Kol).Include(b => b.PrimaryManager)
        .Include(b => b.BookingWages).ThenInclude(w => w.Employee).Include(b => b.Payments);
    private IQueryable<Booking> Visible() => Role == AppRoles.BookingStaff
        ? Query().Where(b => b.BookingWages.Any(w => w.EmployeeId == EmployeeId)) : Query();
    private void Audit(Booking b, string action, string detail) => _auditService.AddEvent(
        new AuditEvent(AuditModules.Booking, action, detail, UserId, "Booking", b.Id.ToString()));
    private IActionResult Result(int id, bool success, string message)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("json"))
            return Json(new { success, message });
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return id > 0 ? RedirectToAction(nameof(Details), new { id }) : RedirectToAction(nameof(Index));
    }
    private async Task<IActionResult> Persist(Booking b, string action, string message, CancellationToken ct)
    {
        b.UpdatedAt = DateTime.Now;
        Audit(b, action, message);
        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result(b.Id, false, "Dữ liệu vừa được thay đổi. Tải lại trang rồi thử lại."); }
        return Result(b.Id, true, message);
    }

    [HttpGet("Bookings")]
    public async Task<IActionResult> Index(string? search, int? campaignId, int? kolId, string? status, int? managerId, int? participantId, CancellationToken cancellationToken)
    {
        var query = Visible();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(b => b.ClientName.Contains(search) || b.CampaignName.Contains(search) || (b.Notes != null && b.Notes.Contains(search)));
        if (campaignId.HasValue) query = query.Where(b => b.CampaignId == campaignId);
        if (kolId.HasValue) query = query.Where(b => b.BookingKols.Any(k => k.KolId == kolId) || b.KolId == kolId);
        if (managerId.HasValue) query = query.Where(b => b.PrimaryManagerId == managerId);
        if (participantId.HasValue) query = query.Where(b => b.BookingWages.Any(w => w.EmployeeId == participantId));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(b => b.Status == status);
        ViewBag.Campaigns = await _context.Campaigns.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        ViewBag.Kols = await _context.Kols.Where(k => k.IsActive).OrderBy(k => k.Name).ToListAsync(cancellationToken);
        ViewBag.Employees = await _context.Employees.Include(e => e.User).OrderBy(e => e.FullName).ToListAsync(cancellationToken);
        ViewBag.Search = search; ViewBag.CampaignId = campaignId; ViewBag.KolId = kolId; ViewBag.Status = status;
        ViewBag.ManagerId = managerId; ViewBag.ParticipantId = participantId; ViewBag.EmployeeId = EmployeeId;
        ViewBag.IsWritable = Role == AppRoles.BookingManager;
        ViewBag.FinanceConfig = await _businessConfigService.GetAsync(cancellationToken);
        ViewBag.Participants = await EligibleParticipants().OrderBy(e => e.FullName).ToListAsync(cancellationToken);
        return View(await query.OrderByDescending(b => b.CreatedAt).ToListAsync(cancellationToken));
    }

    [HttpGet("Bookings/Details/{id}")]
    [HttpGet("Booking/Details/{id}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var booking = await Visible().Include(b => b.ContractApprovedBy).Include(b => b.ContractSignedBy).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (booking == null) return NotFound();
        if (Role == AppRoles.BookingStaff) booking.BookingWages = booking.BookingWages.Where(w => w.EmployeeId == EmployeeId).ToList();
        ViewBag.IsWritable = Owns(booking); ViewBag.Role = Role;
        ViewBag.CanDraft = Owns(booking) || Role == AppRoles.BookingStaff;
        ViewBag.Participants = await EligibleParticipants().OrderBy(e => e.FullName).ToListAsync(cancellationToken);
        ViewBag.WagePool = booking.FinanceVersion == 1 ? booking.CommissionPool : decimal.Round(booking.BookingPrice * (await _businessConfigService.GetAsync(cancellationToken)).CommissionPercent / 100m, 2);
        return View(booking);
    }

    [HttpPost("Bookings/Create"), ValidateAntiForgeryToken]
    public Task<IActionResult> Create(Booking input, int[] kolIds, IFormFile? acceptanceFile, string deadlineStr, string? postingDateStr, [Bind(Prefix = "wages")] Dictionary<int, decimal> wages, CancellationToken cancellationToken)
        => Save(0, input, kolIds, acceptanceFile, deadlineStr, postingDateStr, cancellationToken, wages);
    [HttpPost("Bookings/Edit/{id}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id, Booking input, int[] kolIds, IFormFile? acceptanceFile, string deadlineStr, string? postingDateStr, CancellationToken cancellationToken)
        => Save(id, input, kolIds, acceptanceFile, deadlineStr, postingDateStr, cancellationToken);

    private IQueryable<Employee> EligibleParticipants() => _context.Employees.Where(e =>
        e.Status == "dang_lam_viec" || e.Status == "thu_viec");

    private async Task<string?> ValidateWages(Dictionary<int, decimal> wages, decimal pool, CancellationToken ct)
    {
        if (ModelState.Any(x => x.Key.StartsWith("wages", StringComparison.OrdinalIgnoreCase) && x.Value!.Errors.Count > 0))
            return "Số tiền phân bổ không hợp lệ.";
        var ids = wages.Keys.ToArray();
        if (wages.Values.Any(v => v < 0 || v > 9999999999999.99m || decimal.Round(v, 2) != v) ||
            await EligibleParticipants().CountAsync(e => ids.Contains(e.Id), ct) != ids.Length)
            return "Chọn nhân viên đang làm việc hoặc thử việc ở bất kỳ phòng ban; số tiền không âm, tối đa 2 chữ số thập phân.";
        return wages.Values.Sum() > pool ? "Tổng thù lao không được vượt quỹ hoa hồng QL Booking." : null;
    }

    private async Task<IActionResult> Save(int id, Booking input, int[] kolIds, IFormFile? acceptanceFile, string deadlineStr, string? postingDateStr, CancellationToken ct, Dictionary<int, decimal>? wages = null)
    {
        if (Role != AppRoles.BookingManager || EmployeeId == 0) return Forbid();
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var b = id == 0 ? new Booking() : await Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b == null) return NotFound();
        if (id != 0 && !Owns(b)) return Forbid();
        if (id != 0 && (b.ContractStatus is not ("nhap" or "tu_choi") || b.Payments.Any(p => p.IsPaid)))
            return Result(id, false, "Booking đang duyệt/đã duyệt đã khóa thông tin. Không được thay đổi nội dung hợp đồng đã trình.");
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == input.CampaignId && c.Status == "running" && c.ConfirmedAt != null, ct);
        if (campaign == null) return Result(id, false, "Chỉ chọn chiến dịch đang chạy.");
        kolIds = kolIds.Distinct().Order().ToArray();
        if (kolIds.Length == 0 || await _context.Kols.CountAsync(k => kolIds.Contains(k.Id) && k.IsActive, ct) != kolIds.Length)
            return Result(id, false, "Chọn ít nhất một KOL/KOC đang hoạt động.");
        if (input.BookingPrice <= 0 || input.BookingPrice > 9999999999999.99m || !DateOnly.TryParse(deadlineStr, out var deadline))
            return Result(id, false, "Giá Booking hoặc deadline không hợp lệ.");
        if (!new[] { "dang_cho", "thuong_luong", "da_chot", "huy" }.Contains(input.Status))
            return Result(id, false, "Trạng thái Booking không hợp lệ.");
        if (input.JobDescription?.Length > 4000 || input.Notes?.Length > 4000 || input.PostLink?.Length > 255)
            return Result(id, false, "Nội dung/ghi chú tối đa 4.000 ký tự, link tối đa 255 ký tự.");
        var config = await _businessConfigService.GetAsync(ct);
        if (b.FinanceVersion == 0)
        { b.CompanyPercent = config.CompanyPercent; b.CommissionPercent = config.CommissionPercent; b.CastPercent = config.CastPercent; }
        b.BookingPrice = decimal.Round(input.BookingPrice, 2, MidpointRounding.AwayFromZero);
        if (id == 0 && wages != null)
        {
            var error = await ValidateWages(wages, b.CommissionPool, ct);
            if (error != null) return Result(id, false, error);
            foreach (var wage in wages)
                b.BookingWages.Add(new BookingWage { EmployeeId = wage.Key, AllocatedWage = wage.Value, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now });
        }
        if (b.BookingWages.Sum(w => w.AllocatedWage) > b.CommissionPool)
            return Result(id, false, "Thù lao cũ vượt quỹ hoa hồng. Điều chỉnh phân bổ trước khi chuyển Booking sang công thức mới.");
        if (acceptanceFile != null)
        {
            var path = await SaveDocument(acceptanceFile, "acceptance", ct);
            if (path == null) return Result(id, false, "Tài liệu phải là PDF/DOCX/JPG/PNG, tối đa 25 MB.");
            b.AcceptanceFileUrl = path;
        }
        b.FinanceVersion = 1; b.ActualCost = b.CommissionPool + b.CastPool;
        b.CampaignId = campaign.Id; b.CampaignName = campaign.Name; b.ClientName = campaign.Client;
        b.PrimaryManagerId = EmployeeId; b.KolId = kolIds[0]; b.Deadline = deadline;
        b.PostingDate = DateOnly.TryParse(postingDateStr, out var posted) ? posted : null;
        if (!string.IsNullOrWhiteSpace(input.PostLink) && (!Uri.TryCreate(input.PostLink, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            return Result(id, false, "Link bài đăng phải là HTTP/HTTPS.");
        b.JobDescription = input.JobDescription; b.Notes = input.Notes; b.PostLink = input.PostLink; b.Status = input.Status;
        foreach (var removed in b.BookingKols.Where(k => !kolIds.Contains(k.KolId)).ToList()) _context.BookingKols.Remove(removed);
        var share = decimal.Floor(b.CastPool / kolIds.Length * 100m) / 100m;
        foreach (var k in kolIds)
        {
            var row = b.BookingKols.FirstOrDefault(x => x.KolId == k);
            if (row == null) { row = new BookingKol { KolId = k }; b.BookingKols.Add(row); }
            row.CastAmount = k == kolIds[^1] ? b.CastPool - share * (kolIds.Length - 1) : share;
        }
        if (id == 0) { b.ContractStatus = "nhap"; b.CreatedAt = DateTime.Now; _context.Bookings.Add(b); }
        b.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(ct);
        Audit(b, id == 0 ? AuditActions.Created : AuditActions.Updated, $"Lưu Booking #{b.Id}; phân bổ {b.CompanyPercent}/{b.CommissionPercent}/{b.CastPercent}, {kolIds.Length} KOL.");
        if (id == 0 && b.BookingWages.Count > 0)
            Audit(b, AuditActions.WageChanged, "Phân bổ khi tạo Booking: " + string.Join("; ", b.BookingWages.Select(w => $"NV #{w.EmployeeId}: {w.AllocatedWage:N2} đ")));
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result(b.Id, true, "Đã lưu Booking. Có thể quản lý nhân viên và phân bổ tại trang chi tiết.");
    }

    private async Task<string?> SaveDocument(IFormFile file, string kind, CancellationToken ct)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length <= 0 || file.Length > 25 * 1024 * 1024 || !new[] { ".pdf", ".docx", ".jpg", ".jpeg", ".png" }.Contains(extension)) return null;
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "booking-documents");
        Directory.CreateDirectory(folder);
        var name = $"{kind}_{Guid.NewGuid():N}{extension}";
        await using var stream = System.IO.File.Create(Path.Combine(folder, name));
        await file.CopyToAsync(stream, ct);
        return "/BookingDocuments/" + name;
    }

    [HttpPost("Bookings/Delete/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var b = await Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (b == null) return NotFound();
        if (!Owns(b)) return Forbid();
        if (b.ContractStatus is not ("nhap" or "tu_choi") || b.Payments.Any(p => p.IsPaid)) return Result(id, false, "Không thể hủy Booking đã trình duyệt hoặc đã thanh toán.");
        b.Status = "huy";
        return await Persist(b, AuditActions.Deleted, "Đã hủy Booking; giữ lịch sử.", cancellationToken);
    }

    [HttpPost("Bookings/UpdateWages/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateWages(int id, [Bind(Prefix = "wages")] Dictionary<int, decimal> wages, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Result(id, false, "Số tiền phân bổ không hợp lệ.");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var b = await Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (b == null) return NotFound();
        if (!Owns(b)) return Forbid();
        if (b.Status == "huy" || b.Payments.Any(p => p.IsPaid)) return Result(id, false, "Đã có thanh toán hoặc Booking đã hủy, không thể đổi phân bổ.");
        var ids = wages.Keys.ToArray();
        var pool = b.FinanceVersion == 1 ? b.CommissionPool : decimal.Round(b.BookingPrice * (await _businessConfigService.GetAsync(cancellationToken)).CommissionPercent / 100m, 2);
        var wageError = await ValidateWages(wages, pool, cancellationToken);
        if (wageError != null) return Result(id, false, wageError);
        foreach (var employeeId in ids.Union(b.BookingWages.Select(w => w.EmployeeId)))
        {
            var old = b.BookingWages.FirstOrDefault(w => w.EmployeeId == employeeId)?.AllocatedWage;
            var next = wages.TryGetValue(employeeId, out var amount) ? amount : (decimal?)null;
            if (old != next) Audit(b, AuditActions.WageChanged,
                $"Nhân viên #{employeeId}: {old?.ToString("N2") ?? "chưa tham gia"} → {next?.ToString("N2") ?? "gỡ khỏi Booking"}.");
        }
        foreach (var removed in b.BookingWages.Where(w => !ids.Contains(w.EmployeeId)).ToList()) _context.BookingWages.Remove(removed);
        foreach (var item in wages)
        {
            var row = b.BookingWages.FirstOrDefault(w => w.EmployeeId == item.Key);
            if (row == null) { row = new BookingWage { EmployeeId = item.Key, CreatedAt = DateTime.Now }; b.BookingWages.Add(row); }
            row.AllocatedWage = item.Value; row.UpdatedAt = DateTime.Now;
        }
        var result = await Persist(b, AuditActions.WageChanged, $"Đã phân bổ {wages.Values.Sum():N0} đ cho {ids.Length} NV; phần còn lại thuộc QL phụ trách.", cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    [HttpPost("Bookings/SubmitApproval/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitApproval(int id, CancellationToken cancellationToken)
    {
        var b = await Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (b == null) return NotFound();
        if (!Owns(b)) return Forbid();
        if (b.ContractStatus is not ("nhap" or "tu_choi") || b.Status == "huy") return Result(id, false, "Chỉ gửi Booking nháp hoặc bị từ chối.");
        if (b.FinanceVersion != 1 || string.IsNullOrWhiteSpace(b.JobDescription) || b.BookingKols.Count == 0)
            return Result(id, false, "Cần cập nhật đầy đủ nội dung, KOL và công thức tài chính trước khi gửi duyệt.");
        b.ContractStatus = "cho_duyet"; b.RejectionReason = null;
        return await Persist(b, "submitted", "Đã gửi đơn phê duyệt Booking lên Giám đốc.", cancellationToken);
    }

    [HttpPost("Bookings/Approve/{id}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(int id, CancellationToken cancellationToken) => DirectorDecision(id, true, null, cancellationToken);
    [HttpPost("Bookings/Reject/{id}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(int id, string? rejectionReason, CancellationToken cancellationToken) => DirectorDecision(id, false, rejectionReason, cancellationToken);
    private async Task<IActionResult> DirectorDecision(int id, bool approve, string? reason, CancellationToken ct)
    {
        if (Role != AppRoles.Director) return Forbid();
        var b = await Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b == null) return NotFound();
        if (b.ContractStatus != "cho_duyet" || b.Status == "huy") return Result(id, false, "Booking không ở bước chờ duyệt.");
        if (!approve && (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)) return Result(id, false, "Nhập lý do từ chối, tối đa 1.000 ký tự.");
        b.ContractStatus = approve ? "da_duyet" : "tu_choi"; b.RejectionReason = reason;
        if (approve) { b.ContractApprovedAt = DateTime.Now; b.ContractApprovedById = EmployeeId == 0 ? null : EmployeeId; }
        return await Persist(b, approve ? AuditActions.Approved : AuditActions.Rejected, approve ? "Giám đốc đã duyệt; chờ soạn hợp đồng." : "Giám đốc từ chối: " + reason, ct);
    }

    [HttpPost("Bookings/UploadContract/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadContract(int id, IFormFile? contractFile, CancellationToken cancellationToken)
    {
        var b = await Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (b == null) return NotFound();
        if (!Owns(b) && !(Role == AppRoles.BookingStaff && b.BookingWages.Any(w => w.EmployeeId == EmployeeId))) return Forbid();
        if (b.ContractStatus is not ("da_duyet" or "phap_ly_tu_choi")) return Result(id, false, "Chỉ soạn/sửa hợp đồng sau duyệt hoặc khi Pháp lý trả sửa.");
        var path = contractFile == null ? null : await SaveDocument(contractFile, "contract", cancellationToken);
        if (path == null) return Result(id, false, "Chọn hợp đồng PDF/DOCX/JPG/PNG tối đa 25 MB.");
        b.ContractFileUrl = path; b.ContractRevision++; b.LegalApprovedRevision = null;
        b.ContractStatus = "cho_phap_ly";
        return await Persist(b, "contract_submitted_legal", $"Đã gửi hợp đồng phiên bản {b.ContractRevision} cho Pháp lý kiểm tra.", cancellationToken);
    }

    [HttpPost("Bookings/SignContract/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SignContract(int id, bool confirmed, CancellationToken cancellationToken)
    {
        if (Role != AppRoles.Director) return Forbid();
        var b = await Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (b == null) return NotFound();
        if (!confirmed) return Result(id, false, "Cần tích xác nhận đã kiểm tra hợp đồng trước khi ký.");
        if (b.ContractStatus != "cho_ky" || b.LegalApprovedRevision != b.ContractRevision || b.LegalReviewedAt == null || string.IsNullOrEmpty(b.ContractFileUrl))
            return Result(id, false, "Pháp lý phải duyệt đúng phiên bản hợp đồng trước khi ký.");
        b.ContractStatus = "da_ky"; b.ContractSignedAt = DateTime.Now; b.ContractSignedById = EmployeeId == 0 ? null : EmployeeId;
        return await Persist(b, AuditActions.ContractSigned, "Giám đốc đã xác nhận ký hợp đồng.", cancellationToken);
    }

    [HttpPost("Bookings/Acceptance/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Acceptance(int id, IFormFile? acceptanceFile, string? postLink, CancellationToken ct)
    {
        var b = await Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b == null) return NotFound();
        if (!Owns(b)) return Forbid();
        if (b.ContractStatus != "da_ky" || b.Status == "huy") return Result(id, false, "Chỉ nghiệm thu sau khi hợp đồng đã ký.");
        if (postLink?.Length > 255) return Result(id, false, "Link tối đa 255 ký tự.");
        if (!string.IsNullOrWhiteSpace(postLink) && (!Uri.TryCreate(postLink, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            return Result(id, false, "Link phải là HTTP/HTTPS.");
        var path = acceptanceFile == null ? null : await SaveDocument(acceptanceFile, "acceptance", ct);
        if (path == null) return Result(id, false, "Chọn bản nghiệm thu hợp lệ.");
        b.AcceptanceFileUrl = path; b.PostLink = postLink; b.PostingDate = DateOnly.FromDateTime(DateTime.Today); b.Status = "hoan_thanh";
        return await Persist(b, "accepted", "Đã nghiệm thu và hoàn thành Booking.", ct);
    }

    [HttpPost("Bookings/Start/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id, CancellationToken ct)
    {
        var b = await Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b == null) return NotFound();
        if (!Owns(b)) return Forbid();
        if (b.ContractStatus != "da_ky" || b.Status is "huy" or "hoan_thanh")
            return Result(id, false, "Chỉ triển khai Booking đã ký, chưa hủy/hoàn thành.");
        b.Status = "dang_trien_khai";
        return await Persist(b, AuditActions.Updated, "Đã bắt đầu triển khai Booking.", ct);
    }

        [HttpGet("Bookings/Dashboard")]
        public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
        {
            if (Role is not (AppRoles.Director or AppRoles.BookingManager))
            {
                return Forbid();
            }

            var bookings = await _context.Bookings
                .Include(b => b.PrimaryManager)
                .ToListAsync(cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.Today);

            // 1. Count by Status
            var statuses = new Dictionary<string, int>
            {
                { "dang_cho", bookings.Count(b => b.Status == "dang_cho") },
                { "thuong_luong", bookings.Count(b => b.Status == "thuong_luong") },
                { "da_chot", bookings.Count(b => b.Status == "da_chot") },
                { "dang_trien_khai", bookings.Count(b => b.Status == "dang_trien_khai") },
                { "hoan_thanh", bookings.Count(b => b.Status == "hoan_thanh") },
                { "huy", bookings.Count(b => b.Status == "huy") }
            };
            ViewBag.Statuses = statuses;

            // 2. Financial Metrics
            decimal revenue = bookings.Where(b => b.Status != "huy").Sum(b => b.BookingPrice);
            decimal cost = bookings.Where(b => b.Status != "huy").Sum(b => b.ActualCost);
            decimal profit = revenue - cost;

            ViewBag.Revenue = revenue;
            ViewBag.Cost = cost;
            ViewBag.Profit = profit;

            // 3. Overdue Bookings (Deadline passed, status is not hoan_thanh or huy)
            var overdueBookings = bookings
                .Where(b => b.Deadline < today && b.Status != "hoan_thanh" && b.Status != "huy")
                .OrderBy(b => b.Deadline)
                .ToList();
            ViewBag.OverdueBookings = overdueBookings;

            // 4. Employee Performance (Group by PrimaryManagerId)
            var managerStats = bookings
                .Where(b => b.PrimaryManagerId.HasValue)
                .GroupBy(b => b.PrimaryManager)
                .Select(g => new ManagerPerformanceViewModel
                {
                    ManagerName = g.Key?.FullName ?? "—",
                    Position = g.Key?.Position ?? "—",
                    CompletedCount = g.Count(b => b.Status == "hoan_thanh"),
                    RunningCount = g.Count(b => b.Status == "dang_trien_khai"),
                    TotalCount = g.Count()
                })
                .OrderByDescending(x => x.CompletedCount)
                .ToList();
            ViewBag.ManagerStats = managerStats;

            return View();
        }


}
    public class ManagerPerformanceViewModel
    {
        public string ManagerName { get; set; } = null!;
        public string Position { get; set; } = null!;
        public int CompletedCount { get; set; }
        public int RunningCount { get; set; }
        public int TotalCount { get; set; }
    }
