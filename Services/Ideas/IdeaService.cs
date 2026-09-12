using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Ideas;

public sealed class IdeaService : IIdeaService
{
    private const int PageSize = 20;
    private const long MaxFileSize = 10 * 1024 * 1024;
    private static readonly HashSet<string> ReferenceExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly ApplicationDbContext _context;
    private readonly ISystemAuditService _auditService;
    private readonly IWebHostEnvironment _environment;

    public IdeaService(ApplicationDbContext context, ISystemAuditService auditService, IWebHostEnvironment environment)
    {
        _context = context;
        _auditService = auditService;
        _environment = environment;
    }

    public async Task<IdeaPageViewModel> GetPageAsync(
        int actorUserId, string actorRole, string? search, string? status, int page,
        CancellationToken cancellationToken = default)
    {
        var actorEmployeeId = await GetActorEmployeeIdAsync(actorUserId, cancellationToken);
        var isManager = actorRole == AppRoles.IdeaManager;
        var normalizedSearch = search?.Trim();
        page = Math.Max(1, page);

        var query = _context.Ideas.AsNoTracking()
            .Include(i => i.Campaign)
            .Include(i => i.CreatorEmployee)
            .Include(i => i.PrimaryStaff)
            .Include(i => i.ReviewerEmployee)
            .Include(i => i.Comments).ThenInclude(c => c.AuthorUser).ThenInclude(u => u!.Employee)
            .Include(i => i.MoodboardImages)
            .AsQueryable();

        if (!isManager)
        {
            query = actorEmployeeId.HasValue
                ? query.Where(i => i.CreatorEmployeeId == actorEmployeeId || i.PrimaryStaffId == actorEmployeeId)
                : query.Where(_ => false);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(i => i.Title.Contains(normalizedSearch) ||
                                     i.ClientName.Contains(normalizedSearch) ||
                                     (i.CampaignName ?? string.Empty).Contains(normalizedSearch));
        }

        if (IdeaStatuses.IsValid(status)) query = query.Where(i => i.Status == status);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Min(page, totalPages);
        var ideas = await query.OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync(cancellationToken);

        var openTaskCounts = await _context.WorkTasks.AsNoTracking()
            .Where(t => t.Module == WorkTaskModules.Ideas && t.Status != WorkTaskStatuses.Done)
            .GroupBy(t => t.AssignedEmployeeId)
            .Select(group => new { EmployeeId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.EmployeeId, item => item.Count, cancellationToken);

        var activeStatuses = new[] { "dang_lam_viec", "thu_viec" };
        var staff = await _context.Employees.AsNoTracking()
            .Where(e => e.User != null &&
                        e.User.Status == AccountStatuses.Active &&
                        (e.User.Role == AppRoles.IdeaStaff || e.User.Role == AppRoles.IdeaManager) &&
                        activeStatuses.Contains(e.Status!))
            .OrderBy(e => e.FullName)
            .Select(e => new { e.Id, e.FullName })
            .ToListAsync(cancellationToken);

        var reviewers = await _context.Employees.AsNoTracking()
            .Where(e => e.User != null && e.User.Status == AccountStatuses.Active &&
                        e.User.Role == AppRoles.IdeaManager && activeStatuses.Contains(e.Status!))
            .OrderBy(e => e.FullName)
            .Select(e => new { e.Id, e.FullName })
            .ToListAsync(cancellationToken);

        return new IdeaPageViewModel
        {
            Search = normalizedSearch,
            Status = status,
            Page = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            IsManager = isManager,
            Campaigns = await _context.Campaigns.AsNoTracking()
                .Where(c => c.Status != "cancelled")
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new IdeaCampaignOptionViewModel(c.Id, c.Name, c.Client))
                .ToListAsync(cancellationToken),
            Staff = staff.Select(e => new IdeaEmployeeOptionViewModel(e.Id, e.FullName,
                openTaskCounts.GetValueOrDefault(e.Id))).ToList(),
            Reviewers = reviewers.Select(e => new IdeaEmployeeOptionViewModel(e.Id, e.FullName,
                openTaskCounts.GetValueOrDefault(e.Id))).ToList(),
            Items = ideas.Select(i => MapItem(i, actorEmployeeId, isManager)).ToList()
        };
    }

