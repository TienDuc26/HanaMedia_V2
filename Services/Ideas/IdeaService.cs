using System.Globalization;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Ideas;

/// <summary>
/// Lõi nghiệp vụ Module 13: Quản lý Ý tưởng.
/// Triển khai quy trình 6 bước:
///   Ý tưởng → Review → Chỉnh sửa → Duyệt → Triển khai → Hoàn thành
/// Chặn cứng ở backend: NV Ý tưởng chỉ được tự chuyển sang "Review" (2 lần: tạo mới & sau khi sửa).
/// Mọi bước còn lại (Duyệt, Triển khai, Hoàn thành, yêu cầu sửa) chỉ QL Ý tưởng được làm.
/// Giám đốc/QL Booking/NV Booking: chỉ xem (không có controller gọi tới các method ghi).
/// </summary>
public sealed class IdeaService : IIdeaService
{
    private const int PageSize = 20;
    private const string IdeaDepartment = "Y_tuong";

    private readonly ApplicationDbContext _context;
    private readonly ISystemAuditService _auditService;

    public IdeaService(ApplicationDbContext context, ISystemAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    // ===========================================================================
    // ============================== GET PAGE ===================================
    // ===========================================================================

    public async Task<IdeaPageViewModel> GetPageAsync(
        int actorUserId, string actorRole, int? actorEmployeeId,
        string? search, string? status, string? client,
        int page,
        CancellationToken cancellationToken = default)
    {
        // Giám đốc, QL Ý tưởng, NV � tưởng, QL/NV Booking — đều có quyền xem danh sách.
        // AdminIT / HCNS — không có quyền, throw để controller trả 403.
        EnsureViewAccess(actorRole);

        page = Math.Max(1, page);
        var normalizedSearch = search?.Trim();
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        var normalizedClient = string.IsNullOrWhiteSpace(client) ? null : client.Trim();

        var query = _context.Ideas.AsNoTracking()
            .Include(idea => idea.CreatorEmployee)
            .Include(idea => idea.PrimaryStaff)
            .Include(idea => idea.ReviewerEmployee)
            .AsQueryable();

        // NV Ý tưởng chỉ thấy ý tưởng của chính mình (tạo hoặc được giao phụ trách).
        if (actorRole == AppRoles.IdeaStaff && actorEmployeeId.HasValue)
        {
            var empId = actorEmployeeId.Value;
            query = query.Where(idea =>
                idea.CreatorEmployeeId == empId ||
                idea.PrimaryStaffId == empId ||
                idea.ReviewerEmployeeId == empId);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(idea =>
                EF.Functions.Like(idea.Title, pattern) ||
                EF.Functions.Like(idea.ClientName, pattern) ||
                EF.Functions.Like(idea.CampaignName ?? string.Empty, pattern));
        }

        if (!string.IsNullOrWhiteSpace(normalizedStatus) && IdeaStatuses.IsValid(normalizedStatus))
        {
            var s = normalizedStatus;
            query = query.Where(idea => idea.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(normalizedClient))
        {
            var c = normalizedClient;
            query = query.Where(idea => idea.ClientName == c);
        }

        var total = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Min(page, totalPages);

        var entities = await query
            .OrderBy(idea => idea.Status == IdeaStatuses.Done)
            .ThenBy(idea => idea.Deadline)
            .ThenByDescending(idea => idea.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        // Số task mở gắn với từng idea
        var ideaIds = entities.Select(i => i.Id).ToList();
        var openTaskCounts = await _context.WorkTasks.AsNoTracking()
            .Where(task => task.IdeaId.HasValue
                && ideaIds.Contains(task.IdeaId!.Value)
                && task.Status != WorkTaskStatuses.Done)
            .GroupBy(task => task.IdeaId!.Value)
            .Select(g => new { IdeaId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.IdeaId, x => x.Count, cancellationToken);

        var items = entities.Select(idea => new IdeaListItemViewModel
        {
            Id = idea.Id,
            Title = idea.Title,
            CreatorName = idea.CreatorEmployee?.FullName ?? "-",
            ClientName = idea.ClientName,
            CampaignName = idea.CampaignName ?? string.Empty,
            PrimaryStaffName = idea.PrimaryStaff?.FullName ?? "-",
            ReviewerName = idea.ReviewerEmployee?.FullName ?? "-",
            Status = idea.Status ?? IdeaStatuses.Draft,
            StatusLabel = IdeaStatuses.GetLabel(idea.Status ?? IdeaStatuses.Draft),
            StatusCssClass = IdeaStatuses.GetCssClass(idea.Status ?? IdeaStatuses.Draft),
            Deadline = idea.Deadline,
            UpdatedAt = idea.UpdatedAt,
            HasReferenceFile = !string.IsNullOrWhiteSpace(idea.ReferenceFileUrl),
            HasMoodboardFile = !string.IsNullOrWhiteSpace(idea.MoodboardFileUrl),
            OpenTasks = openTaskCounts.TryGetValue(idea.Id, out var c) ? c : 0
        }).ToList();

        var distinctClients = await _context.Ideas.AsNoTracking()
            .Select(idea => idea.ClientName)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c => c)
            .Take(50)
            .ToListAsync(cancellationToken);

        var employees = new List<IdeaEmployeeOptionViewModel>();
        var reviewers = new List<IdeaEmployeeOptionViewModel>();
        if (CanCreate(actorRole))
        {
            employees = await _context.Employees.AsNoTracking()
                .Where(employee =>
                    employee.UserId.HasValue &&
                    employee.Department == IdeaDepartment)
                .OrderBy(employee => employee.FullName)
                .Select(employee => new IdeaEmployeeOptionViewModel(employee.Id, employee.FullName, employee.Department))
                .ToListAsync(cancellationToken);

            // Reviewer chỉ là QL Ý tưởng (employee thuộc phòng Y_tuong có IsManager=true).
            reviewers = await _context.Employees.AsNoTracking()
                .Where(employee =>
                    employee.Department == IdeaDepartment &&
                    employee.IsManager)
                .OrderBy(employee => employee.FullName)
                .Select(employee => new IdeaEmployeeOptionViewModel(employee.Id, employee.FullName, employee.Department))
                .ToListAsync(cancellationToken);
        }

        return new IdeaPageViewModel
        {
            Items = items,
            Employees = employees,
            Reviewers = reviewers,
            Search = normalizedSearch,
            StatusFilter = normalizedStatus,
            ClientFilter = normalizedClient,
            Page = page,
            TotalPages = totalPages,
            TotalCount = total,
            CanCreate = CanCreate(actorRole),
            CanEdit = CanEdit(actorRole, actorEmployeeId),
            CanTransition = CanTransition(actorRole),
            CanComment = CanComment(actorRole),
            CanAssignTask = actorRole == AppRoles.IdeaManager,
            CurrentUserRole = actorRole,
            CurrentEmployeeId = actorEmployeeId,
            AvailableStatuses = IdeaStatuses.All.ToList(),
            DistinctClients = distinctClients
        };
    }

    // ===========================================================================
    // ============================ GET DETAIL ===================================
    // ===========================================================================

    public async Task<IdeaDetailViewModel?> GetDetailAsync(
        int ideaId, int actorUserId, string actorRole, int? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        EnsureViewAccess(actorRole);

        var idea = await _context.Ideas.AsNoTracking()
            .Include(i => i.CreatorEmployee)
            .Include(i => i.PrimaryStaff)
            .Include(i => i.ReviewerEmployee)
            .Include(i => i.MoodboardImages.OrderBy(m => m.SortOrder).ThenBy(m => m.Id))
            .FirstOrDefaultAsync(i => i.Id == ideaId, cancellationToken);

        if (idea is null) return null;

        // NV Ý tưởng chỉ được xem ý tưởng của mình
        if (actorRole == AppRoles.IdeaStaff && actorEmployeeId.HasValue)
        {
            var empId = actorEmployeeId.Value;
            var canView = idea.CreatorEmployeeId == empId
                || idea.PrimaryStaffId == empId
                || idea.ReviewerEmployeeId == empId;
            if (!canView) return null;
        }

        var comments = await _context.IdeaComments.AsNoTracking()
            .Where(c => c.IdeaId == ideaId)
            .OrderBy(c => c.CreatedAt)
            .Join(_context.Users.AsNoTracking(),
                c => c.AuthorUserId,
                u => (int?)u.Id,
                (c, u) => new { Comment = c, User = u })
            .ToListAsync(cancellationToken);

        var commentVms = comments.Select(row => new IdeaCommentViewModel
        {
            Id = row.Comment.Id,
            IdeaId = row.Comment.IdeaId,
            AuthorUserId = row.Comment.AuthorUserId,
            AuthorName = row.User?.Username ?? "Người dùng không xác định",
            AuthorRole = AppRoles.TryGetLabel(row.User?.Role ?? string.Empty, out var lbl) ? lbl : (row.User?.Role ?? ""),
            CommentType = row.Comment.CommentType,
            Content = row.Comment.Content,
            CreatedAt = row.Comment.CreatedAt
        }).ToList();

        // Danh sách task gắn với ý tưởng — dùng cho khu vực "Công việc liên quan" trong modal detail.
        var relatedTaskVms = await _context.WorkTasks.AsNoTracking()
            .Where(task => task.IdeaId == ideaId)
            .Include(task => task.AssignedEmployee)
            .OrderBy(task => task.Status == WorkTaskStatuses.Done)
            .ThenBy(task => task.Deadline)
            .ThenByDescending(task => task.Id)
            .Select(task => new IdeaRelatedTaskViewModel(
                task.Id,
                task.Title,
                task.WorkCategory,
                task.WorkCategory == null ? null : IdeaTaskCategories.GetLabel(task.WorkCategory),
                task.Status,
                WorkTaskStatuses.GetLabel(task.Status),
                task.AssignedEmployee.FullName,
                task.Deadline))
            .ToListAsync(cancellationToken);

        return new IdeaDetailViewModel
        {
            Id = idea.Id,
            Title = idea.Title,
            CreatorEmployeeId = idea.CreatorEmployeeId,
            CreatorName = idea.CreatorEmployee?.FullName ?? "-",
            ClientName = idea.ClientName,
            CampaignName = idea.CampaignName ?? string.Empty,
            Industry = idea.Industry ?? string.Empty,
            Category = idea.Category,
            CategoryLabel = IdeaCategories.GetLabel(idea.Category),
            Insight = idea.Insight,
            Concept = idea.Concept,
            ContentDetails = idea.ContentDetails,
            ReferenceLink = idea.ReferenceLink,
            ReferenceFileUrl = idea.ReferenceFileUrl,
            MoodboardDesc = idea.MoodboardDesc,
            MoodboardFileUrl = idea.MoodboardFileUrl,
            MoodboardImages = idea.MoodboardImages
                .Select(m => new IdeaMoodboardImageViewModel
                {
                    Id = m.Id,
                    FileUrl = m.FileUrl,
                    SortOrder = m.SortOrder
                }).ToList(),
            ScriptText = idea.ScriptText,
            Deadline = idea.Deadline,
            PrimaryStaffId = idea.PrimaryStaffId,
            PrimaryStaffName = idea.PrimaryStaff?.FullName ?? "-",
            ReviewerEmployeeId = idea.ReviewerEmployeeId,
            ReviewerName = idea.ReviewerEmployee?.FullName ?? "-",
            Status = idea.Status ?? IdeaStatuses.Draft,
            StatusLabel = IdeaStatuses.GetLabel(idea.Status ?? IdeaStatuses.Draft),
            StatusCssClass = IdeaStatuses.GetCssClass(idea.Status ?? IdeaStatuses.Draft),
            CreatedAt = idea.CreatedAt,
            UpdatedAt = idea.UpdatedAt,
            Comments = commentVms,
            AllowedTransitions = GetAllowedTransitions(idea, actorRole, actorEmployeeId).ToList(),
            RelatedTasks = relatedTaskVms
        };
    }

    // ===========================================================================
    // ============================== CREATE =====================================
    // ===========================================================================

    public async Task<IdeaOperationResult> CreateAsync(
        IdeaUpsertInputModel input, int actorUserId, string actorRole, int? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        if (!CanCreate(actorRole))
            return IdeaOperationResult.Failure("Tài khoản không có quyền tạo ý tưởng.");
        if (input.Deadline < DateOnly.FromDateTime(DateTime.Today))
            return IdeaOperationResult.Failure("Deadline không được n�m trong quá khứ.");

        var category = string.IsNullOrWhiteSpace(input.Category) ? IdeaCategories.ChuaSuDung : input.Category.Trim();
        if (!IdeaCategories.IsValid(category))
            return IdeaOperationResult.Failure("Phân loại thư viện � tưởng không hợp lệ.");

        if (input.PrimaryStaffId.HasValue)
        {
            var ok = await EmployeeExistsAndBelongsToIdeaDept(input.PrimaryStaffId.Value, cancellationToken);
            if (!ok) return IdeaOperationResult.Failure("Người phụ trách phải thuộc phòng Ý tưởng.");
        }
        if (input.ReviewerEmployeeId.HasValue)
        {
            var ok = await ReviewerExists(input.ReviewerEmployeeId.Value, cancellationToken);
            if (!ok) return IdeaOperationResult.Failure("Người review phải là Quản lý thuộc phòng Ý tưởng.");
        }

        var now = DateTime.UtcNow;
        var idea = new Idea
        {
            Title = input.Title.Trim(),
            CreatorEmployeeId = actorEmployeeId,
            ClientName = input.ClientName.Trim(),
            CampaignName = string.IsNullOrWhiteSpace(input.CampaignName) ? null : input.CampaignName.Trim(),
            Industry = string.IsNullOrWhiteSpace(input.Industry) ? string.Empty : input.Industry.Trim(),
            Category = category,
            Insight = string.IsNullOrWhiteSpace(input.Insight) ? null : input.Insight.Trim(),
            Concept = string.IsNullOrWhiteSpace(input.Concept) ? null : input.Concept.Trim(),
            ContentDetails = string.IsNullOrWhiteSpace(input.ContentDetails) ? null : input.ContentDetails.Trim(),
            ReferenceLink = string.IsNullOrWhiteSpace(input.ReferenceLink) ? null : input.ReferenceLink.Trim(),
            MoodboardDesc = string.IsNullOrWhiteSpace(input.MoodboardDesc) ? null : input.MoodboardDesc.Trim(),
            ScriptText = string.IsNullOrWhiteSpace(input.ScriptText) ? null : input.ScriptText.Trim(),
            Deadline = input.Deadline,
            PrimaryStaffId = input.PrimaryStaffId,
            ReviewerEmployeeId = input.ReviewerEmployeeId,
            Status = IdeaStatuses.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.Ideas.Add(idea);
        await _context.SaveChangesAsync(cancellationToken);

        _auditService.AddEvent(new AuditEvent(
            AuditModules.Ideas,
            "idea_created",
            $"Tạo ý tưởng '{idea.Title}' cho client '{idea.ClientName}'.",
            actorUserId,
            "Idea",
            idea.Id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);

        return IdeaOperationResult.Success("Đã tạo ý tưởng mới.", idea.Id);
    }

    // ===========================================================================
    // ============================== UPDATE =====================================
    // ===========================================================================

    public async Task<IdeaOperationResult> UpdateAsync(
        IdeaUpsertInputModel input, int actorUserId, string actorRole, int? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        if (!input.Id.HasValue)
            return IdeaOperationResult.Failure("Thiếu ID ý tư�ng cần cập nhật.");
        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == input.Id.Value, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy ý tư�ng.");

        if (!CanEditThisIdea(idea, actorRole, actorEmployeeId))
            return IdeaOperationResult.Failure("Bạn không có quyền chỉnh sửa ý tưởng này.");

        // Không cho sửa nội dung sau khi đã duyệt / đang triển khai / hoàn thành — tránh lịch sử bị "lệch".
        if (idea.Status is IdeaStatuses.Approved or IdeaStatuses.InProduction or IdeaStatuses.Done)
        {
            return IdeaOperationResult.Failure("Ý tưởng đã ở trạng thái sau duyệt — không thể chỉnh sửa nội dung. Hãy tạo ý tưởng mới nếu cần.");
        }

        if (input.Deadline < DateOnly.FromDateTime(DateTime.Today))
            return IdeaOperationResult.Failure("Deadline không được nằm trong quá khứ.");

        var category = string.IsNullOrWhiteSpace(input.Category) ? idea.Category : input.Category.Trim();
        if (!IdeaCategories.IsValid(category))
            return IdeaOperationResult.Failure("Phân loại thư viện ý tưởng không hợp lệ.");

        if (input.PrimaryStaffId.HasValue)
        {
            var ok = await EmployeeExistsAndBelongsToIdeaDept(input.PrimaryStaffId.Value, cancellationToken);
            if (!ok) return IdeaOperationResult.Failure("Người phụ trách phải thuộc phòng Ý tưởng.");
        }
        if (input.ReviewerEmployeeId.HasValue)
        {
            var ok = await ReviewerExists(input.ReviewerEmployeeId.Value, cancellationToken);
            if (!ok) return IdeaOperationResult.Failure("Người review phải là Quản lý thuộc phòng Ý tưởng.");
        }

        idea.Title = input.Title.Trim();
        idea.ClientName = input.ClientName.Trim();
        idea.CampaignName = string.IsNullOrWhiteSpace(input.CampaignName) ? null : input.CampaignName.Trim();
        idea.Industry = string.IsNullOrWhiteSpace(input.Industry) ? string.Empty : input.Industry.Trim();
        idea.Category = category;
        idea.Insight = string.IsNullOrWhiteSpace(input.Insight) ? null : input.Insight.Trim();
        idea.Concept = string.IsNullOrWhiteSpace(input.Concept) ? null : input.Concept.Trim();
        idea.ContentDetails = string.IsNullOrWhiteSpace(input.ContentDetails) ? null : input.ContentDetails.Trim();
        idea.ReferenceLink = string.IsNullOrWhiteSpace(input.ReferenceLink) ? null : input.ReferenceLink.Trim();
        idea.MoodboardDesc = string.IsNullOrWhiteSpace(input.MoodboardDesc) ? null : input.MoodboardDesc.Trim();
        idea.ScriptText = string.IsNullOrWhiteSpace(input.ScriptText) ? null : input.ScriptText.Trim();
        idea.Deadline = input.Deadline;
        idea.PrimaryStaffId = input.PrimaryStaffId;
        idea.ReviewerEmployeeId = input.ReviewerEmployeeId;
        idea.UpdatedAt = DateTime.UtcNow;

        _auditService.AddEvent(new AuditEvent(
            AuditModules.Ideas,
            "idea_updated",
            $"Cập nhật nội dung ý tưởng '{idea.Title}'.",
            actorUserId,
            "Idea",
            idea.Id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);

        return IdeaOperationResult.Success("Đã cập nhật ý tưởng.", idea.Id);
    }

    // ===========================================================================
    // ============================ TRANSITION ===================================
    // ===========================================================================

    public async Task<IdeaOperationResult> TransitionAsync(
        IdeaTransitionInputModel input, int actorUserId, string actorRole, int? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        if (!IdeaStatuses.IsValid(input.TargetStatus))
            return IdeaOperationResult.Failure("Trạng thái đích không hợp lệ.");

        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == input.Id, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy � tưởng.");

        var currentStatus = idea.Status ?? IdeaStatuses.Draft;
        var allowed = GetAllowedTransitionsRaw(idea, actorRole, actorEmployeeId);
        if (!allowed.Contains(input.TargetStatus))
            return IdeaOperationResult.Failure("Bạn không có quyền thực hiện bước chuyển trạng thái này.");

        var comment = input.Comment?.Trim();
        var requiresComment = input.TargetStatus == IdeaStatuses.NeedRevision;
        if (requiresComment && string.IsNullOrWhiteSpace(comment))
            return IdeaOperationResult.Failure("Cần nhập nội dung yêu cầu ch�nh sửa.");
        if (comment?.Length > 2000)
            return IdeaOperationResult.Failure("Ghi chú không được vượt quá 2000 ký tự.");

        var previousStatus = idea.Status;
        idea.Status = input.TargetStatus;
        idea.UpdatedAt = DateTime.UtcNow;
        if (requiresComment)
        {
            idea.FeedbackComment = comment;
        }

        // Nếu là bước chuyển "Chỉnh sửa" → thêm 1 comment vào lịch sử.
        if (requiresComment && !string.IsNullOrWhiteSpace(comment))
        {
            _context.IdeaComments.Add(new IdeaComment
            {
                IdeaId = idea.Id,
                AuthorUserId = actorUserId,
                CommentType = IdeaCommentTypes.RevisionRequest,
                Content = comment!,
                CreatedAt = DateTime.UtcNow
            });
        }

        _auditService.AddEvent(new AuditEvent(
            AuditModules.Ideas,
            input.TargetStatus == IdeaStatuses.Approved ? "idea_approved" :
            input.TargetStatus == IdeaStatuses.NeedRevision ? "idea_revision_requested" :
            input.TargetStatus == IdeaStatuses.Done ? "idea_done" :
            input.TargetStatus == IdeaStatuses.InProduction ? "idea_in_production" :
            "idea_status_changed",
            $"Ý tưởng '{idea.Title}': {IdeaStatuses.GetLabel(previousStatus ?? IdeaStatuses.Draft)} → {IdeaStatuses.GetLabel(input.TargetStatus)}" +
            (string.IsNullOrWhiteSpace(comment) ? "." : $". Ghi chú: {comment}"),
            actorUserId,
            "Idea",
            idea.Id.ToString()));

        await _context.SaveChangesAsync(cancellationToken);
        return IdeaOperationResult.Success(
            $"Đã chuyển ý tưởng sang trạng thái \"{IdeaStatuses.GetLabel(input.TargetStatus)}\".",
            idea.Id);
    }

    // ===========================================================================
    // ============================ ADD COMMENT ==================================
    // ===========================================================================

    public async Task<IdeaOperationResult> AddCommentAsync(
        IdeaCommentInputModel input, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (!CanComment(actorRole))
            return IdeaOperationResult.Failure("Tài khoản không có quyền bình luận ý tưởng.");

        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == input.IdeaId, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy ý tưởng.");

        var commentType = string.IsNullOrWhiteSpace(input.CommentType) ? IdeaCommentTypes.General : input.CommentType.Trim();
        if (!IdeaCommentTypes.IsValid(commentType))
            return IdeaOperationResult.Failure("Loại comment không hợp lệ.");

        _context.IdeaComments.Add(new IdeaComment
        {
            IdeaId = idea.Id,
            AuthorUserId = actorUserId,
            CommentType = commentType,
            Content = input.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        });

        _auditService.AddEvent(new AuditEvent(
            AuditModules.Ideas,
            "idea_commented",
            $"Bình luận mới trên ý tưởng '{idea.Title}'.",
            actorUserId,
            "Idea",
            idea.Id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);

        return IdeaOperationResult.Success("Đã thêm bình luận.", idea.Id);
    }

    // ===========================================================================
    // ============================ TRANSITION RULES =============================
    // ===========================================================================

    /// <summary>
    /// Trả về danh sách các bước chuyển trạng thái mà user hiện tại có thể thực hiện trên ý tưởng này.
    /// </summary>
    private static IReadOnlyList<IdeaTransitionOptionViewModel> GetAllowedTransitions(
        Idea idea, string actorRole, int? actorEmployeeId)
    {
        var raw = GetAllowedTransitionsRaw(idea, actorRole, actorEmployeeId);
        return raw.Select(status => BuildOption(status, idea)).ToList();
    }

    private static IdeaTransitionOptionViewModel BuildOption(string status, Idea idea)
    {
        return status switch
        {
            IdeaStatuses.Review => new IdeaTransitionOptionViewModel(
                IdeaStatuses.Review, "Gửi review", false, true, "approve"),
            IdeaStatuses.NeedRevision => new IdeaTransitionOptionViewModel(
                IdeaStatuses.NeedRevision, "Yêu cầu chỉnh sửa", true, false, "reject"),
            IdeaStatuses.Approved => new IdeaTransitionOptionViewModel(
                IdeaStatuses.Approved, "Duyệt ý tưởng", false, false, "approve"),
            IdeaStatuses.InProduction => new IdeaTransitionOptionViewModel(
                IdeaStatuses.InProduction, "Chuyển sang triển khai", false, false, "approve"),
            IdeaStatuses.Done => new IdeaTransitionOptionViewModel(
                IdeaStatuses.Done, "Đánh dấu hoàn thành", false, false, "approve"),
            _ => new IdeaTransitionOptionViewModel(
                status, IdeaStatuses.GetLabel(status), false, false, string.Empty)
        };
    }

    /// <summary>
    /// Logic chặn cứng quyền chuyển trạng thái — đúng theo yêu cầu:
    ///   NV Ý tưởng: chỉ được chuyển "y_tuong" → "review" và "need_revision" → "review"
    ///   QL Ý tưởng: đủ quyền trên mọi bước
    ///   Giám đốc / Booking / NV Booking / AdminIT / HCNS: không có quyền chuyển
    /// </summary>
    private static HashSet<string> GetAllowedTransitionsRaw(Idea idea, string actorRole, int? actorEmployeeId)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var currentStatus = idea.Status ?? IdeaStatuses.Draft;

        if (actorRole == AppRoles.IdeaManager)
        {
            if (currentStatus == IdeaStatuses.Review)
            {
                result.Add(IdeaStatuses.NeedRevision);
                result.Add(IdeaStatuses.Approved);
            }
            else if (currentStatus == IdeaStatuses.Approved)
            {
                result.Add(IdeaStatuses.InProduction);
            }
            else if (currentStatus == IdeaStatuses.InProduction)
            {
                result.Add(IdeaStatuses.Done);
            }
            return result;
        }

        if (actorRole == AppRoles.IdeaStaff && actorEmployeeId.HasValue)
        {
            // NV Ý tưởng ch� được tự chuyển sang "review" ở 2 thời điểm:
            // (1) Vừa tạo xong (status = y_tuong) → gửi review
            // (2) Sau khi QL yêu cầu sửa (status = need_revision) → gửi lại
            var isOwner = idea.CreatorEmployeeId == actorEmployeeId.Value
                || idea.PrimaryStaffId == actorEmployeeId.Value;
            if (isOwner &&
                (currentStatus == IdeaStatuses.Draft || currentStatus == IdeaStatuses.NeedRevision))
            {
                result.Add(IdeaStatuses.Review);
            }
            return result;
        }

        // Giám đốc, QL/NV Booking, AdminIT, HCNS — không có bước chuyển nào qua service.
        return result;
    }

    // ===========================================================================
    // ============================ ACCESS HELPERS ===============================
    // ===========================================================================

    private static bool CanCreate(string role) =>
        role == AppRoles.IdeaManager || role == AppRoles.IdeaStaff;

    private static bool CanTransition(string role) =>
        role == AppRoles.IdeaManager || role == AppRoles.IdeaStaff;

    private static bool CanComment(string role) =>
        role == AppRoles.IdeaManager || role == AppRoles.IdeaStaff
        || role == AppRoles.Director || role == AppRoles.BookingManager || role == AppRoles.BookingStaff;

    /// <summary>
    /// Quyền edit nội dung: NV Ý tưởng chỉ sửa được ý tưởng của mình (tạo/phụ trách) và chỉ khi
    /// chưa duyệt. QL Ý tưởng sửa được mọi ý tưởng trong phòng ban (khi cần sửa nội dung).
    /// </summary>
    private static bool CanEdit(string role, int? actorEmployeeId) =>
        role == AppRoles.IdeaManager || role == AppRoles.IdeaStaff;

    private static bool CanEditThisIdea(Idea idea, string actorRole, int? actorEmployeeId)
    {
        if (actorRole == AppRoles.IdeaManager) return true;
        if (actorRole == AppRoles.IdeaStaff && actorEmployeeId.HasValue)
        {
            return idea.CreatorEmployeeId == actorEmployeeId.Value
                || idea.PrimaryStaffId == actorEmployeeId.Value;
        }
        return false;
    }

    private static void EnsureViewAccess(string role)
    {
        // Chỉ các role sau được xem: Giám đốc, QL � tưởng, NV Ý tưởng, QL Booking, NV Booking.
        // AdminIT / HCNS — không có quyền, throw để controller trả 403.
        var allowList = new[]
        {
            AppRoles.Director,
            AppRoles.IdeaManager,
            AppRoles.IdeaStaff,
            AppRoles.BookingManager,
            AppRoles.BookingStaff
        };
        if (!allowList.Contains(role))
        {
            throw new UnauthorizedAccessException("Tài khoản không có quyền truy cập Module Ý tưởng.");
        }
    }

    private async Task<bool> EmployeeExistsAndBelongsToIdeaDept(int employeeId, CancellationToken cancellationToken)
    {
        return await _context.Employees.AsNoTracking()
            .AnyAsync(employee =>
                employee.Id == employeeId &&
                employee.Department == IdeaDepartment,
                cancellationToken);
    }

    /// <summary>
    /// Reviewer hợp lệ phải là QL Ý tưởng: employee thuộc phòng Y_tuong và có IsManager=true.
    /// </summary>
    private async Task<bool> ReviewerExists(int employeeId, CancellationToken cancellationToken)
    {
        return await _context.Employees.AsNoTracking()
            .AnyAsync(employee =>
                employee.Id == employeeId &&
                employee.Department == IdeaDepartment &&
                employee.IsManager,
                cancellationToken);
    }
}
