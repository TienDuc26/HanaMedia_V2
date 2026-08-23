using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Campaigns;

public sealed class CampaignService : ICampaignService
{
    private const int PageSize = 20;
    private readonly ApplicationDbContext _context;
    private readonly ISystemAuditService _auditService;

    public CampaignService(ApplicationDbContext context, ISystemAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<CampaignPageViewModel> GetPageAsync(
        string actorRole, string? search, string? status, int page,
        CancellationToken cancellationToken = default)
    {
        if (!CampaignPermissions.CanView(actorRole))
            throw new UnauthorizedAccessException("Tài khoản không có quyền xem chiến dịch.");

        var normalizedSearch = search?.Trim();
        var normalizedStatus = CampaignStatuses.IsValid(status) ? status : null;
        page = Math.Max(1, page);
        var query = _context.Campaigns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(item =>
                EF.Functions.Like(item.Name, pattern) ||
                EF.Functions.Like(item.ClientName, pattern));
        }
        if (normalizedStatus is not null) query = query.Where(item => item.Status == normalizedStatus);

        var total = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Min(page, totalPages);
        var campaigns = await query
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(item => new CampaignListItemViewModel
            {
                Id = item.Id,
                Name = item.Name,
                ClientName = item.ClientName,
                Description = item.Description,
                Budget = item.Budget,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                Status = item.Status,
                StatusLabel = CampaignStatuses.GetLabel(item.Status),
                CreatedByName = item.CreatedByUser.Username,
                BookingCount = item.Bookings.Count,
                IdeaCount = item.Ideas.Count,
                RowVersion = Convert.ToBase64String(item.RowVersion)
            })
            .ToListAsync(cancellationToken);

        return new CampaignPageViewModel
        {
            Search = normalizedSearch,
            Status = normalizedStatus,
            Page = page,
            TotalPages = totalPages,
            CanManage = CampaignPermissions.CanManage(actorRole),
            Campaigns = campaigns
        };
    }

