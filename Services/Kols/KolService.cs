using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Kols;

public sealed class KolService : IKolService
{
    private const int PageSize = 20;
    private static readonly string[] ActiveEmployeeStatuses = ["dang_lam_viec", "thu_viec"];
    private readonly ApplicationDbContext _context;
    private readonly ISystemAuditService _auditService;

    public KolService(ApplicationDbContext context, ISystemAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<KolPageViewModel> GetPageAsync(
        int actorUserId, string actorRole, string? search, string? platform, string? status, int page,
        CancellationToken cancellationToken = default)
    {
        if (!KolPermissions.CanView(actorRole))
            throw new UnauthorizedAccessException("Tài khoản không có quyền xem dữ liệu KOL/KOC.");

        var normalizedSearch = search?.Trim();
        var normalizedPlatform = KolPlatforms.IsValid(platform) ? platform : null;
        var normalizedStatus = KolStatuses.IsValid(status) ? status : null;
        page = Math.Max(1, page);
        var query = _context.Kols.AsNoTracking()
            .Include(item => item.ResponsibleStaff)
            .Include(item => item.Bookings)
                .ThenInclude(item => item.Campaign)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(item =>
                EF.Functions.Like(item.Name, pattern) ||
                EF.Functions.Like(item.Niche, pattern) ||
                EF.Functions.Like(item.Location, pattern));
        }
        if (normalizedPlatform is not null) query = query.Where(item => item.Platform == normalizedPlatform);
        if (normalizedStatus is not null) query = query.Where(item => item.Status == normalizedStatus);

        var total = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Min(page, totalPages);
        var entities = await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var currentEmployeeId = await _context.Employees.AsNoTracking()
            .Where(item => item.UserId == actorUserId)
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var canManageAll = KolPermissions.CanManageAll(actorRole);
        var staffOptions = await _context.Employees.AsNoTracking()
            .Where(item => item.Department == "Booking" && item.UserId.HasValue && ActiveEmployeeStatuses.Contains(item.Status!))
            .OrderBy(item => item.FullName)
            .Select(item => new KolResponsibleStaffOptionViewModel(item.Id, item.FullName))
            .ToListAsync(cancellationToken);

        return new KolPageViewModel
        {
            Search = normalizedSearch,
            Platform = normalizedPlatform,
            Status = normalizedStatus,
            Page = page,
            TotalPages = totalPages,
            CanCreate = KolPermissions.CanCreate(actorRole) && (canManageAll || currentEmployeeId.HasValue),
            CanManageAll = canManageAll,
            ResponsibleStaff = staffOptions,
            Kols = entities.Select(item => new KolListItemViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Platform = item.Platform,
                ProfileLink = item.ProfileLink,
                FollowersCount = item.FollowersCount,
                EngagementRate = item.EngagementRate,
                Niche = item.Niche,
                BookingPrice = item.BookingPrice,
                Location = item.Location,
                ContactInfo = item.ContactInfo,
                ResponsibleStaffId = item.ResponsibleStaffId,
                ResponsibleStaffName = item.ResponsibleStaff?.FullName ?? "Chưa phân công",
                RatingScore = item.RatingScore,
                Status = item.Status ?? KolStatuses.Potential,
                StatusLabel = KolStatuses.GetLabel(item.Status ?? KolStatuses.Potential),
                CanEdit = canManageAll || (currentEmployeeId.HasValue && item.ResponsibleStaffId == currentEmployeeId),
                CanDelete = canManageAll && item.Bookings.Count == 0,
                RowVersion = Convert.ToBase64String(item.RowVersion),
                BookingHistory = item.Bookings
                    .OrderByDescending(booking => booking.CreatedAt)
                    .Select(booking => new KolBookingHistoryViewModel(
                        booking.Id,
                        booking.ClientName,
                        booking.Campaign?.Name ?? booking.CampaignName,
                        booking.BookingPrice,
                        booking.Status ?? "dang_cho",
                        GetBookingStatusLabel(booking.Status),
                        booking.Deadline))
                    .ToList()
            }).ToList()
        };
    }

    public async Task<KolOperationResult> CreateAsync(
        CreateKolInputModel input, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (!KolPermissions.CanCreate(actorRole)) return KolOperationResult.Failure("Tài khoản không có quyền thêm KOL/KOC.");
        var validation = Validate(input);
        if (validation is not null) return KolOperationResult.Failure(validation);
        Normalize(input);
        if (await HasDuplicateAsync(input.Platform, input.ProfileLink, null, cancellationToken))
            return KolOperationResult.Failure("Tài khoản KOL/KOC này đã tồn tại trên nền tảng đã chọn.");

        var responsible = await ResolveResponsibleStaffAsync(input.ResponsibleStaffId, actorUserId, actorRole, cancellationToken);
        if (responsible is null)
            return KolOperationResult.Failure("Người phụ trách phải là nhân sự Booking đang hoạt động và đã có tài khoản.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var kol = new Kol
        {
            Name = input.Name,
            Platform = input.Platform,
            ProfileLink = input.ProfileLink,
            FollowersCount = input.FollowersCount,
            EngagementRate = input.EngagementRate,
            Niche = input.Niche,
            BookingPrice = input.BookingPrice,
            Location = input.Location,
            ContactInfo = input.ContactInfo,
            ResponsibleStaffId = responsible.Id,
            RatingScore = input.RatingScore,
            Status = input.Status,
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.Kols.Add(kol);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            _auditService.AddEvent(new AuditEvent(AuditModules.Booking, "kol_created",
                $"Thêm {kol.Platform} KOL/KOC '{kol.Name}', phụ trách: {responsible.FullName}.",
                actorUserId, "Kol", kol.Id.ToString()));
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return KolOperationResult.Success("Đã thêm KOL/KOC vào database.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return KolOperationResult.Failure("Không thể thêm KOL/KOC. Hãy kiểm tra link tài khoản và dữ liệu nhập.");
        }
    }

    public async Task<KolOperationResult> UpdateAsync(
        UpdateKolInputModel input, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (!KolPermissions.CanCreate(actorRole)) return KolOperationResult.Failure("Tài khoản không có quyền cập nhật KOL/KOC.");
        var validation = Validate(input);
        if (validation is not null) return KolOperationResult.Failure(validation);
        if (!TryParseVersion(input.RowVersion, out var expectedVersion))
            return KolOperationResult.Failure("Phiên bản dữ liệu không hợp lệ. Hãy tải lại trang.");
        Normalize(input);

        var kol = await _context.Kols
            .Include(item => item.ResponsibleStaff)
            .FirstOrDefaultAsync(item => item.Id == input.Id, cancellationToken);
        if (kol is null) return KolOperationResult.Failure("Không tìm thấy KOL/KOC.");
        var currentEmployeeId = await GetCurrentEmployeeIdAsync(actorUserId, cancellationToken);
        var canManageAll = KolPermissions.CanManageAll(actorRole);
        if (!canManageAll && (!currentEmployeeId.HasValue || kol.ResponsibleStaffId != currentEmployeeId))
            return KolOperationResult.Failure("Nhân viên chỉ được cập nhật KOL/KOC do mình phụ trách.");
        if (await HasDuplicateAsync(input.Platform, input.ProfileLink, input.Id, cancellationToken))
            return KolOperationResult.Failure("Tài khoản KOL/KOC này đã tồn tại trên nền tảng đã chọn.");

        Employee? responsible;
        if (canManageAll)
        {
            responsible = await ResolveResponsibleStaffAsync(input.ResponsibleStaffId, actorUserId, actorRole, cancellationToken);
            if (responsible is null)
                return KolOperationResult.Failure("Người phụ trách phải là nhân sự Booking đang hoạt động và đã có tài khoản.");
        }
        else
        {
            responsible = kol.ResponsibleStaff;
        }

        _context.Entry(kol).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        var oldStatus = kol.Status ?? KolStatuses.Potential;
        kol.Name = input.Name;
        kol.Platform = input.Platform;
        kol.ProfileLink = input.ProfileLink;
        kol.FollowersCount = input.FollowersCount;
        kol.EngagementRate = input.EngagementRate;
        kol.Niche = input.Niche;
        kol.BookingPrice = input.BookingPrice;
        kol.Location = input.Location;
        kol.ContactInfo = input.ContactInfo;
        kol.ResponsibleStaffId = responsible!.Id;
        kol.RatingScore = canManageAll ? input.RatingScore : kol.RatingScore;
        kol.Status = input.Status;
        kol.UpdatedAt = DateTime.UtcNow;
        _auditService.AddEvent(new AuditEvent(AuditModules.Booking, "kol_updated",
            $"Cập nhật KOL/KOC '{kol.Name}'" +
            (oldStatus == kol.Status ? "." : $": {KolStatuses.GetLabel(oldStatus)} → {KolStatuses.GetLabel(kol.Status)}."),
            actorUserId, "Kol", kol.Id.ToString()));
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return KolOperationResult.Success("Đã cập nhật KOL/KOC.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return KolOperationResult.Failure("KOL/KOC vừa được người khác cập nhật. Hãy tải lại trang.");
        }
        catch (DbUpdateException)
        {
            return KolOperationResult.Failure("Không thể cập nhật KOL/KOC. Hãy kiểm tra lại dữ liệu.");
        }
    }

    public async Task<KolOperationResult> DeleteAsync(
        DeleteKolInputModel input, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (!KolPermissions.CanManageAll(actorRole)) return KolOperationResult.Failure("Chỉ Quản lý Booking được xóa KOL/KOC.");
        if (!TryParseVersion(input.RowVersion, out var expectedVersion))
            return KolOperationResult.Failure("Phiên bản dữ liệu không hợp lệ. Hãy tải lại trang.");
        var kol = await _context.Kols.Include(item => item.Bookings)
            .FirstOrDefaultAsync(item => item.Id == input.Id, cancellationToken);
        if (kol is null) return KolOperationResult.Failure("Không tìm thấy KOL/KOC.");
        if (kol.Bookings.Count > 0) return KolOperationResult.Failure("Không thể xóa KOL/KOC đã có lịch sử Booking.");

        _context.Entry(kol).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        _context.Kols.Remove(kol);
        _auditService.AddEvent(new AuditEvent(AuditModules.Booking, "kol_deleted",
            $"Xóa KOL/KOC '{kol.Name}' trên {kol.Platform}.", actorUserId, "Kol", kol.Id.ToString(), AuditSeverity.Warning));
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return KolOperationResult.Success("Đã xóa KOL/KOC.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return KolOperationResult.Failure("KOL/KOC vừa được người khác cập nhật. Hãy tải lại trang.");
        }
    }

    private async Task<Employee?> ResolveResponsibleStaffAsync(int? requestedId, int actorUserId, string actorRole, CancellationToken token)
    {
        var employeeId = KolPermissions.CanManageAll(actorRole)
            ? requestedId
            : await GetCurrentEmployeeIdAsync(actorUserId, token);
        if (!employeeId.HasValue) return null;
        return await _context.Employees.FirstOrDefaultAsync(item =>
            item.Id == employeeId && item.Department == "Booking" && item.UserId.HasValue &&
            ActiveEmployeeStatuses.Contains(item.Status!), token);
    }

    private async Task<int?> GetCurrentEmployeeIdAsync(int actorUserId, CancellationToken token)
        => await _context.Employees.AsNoTracking()
            .Where(item => item.UserId == actorUserId)
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync(token);

    private async Task<bool> HasDuplicateAsync(string platform, string profileLink, int? excludedId, CancellationToken token)
        => await _context.Kols.AnyAsync(item => item.Platform == platform && item.ProfileLink == profileLink &&
            (!excludedId.HasValue || item.Id != excludedId.Value), token);

    private static string? Validate(KolInputModel input)
    {
        if ((input.Name?.Trim().Length ?? 0) is < 2 or > 100) return "Tên KOL/KOC phải có từ 2 đến 100 ký tự.";
        if (!KolPlatforms.IsValid(input.Platform)) return "Nền tảng không hợp lệ.";
        if (!Uri.TryCreate(input.ProfileLink?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return "Link tài khoản phải là URL http/https hợp lệ.";
        if (input.FollowersCount < 0) return "Số follower không được âm.";
        if (input.EngagementRate is < 0 or > 100) return "Tỉ lệ tương tác phải nằm trong khoảng 0–100%.";
        if ((input.Niche?.Trim().Length ?? 0) is < 2 or > 100) return "Chủ đề/niche phải có từ 2 đến 100 ký tự.";
        if (input.BookingPrice < 0) return "Giá Booking không được âm.";
        if ((input.Location?.Trim().Length ?? 0) is < 2 or > 100) return "Địa bàn phải có từ 2 đến 100 ký tự.";
        if ((input.ContactInfo?.Trim().Length ?? 0) is < 3 or > 255) return "Thông tin liên hệ phải có từ 3 đến 255 ký tự.";
        if (input.RatingScore.HasValue && input.RatingScore is < 1 or > 5) return "Đánh giá phải từ 1 đến 5 sao.";
        if (!KolStatuses.IsValid(input.Status)) return "Trạng thái KOL/KOC không hợp lệ.";
        return null;
    }

    private static void Normalize(KolInputModel input)
    {
        input.Name = input.Name.Trim();
        input.Platform = input.Platform.Trim();
        input.ProfileLink = input.ProfileLink.Trim();
        input.Niche = input.Niche.Trim();
        input.Location = input.Location.Trim();
        input.ContactInfo = input.ContactInfo.Trim();
        input.Status = input.Status.Trim();
    }

    private static bool TryParseVersion(string? value, out byte[] version)
    {
        try { version = Convert.FromBase64String(value ?? string.Empty); return version.Length > 0; }
        catch (FormatException) { version = []; return false; }
    }

    private static string GetBookingStatusLabel(string? status) => status switch
    {
        "dang_cho" => "Đang chờ",
        "thuong_luong" => "Thương lượng",
        "da_chot" => "Đã chốt",
        "dang_trien_khai" => "Đang triển khai",
        "hoan_thanh" => "Hoàn thành",
        "huy" => "Hủy",
        _ => status ?? "Đang chờ"
    };
}