    public async Task<IdeaOperationResult> CreateAsync(
        SaveIdeaInputModel input, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateFiles(input);
        if (validation is not null) return IdeaOperationResult.Failure(validation);
        if (input.Deadline < DateOnly.FromDateTime(DateTime.Today))
            return IdeaOperationResult.Failure("Deadline không được nằm trong quá khứ.");

        var actorEmployeeId = await GetActorEmployeeIdAsync(actorUserId, cancellationToken);
        if (!actorEmployeeId.HasValue)
            return IdeaOperationResult.Failure("Tài khoản chưa liên kết với hồ sơ nhân sự nên chưa thể tạo ý tưởng.");

        var campaign = await _context.Campaigns.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == input.CampaignId && c.Status != "cancelled", cancellationToken);
        if (campaign is null) return IdeaOperationResult.Failure("Chiến dịch không tồn tại hoặc đã bị hủy.");

        var primaryStaffId = actorRole == AppRoles.IdeaManager ? input.PrimaryStaffId ?? actorEmployeeId : actorEmployeeId;
        var reviewerId = actorRole == AppRoles.IdeaManager ? input.ReviewerEmployeeId ?? actorEmployeeId : input.ReviewerEmployeeId;
        var peopleError = await ValidatePeopleAsync(primaryStaffId, reviewerId, cancellationToken);
        if (peopleError is not null) return IdeaOperationResult.Failure(peopleError);