    public async Task<CampaignOperationResult> CreateAsync(
        CreateCampaignInputModel input, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (!CampaignPermissions.CanManage(actorRole)) return CampaignOperationResult.Failure("Chỉ Quản lý Booking được tạo chiến dịch.");
        var validation = Validate(input);
        if (validation is not null) return CampaignOperationResult.Failure(validation);
        Normalize(input);
        if (await HasDuplicateAsync(input.Name, input.ClientName, null, cancellationToken))
            return CampaignOperationResult.Failure("Khách hàng đã có chiến dịch cùng tên.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var campaign = new Campaign
        {
            Name = input.Name,
            ClientName = input.ClientName,
            Description = input.Description,
            Budget = input.Budget,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            Status = input.Status,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.Campaigns.Add(campaign);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            _auditService.AddEvent(new AuditEvent(AuditModules.Booking, "campaign_created",
                $"Tạo chiến dịch '{campaign.Name}' cho khách hàng {campaign.ClientName}.",
                actorUserId, "Campaign", campaign.Id.ToString()));
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CampaignOperationResult.Success("Đã tạo chiến dịch thành công.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CampaignOperationResult.Failure("Không thể tạo chiến dịch. Hãy kiểm tra tên chiến dịch và dữ liệu nhập.");
        }
    }

    public async Task<CampaignOperationResult> UpdateAsync(
        UpdateCampaignInputModel input, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (!CampaignPermissions.CanManage(actorRole)) return CampaignOperationResult.Failure("Chỉ Quản lý Booking được sửa chiến dịch.");
        var validation = Validate(input);
        if (validation is not null) return CampaignOperationResult.Failure(validation);
        if (!TryParseVersion(input.RowVersion, out var expectedVersion)) return CampaignOperationResult.Failure("Phiên bản dữ liệu không hợp lệ. Hãy tải lại trang.");
        Normalize(input);
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(item => item.Id == input.Id, cancellationToken);
        if (campaign is null) return CampaignOperationResult.Failure("Không tìm thấy chiến dịch.");
        if (await HasDuplicateAsync(input.Name, input.ClientName, input.Id, cancellationToken))
            return CampaignOperationResult.Failure("Khách hàng đã có chiến dịch cùng tên.");

        _context.Entry(campaign).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        campaign.Name = input.Name;
        campaign.ClientName = input.ClientName;
        campaign.Description = input.Description;
        campaign.Budget = input.Budget;
        campaign.StartDate = input.StartDate;
        campaign.EndDate = input.EndDate;
        campaign.Status = input.Status;
        campaign.UpdatedAt = DateTime.UtcNow;
        _auditService.AddEvent(new AuditEvent(AuditModules.Booking, "campaign_updated",
            $"Cập nhật chiến dịch '{campaign.Name}' cho khách hàng {campaign.ClientName}.",
            actorUserId, "Campaign", campaign.Id.ToString()));
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return CampaignOperationResult.Success("Đã cập nhật chiến dịch.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return CampaignOperationResult.Failure("Chiến dịch vừa được người khác cập nhật. Hãy tải lại trang.");
        }
        catch (DbUpdateException)
        {
            return CampaignOperationResult.Failure("Không thể cập nhật chiến dịch. Hãy kiểm tra lại dữ liệu.");
        }
    }

    public async Task<CampaignOperationResult> DeleteAsync(
        DeleteCampaignInputModel input, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (!CampaignPermissions.CanManage(actorRole)) return CampaignOperationResult.Failure("Chỉ Quản lý Booking được xóa chiến dịch.");
        if (!TryParseVersion(input.RowVersion, out var expectedVersion)) return CampaignOperationResult.Failure("Phiên bản dữ liệu không hợp lệ. Hãy tải lại trang.");
        var campaign = await _context.Campaigns
            .Include(item => item.Bookings)
            .Include(item => item.Ideas)
            .FirstOrDefaultAsync(item => item.Id == input.Id, cancellationToken);
        if (campaign is null) return CampaignOperationResult.Failure("Không tìm thấy chiến dịch.");
        if (campaign.Bookings.Count > 0 || campaign.Ideas.Count > 0)
            return CampaignOperationResult.Failure("Không thể xóa chiến dịch đã được Booking hoặc Ý tưởng sử dụng.");

        _context.Entry(campaign).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        _context.Campaigns.Remove(campaign);
        _auditService.AddEvent(new AuditEvent(AuditModules.Booking, "campaign_deleted",
            $"Xóa chiến dịch '{campaign.Name}' của khách hàng {campaign.ClientName}.",
            actorUserId, "Campaign", campaign.Id.ToString(), AuditSeverity.Warning));
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return CampaignOperationResult.Success("Đã xóa chiến dịch.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return CampaignOperationResult.Failure("Chiến dịch vừa được người khác cập nhật. Hãy tải lại trang.");
        }
    }

    private async Task<bool> HasDuplicateAsync(string name, string clientName, int? excludedId, CancellationToken token)
        => await _context.Campaigns.AnyAsync(item =>
            item.Name == name && item.ClientName == clientName && (!excludedId.HasValue || item.Id != excludedId.Value), token);

    private static string? Validate(CampaignInputModel input)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        var client = input.ClientName?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 150) return "Tên chiến dịch phải có từ 2 đến 150 ký tự.";
        if (client.Length is < 2 or > 150) return "Tên khách hàng phải có từ 2 đến 150 ký tự.";
        if (input.Description?.Trim().Length > 2000) return "Mô tả không được vượt quá 2000 ký tự.";
        if (input.Budget < 0) return "Ngân sách không được âm.";
        if (input.EndDate < input.StartDate) return "Ngày kết thúc không được trước ngày bắt đầu.";
        if (!CampaignStatuses.IsValid(input.Status)) return "Trạng thái chiến dịch không hợp lệ.";
        return null;
    }

    private static void Normalize(CampaignInputModel input)
    {
        input.Name = input.Name.Trim();
        input.ClientName = input.ClientName.Trim();
        input.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        input.Status = input.Status.Trim();
    }

    private static bool TryParseVersion(string? value, out byte[] version)
    {
        try { version = Convert.FromBase64String(value ?? string.Empty); return version.Length > 0; }
        catch (FormatException) { version = []; return false; }
    }
}