        var now = DateTime.Now;
        var idea = new Idea
        {
            Title = input.Title.Trim(),
            CampaignId = campaign.Id,
            CampaignName = campaign.Name,
            ClientName = campaign.Client,
            Industry = "Chưa phân loại",
            Category = "chua_su_dung",
            Insight = Normalize(input.Insight),
            Concept = Normalize(input.Concept),
            ContentDetails = Normalize(input.ContentDetails),
            ReferenceLink = Normalize(input.ReferenceLink),
            MoodboardDesc = Normalize(input.MoodboardDesc),
            ScriptText = Normalize(input.ScriptText),
            Deadline = input.Deadline,
            CreatorEmployeeId = actorEmployeeId,
            PrimaryStaffId = primaryStaffId,
            ReviewerEmployeeId = reviewerId,
            Status = IdeaStatuses.Idea,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Ideas.Add(idea);
        await _context.SaveChangesAsync(cancellationToken);
        idea.ReferenceFileUrl = await SaveFileAsync(input.ReferenceFile, idea.Id, "reference", cancellationToken);
        idea.MoodboardFileUrl = await SaveFileAsync(input.MoodboardFile, idea.Id, "moodboard", cancellationToken);
        await SaveMoodboardImagesAsync(input.MoodboardFiles, idea, cancellationToken);
        _auditService.AddEvent(new AuditEvent(AuditModules.Ideas, AuditActions.Created,
            $"Tạo ý tưởng '{idea.Title}' cho chiến dịch '{campaign.Name}'.", actorUserId, "Idea", idea.Id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);
        return IdeaOperationResult.Success("Đã tạo ý tưởng. Bạn có thể tiếp tục chỉnh sửa trước khi gửi review.");
    }

    public async Task<IdeaOperationResult> UpdateAsync(
        SaveIdeaInputModel input, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateFiles(input);
        if (validation is not null) return IdeaOperationResult.Failure(validation);
        var idea = await _context.Ideas.Include(i => i.MoodboardImages)
            .FirstOrDefaultAsync(i => i.Id == input.Id, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy ý tưởng.");

        var actorEmployeeId = await GetActorEmployeeIdAsync(actorUserId, cancellationToken);
        var isManager = actorRole == AppRoles.IdeaManager;
        var editableStatus = idea.Status is IdeaStatuses.Idea or IdeaStatuses.Review or IdeaStatuses.NeedRevision;
        var hasDirectorRevisionRequest = idea.DirectorReviewStatus == DirectorIdeaReviewStatuses.RevisionRequested;
        var canEdit = (isManager && (editableStatus || hasDirectorRevisionRequest)) || (actorEmployeeId.HasValue &&
            (idea.CreatorEmployeeId == actorEmployeeId || idea.PrimaryStaffId == actorEmployeeId) &&
            (idea.Status is IdeaStatuses.Idea or IdeaStatuses.NeedRevision || hasDirectorRevisionRequest));
        if (!canEdit) return IdeaOperationResult.Failure("Bạn không có quyền chỉnh sửa ý tưởng ở trạng thái hiện tại.");

        var campaign = await _context.Campaigns.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == input.CampaignId && c.Status != "cancelled", cancellationToken);
        if (campaign is null) return IdeaOperationResult.Failure("Chiến dịch không tồn tại hoặc đã bị hủy.");

        var primaryStaffId = isManager ? input.PrimaryStaffId ?? idea.PrimaryStaffId : idea.PrimaryStaffId;
        var reviewerId = isManager ? input.ReviewerEmployeeId ?? idea.ReviewerEmployeeId : idea.ReviewerEmployeeId;
        var peopleError = await ValidatePeopleAsync(primaryStaffId, reviewerId, cancellationToken);
        if (peopleError is not null) return IdeaOperationResult.Failure(peopleError);

        idea.Title = input.Title.Trim();
        idea.CampaignId = campaign.Id;
        idea.CampaignName = campaign.Name;
        idea.ClientName = campaign.Client;
        idea.Insight = Normalize(input.Insight);
        idea.Concept = Normalize(input.Concept);
        idea.ContentDetails = Normalize(input.ContentDetails);
        idea.ReferenceLink = Normalize(input.ReferenceLink);
        idea.MoodboardDesc = Normalize(input.MoodboardDesc);
        idea.ScriptText = Normalize(input.ScriptText);
        idea.Deadline = input.Deadline;
        idea.PrimaryStaffId = primaryStaffId;
        idea.ReviewerEmployeeId = reviewerId;
        idea.UpdatedAt = DateTime.Now;
        idea.ReferenceFileUrl = await SaveFileAsync(input.ReferenceFile, idea.Id, "reference", cancellationToken) ?? idea.ReferenceFileUrl;
        idea.MoodboardFileUrl = await SaveFileAsync(input.MoodboardFile, idea.Id, "moodboard", cancellationToken) ?? idea.MoodboardFileUrl;
        await SaveMoodboardImagesAsync(input.MoodboardFiles, idea, cancellationToken);
        _auditService.AddEvent(new AuditEvent(AuditModules.Ideas, AuditActions.Updated,
            $"Cập nhật ý tưởng '{idea.Title}'.", actorUserId, "Idea", idea.Id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);
        return IdeaOperationResult.Success("Đã lưu thay đổi ý tưởng.");
    }

    public async Task<IdeaOperationResult> SubmitAsync(int id, int actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy ý tưởng.");
        var actorEmployeeId = await GetActorEmployeeIdAsync(actorUserId, cancellationToken);
        if (!CanAccess(idea, actorEmployeeId, actorRole == AppRoles.IdeaManager))
            return IdeaOperationResult.Failure("Bạn không có quyền gửi ý tưởng này.");
        var isDirectorResubmission = idea.DirectorReviewStatus == DirectorIdeaReviewStatuses.RevisionRequested;
        if (idea.Status is not (IdeaStatuses.Idea or IdeaStatuses.NeedRevision) && !isDirectorResubmission)
            return IdeaOperationResult.Failure("Chỉ ý tưởng mới hoặc đang cần sửa mới có thể gửi review.");
        if (string.IsNullOrWhiteSpace(idea.Insight) || string.IsNullOrWhiteSpace(idea.Concept) ||
            string.IsNullOrWhiteSpace(idea.ContentDetails) || string.IsNullOrWhiteSpace(idea.ScriptText))
            return IdeaOperationResult.Failure("Cần hoàn thiện Insight, Concept, Nội dung và Script trước khi gửi review.");
        if (!idea.ReviewerEmployeeId.HasValue)
            return IdeaOperationResult.Failure("Ý tưởng chưa có người review.");

        if (idea.Status is IdeaStatuses.Idea or IdeaStatuses.NeedRevision)
            idea.Status = IdeaStatuses.Review;
        if (isDirectorResubmission)
        {
            idea.DirectorReviewStatus = DirectorIdeaReviewStatuses.Pending;
            idea.DirectorReviewedAt = null;
            idea.DirectorReviewedByUserId = null;
            AddComment(idea.Id, actorUserId, "Đã cập nhật ý tưởng theo feedback và gửi lại Giám đốc.", IdeaCommentTypes.General);
        }
        idea.UpdatedAt = DateTime.Now;
        _auditService.AddEvent(new AuditEvent(AuditModules.Ideas, AuditActions.Updated,
            $"Gửi ý tưởng '{idea.Title}' để review.", actorUserId, "Idea", id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);
        return IdeaOperationResult.Success("Ý tưởng đã được gửi cho Quản lý Ý tưởng review.");
    }

    public async Task<IdeaOperationResult> ReviewAsync(
        int id, string decision, string? feedback, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.IdeaManager) return IdeaOperationResult.Failure("Chỉ Quản lý Ý tưởng được review trong Module 13.");
        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy ý tưởng.");
        if (idea.Status != IdeaStatuses.Review) return IdeaOperationResult.Failure("Ý tưởng không ở trạng thái chờ review.");

        var normalizedFeedback = Normalize(feedback);
        if (decision == "revision" && string.IsNullOrWhiteSpace(normalizedFeedback))
            return IdeaOperationResult.Failure("Cần nhập nội dung yêu cầu chỉnh sửa.");
        if (decision is not ("revision" or "approve")) return IdeaOperationResult.Failure("Quyết định review không hợp lệ.");

        idea.Status = decision == "approve" ? IdeaStatuses.Approved : IdeaStatuses.NeedRevision;
        idea.FeedbackComment = normalizedFeedback;
        idea.UpdatedAt = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(normalizedFeedback)) AddComment(idea.Id, actorUserId, normalizedFeedback,
            decision == "approve" ? IdeaCommentTypes.Review : IdeaCommentTypes.RevisionRequest);
        var action = decision == "approve" ? AuditActions.Approved : AuditActions.Rejected;
        _auditService.AddEvent(new AuditEvent(AuditModules.Ideas, action,
            $"{(decision == "approve" ? "Duyệt" : "Yêu cầu sửa")} ý tưởng '{idea.Title}'.", actorUserId, "Idea", id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);
        return IdeaOperationResult.Success(decision == "approve" ? "Đã duyệt ý tưởng." : "Đã gửi yêu cầu chỉnh sửa.");
    }

    public async Task<IdeaOperationResult> AdvanceAsync(int id, int actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy ý tưởng.");
        var actorEmployeeId = await GetActorEmployeeIdAsync(actorUserId, cancellationToken);
        if (!CanAccess(idea, actorEmployeeId, actorRole == AppRoles.IdeaManager))
            return IdeaOperationResult.Failure("Bạn không có quyền cập nhật tiến độ ý tưởng này.");

        var previousStatus = idea.Status;
        idea.Status = previousStatus switch
        {
            IdeaStatuses.Approved => IdeaStatuses.InProgress,
            IdeaStatuses.InProgress => IdeaStatuses.Done,
            _ => idea.Status
        };
        if (previousStatus is not (IdeaStatuses.Approved or IdeaStatuses.InProgress))
            return IdeaOperationResult.Failure("Trạng thái hiện tại không thể chuyển sang bước triển khai tiếp theo.");

        idea.UpdatedAt = DateTime.Now;
        _auditService.AddEvent(new AuditEvent(AuditModules.Ideas, AuditActions.Updated,
            $"Chuyển tiến độ ý tưởng '{idea.Title}' sang {IdeaStatuses.GetLabel(idea.Status!)}.", actorUserId, "Idea", id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);
        return IdeaOperationResult.Success($"Đã chuyển sang trạng thái {IdeaStatuses.GetLabel(idea.Status!)}.");
    }

    public async Task<IdeaOperationResult> AddCommentAsync(
        int id, string? content, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(content);
        if (string.IsNullOrWhiteSpace(normalized)) return IdeaOperationResult.Failure("Nội dung bình luận không được để trống.");
        if (normalized.Length > 2000) return IdeaOperationResult.Failure("Bình luận không được vượt quá 2000 ký tự.");
        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy ý tưởng.");
        var actorEmployeeId = await GetActorEmployeeIdAsync(actorUserId, cancellationToken);
        if (!CanAccess(idea, actorEmployeeId, actorRole == AppRoles.IdeaManager))
            return IdeaOperationResult.Failure("Bạn không có quyền bình luận ý tưởng này.");

        AddComment(id, actorUserId, normalized, IdeaCommentTypes.General);
        _auditService.AddEvent(new AuditEvent(AuditModules.Ideas, AuditActions.Updated,
            $"Thêm bình luận cho ý tưởng '{idea.Title}'.", actorUserId, "Idea", id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);
        return IdeaOperationResult.Success("Đã thêm bình luận.");
    }

    public async Task<IdeaOperationResult> DeleteAsync(int id, int actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.IdeaManager) return IdeaOperationResult.Failure("Chỉ Quản lý Ý tưởng được xóa ý tưởng.");
        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy ý tưởng.");
        if (await _context.WorkTasks.AnyAsync(t => t.RelatedType == WorkTaskRelatedTypes.Idea && t.RelatedId == id, cancellationToken))
            return IdeaOperationResult.Failure("Không thể xóa ý tưởng đã liên kết với công việc. Hãy giữ lại để bảo toàn lịch sử.");

        _context.Ideas.Remove(idea);
        _auditService.AddEvent(new AuditEvent(AuditModules.Ideas, AuditActions.Deleted,
            $"Xóa ý tưởng '{idea.Title}'.", actorUserId, "Idea", id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);
        return IdeaOperationResult.Success("Đã xóa ý tưởng.");
    }

    public async Task<IdeaOperationResult> DeleteMoodboardImageAsync(
        long imageId, int actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.IdeaManager)
            return IdeaOperationResult.Failure("Chỉ Quản lý Ý tưởng được xóa ảnh moodboard.");
        var image = await _context.IdeaMoodboardImages.Include(item => item.Idea)
            .FirstOrDefaultAsync(item => item.Id == imageId, cancellationToken);
        if (image is null) return IdeaOperationResult.Failure("Không tìm thấy ảnh moodboard.");

        _context.IdeaMoodboardImages.Remove(image);
        _auditService.AddEvent(new AuditEvent(AuditModules.Ideas, AuditActions.Updated,
            $"Xóa một ảnh moodboard của ý tưởng '{image.Idea.Title}'.", actorUserId, "Idea", image.IdeaId.ToString()));
        await _context.SaveChangesAsync(cancellationToken);
        DeleteStoredFile(image.FileUrl);
        return IdeaOperationResult.Success("Đã xóa ảnh moodboard.");
    }

    private async Task<int?> GetActorEmployeeIdAsync(int userId, CancellationToken cancellationToken) =>
        await _context.Employees.AsNoTracking().Where(e => e.UserId == userId)
            .Select(e => (int?)e.Id).FirstOrDefaultAsync(cancellationToken);

    private async Task<string?> ValidatePeopleAsync(int? primaryStaffId, int? reviewerId, CancellationToken cancellationToken)
    {
        if (!primaryStaffId.HasValue) return "Cần chọn người phụ trách ý tưởng.";
        if (!reviewerId.HasValue) return "Cần chọn Quản lý Ý tưởng review.";
        var primaryValid = await _context.Employees.AnyAsync(e => e.Id == primaryStaffId && e.User != null &&
            e.User.Status == AccountStatuses.Active && (e.User.Role == AppRoles.IdeaStaff || e.User.Role == AppRoles.IdeaManager), cancellationToken);
        if (!primaryValid) return "Người phụ trách không phải nhân sự Ý tưởng đang hoạt động.";
        var reviewerValid = await _context.Employees.AnyAsync(e => e.Id == reviewerId && e.User != null &&
            e.User.Status == AccountStatuses.Active && e.User.Role == AppRoles.IdeaManager, cancellationToken);
        return reviewerValid ? null : "Người review phải là Quản lý Ý tưởng đang hoạt động.";
    }

    private static IdeaListItemViewModel MapItem(Idea idea, int? actorEmployeeId, bool isManager)
    {
        var canAccess = CanAccess(idea, actorEmployeeId, isManager);
        return new IdeaListItemViewModel
        {
            Id = idea.Id,
            Title = idea.Title,
            CampaignId = idea.CampaignId,
            ClientName = idea.Campaign?.Client ?? idea.ClientName,
            CampaignName = idea.Campaign?.Name ?? idea.CampaignName ?? string.Empty,
            Insight = idea.Insight,
            Concept = idea.Concept,
            ContentDetails = idea.ContentDetails,
            ReferenceLink = idea.ReferenceLink,
            ReferenceFilePath = idea.ReferenceFileUrl,
            MoodboardDesc = idea.MoodboardDesc,
            MoodboardFilePath = idea.MoodboardFileUrl,
            MoodboardImages = idea.MoodboardImages.OrderBy(image => image.SortOrder).ThenBy(image => image.Id)
                .Select(image => new IdeaMoodboardImageViewModel(image.Id, image.FileUrl, image.SortOrder)).ToList(),
            ScriptText = idea.ScriptText,
            Deadline = idea.Deadline,
            PrimaryStaffId = idea.PrimaryStaffId,
            ReviewerEmployeeId = idea.ReviewerEmployeeId,
            CreatorName = idea.CreatorEmployee?.FullName ?? "-",
            PrimaryStaffName = idea.PrimaryStaff?.FullName ?? "-",
            ReviewerName = idea.ReviewerEmployee?.FullName ?? "-",
            Status = idea.Status ?? IdeaStatuses.Idea,
            StatusLabel = IdeaStatuses.GetLabel(idea.Status ?? IdeaStatuses.Idea),
            FeedbackComment = idea.FeedbackComment,
            DirectorReviewStatus = idea.DirectorReviewStatus,
            DirectorReviewStatusLabel = DirectorIdeaReviewStatuses.GetLabel(idea.DirectorReviewStatus),
            DirectorFeedback = idea.DirectorFeedback,
            DirectorReviewedAt = idea.DirectorReviewedAt,
            UpdatedAt = idea.UpdatedAt,
            CanEdit = (isManager && (idea.Status is IdeaStatuses.Idea or IdeaStatuses.Review or IdeaStatuses.NeedRevision ||
                                     idea.DirectorReviewStatus == DirectorIdeaReviewStatuses.RevisionRequested)) ||
                      (!isManager && canAccess &&
                       (idea.Status is IdeaStatuses.Idea or IdeaStatuses.NeedRevision ||
                        idea.DirectorReviewStatus == DirectorIdeaReviewStatuses.RevisionRequested)),
            CanSubmit = canAccess &&
                        (idea.Status is IdeaStatuses.Idea or IdeaStatuses.NeedRevision ||
                         idea.DirectorReviewStatus == DirectorIdeaReviewStatuses.RevisionRequested),
            CanReview = isManager && idea.Status == IdeaStatuses.Review,
            CanAdvance = canAccess && idea.Status is IdeaStatuses.Approved or IdeaStatuses.InProgress,
            CanDelete = isManager,
            Comments = idea.Comments.OrderByDescending(c => c.CreatedAt).Select(c => new IdeaCommentViewModel(
                c.AuthorUser?.Employee?.FullName ?? c.AuthorUser?.Username ?? "Tài khoản đã xóa",
                c.AuthorUser is null ? "-" : AppRoles.GetLabel(c.AuthorUser.Role), c.Content, c.CreatedAt)).ToList()
        };
    }

    private static bool CanAccess(Idea idea, int? actorEmployeeId, bool isManager) =>
        isManager || (actorEmployeeId.HasValue &&
            (idea.CreatorEmployeeId == actorEmployeeId || idea.PrimaryStaffId == actorEmployeeId));

    private void AddComment(int ideaId, int userId, string content, string commentType) => _context.IdeaComments.Add(new IdeaComment
    {
        IdeaId = ideaId,
        AuthorUserId = userId,
        CommentType = commentType,
        Content = content,
        CreatedAt = DateTime.Now
    });

    private static string? ValidateFiles(SaveIdeaInputModel input)
    {
        var referenceError = ValidateFile(input.ReferenceFile, ReferenceExtensions, "Tệp reference");
        if (referenceError is not null) return referenceError;
        var moodboardFiles = input.MoodboardFiles.Where(file => file.Length > 0).ToList();
        if (input.MoodboardFile is { Length: > 0 }) moodboardFiles.Add(input.MoodboardFile);
        if (moodboardFiles.Count > 10) return "Mỗi lần chỉ được tải lên tối đa 10 ảnh moodboard.";
        if (moodboardFiles.Sum(file => file.Length) > 30 * 1024 * 1024)
            return "Tổng dung lượng ảnh moodboard mỗi lần không được vượt quá 30 MB.";
        foreach (var file in moodboardFiles)
        {
            var error = ValidateFile(file, ImageExtensions, "Ảnh moodboard");
            if (error is not null) return error;
        }
        return null;
    }

    private static string? ValidateFile(IFormFile? file, HashSet<string> allowedExtensions, string label)
    {
        if (file is null || file.Length == 0) return null;
        if (file.Length > MaxFileSize) return $"{label} vượt quá 10 MB.";
        var extension = Path.GetExtension(file.FileName);
        return allowedExtensions.Contains(extension) ? null : $"{label} có định dạng không được hỗ trợ.";
    }

    private async Task<string?> SaveFileAsync(IFormFile? file, int ideaId, string prefix, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return null;
        var root = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var folder = Path.Combine(root, "uploads", "ideas", ideaId.ToString());
        Directory.CreateDirectory(folder);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedName = $"{prefix}_{Guid.NewGuid():N}{extension}";
        await using var stream = new FileStream(Path.Combine(folder, storedName), FileMode.CreateNew);
        await file.CopyToAsync(stream, cancellationToken);
        return $"/uploads/ideas/{ideaId}/{storedName}";
    }

    private async Task SaveMoodboardImagesAsync(
        IEnumerable<IFormFile> files, Idea idea, CancellationToken cancellationToken)
    {
        var nextOrder = idea.MoodboardImages.Count == 0 ? 0 : idea.MoodboardImages.Max(image => image.SortOrder) + 1;
        foreach (var file in files.Where(file => file.Length > 0))
        {
            var url = await SaveFileAsync(file, idea.Id, "moodboard", cancellationToken);
            if (url is null) continue;
            var image = new IdeaMoodboardImage
            {
                IdeaId = idea.Id,
                FileUrl = url,
                SortOrder = nextOrder++,
                CreatedAt = DateTime.Now
            };
            idea.MoodboardImages.Add(image);
            _context.IdeaMoodboardImages.Add(image);
        }
    }

    private void DeleteStoredFile(string url)
    {
        var root = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var ideaUploadsRoot = Path.GetFullPath(Path.Combine(root, "uploads", "ideas"));
        var relativePath = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (fullPath.StartsWith(ideaUploadsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            File.Delete(fullPath);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
