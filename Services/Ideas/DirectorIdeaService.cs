using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Ideas;

public sealed class DirectorIdeaService : IDirectorIdeaService
{
    private const int PageSize = 20;
    private readonly ApplicationDbContext _context;
    private readonly ISystemAuditService _auditService;

    public DirectorIdeaService(ApplicationDbContext context, ISystemAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<DirectorIdeaPageViewModel> GetPageAsync(
        string? search,
        string? status,
        string? directorStatus,
        int page,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = Normalize(search);
        var query = _context.Ideas.AsNoTracking()
            .Include(item => item.Campaign)
            .Include(item => item.CreatorEmployee)
            .Include(item => item.PrimaryStaff)
            .Include(item => item.ReviewerEmployee)
            .Include(item => item.MoodboardImages)
            .Include(item => item.Comments).ThenInclude(comment => comment.AuthorUser)
            .AsSplitQuery()
            .AsQueryable();

        if (normalizedSearch is not null)
        {
            query = query.Where(item => item.Title.Contains(normalizedSearch) ||
                item.ClientName.Contains(normalizedSearch) ||
                (item.CampaignName ?? string.Empty).Contains(normalizedSearch));
        }
        if (IdeaStatuses.IsValid(status)) query = query.Where(item => item.Status == status);
        if (DirectorIdeaReviewStatuses.IsValid(directorStatus))
            query = query.Where(item => item.DirectorReviewStatus == directorStatus);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        var ideas = await query.OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync(cancellationToken);

        return new DirectorIdeaPageViewModel
        {
            Search = normalizedSearch,
            Status = status,
            DirectorStatus = directorStatus,
            Page = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            Items = ideas.Select(MapItem).ToList()
        };
    }

    public async Task<IdeaOperationResult> UpdateContentAsync(
        DirectorEditIdeaInputModel input,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var idea = await _context.Ideas.FirstOrDefaultAsync(item => item.Id == input.Id, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy ý tưởng.");

        idea.Insight = Normalize(input.Insight);
        idea.Concept = Normalize(input.Concept);
        idea.ContentDetails = Normalize(input.ContentDetails);
        idea.ScriptText = Normalize(input.ScriptText);
        idea.UpdatedAt = DateTime.Now;
        _auditService.AddEvent(new AuditEvent(AuditModules.Ideas, AuditActions.Updated,
            $"Giám đốc chỉnh sửa trực tiếp nội dung ý tưởng '{idea.Title}'.",
            actorUserId, "Idea", idea.Id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);
        return IdeaOperationResult.Success("Đã lưu nội dung chỉnh sửa của Giám đốc.");
    }

    public async Task<IdeaOperationResult> SendFeedbackAsync(
        int id,
        string? feedback,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedFeedback = Normalize(feedback);
        if (normalizedFeedback is null || normalizedFeedback.Length > 2000)
            return IdeaOperationResult.Failure("Feedback của Giám đốc phải có từ 1 đến 2000 ký tự.");

        var idea = await _context.Ideas.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy ý tưởng.");

        idea.DirectorReviewStatus = DirectorIdeaReviewStatuses.RevisionRequested;
        idea.DirectorFeedback = normalizedFeedback;
        idea.DirectorReviewedByUserId = actorUserId;
        idea.DirectorReviewedAt = DateTime.Now;
        idea.UpdatedAt = DateTime.Now;
        AddComment(idea.Id, actorUserId, normalizedFeedback, IdeaCommentTypes.DirectorFeedback);
        _auditService.AddEvent(new AuditEvent(AuditModules.Ideas, AuditActions.Rejected,
            $"Giám đốc yêu cầu chỉnh sửa ý tưởng '{idea.Title}'.",
            actorUserId, "Idea", idea.Id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);
        return IdeaOperationResult.Success("Đã gửi feedback; nhân viên có thể chỉnh sửa và gửi lại Giám đốc.");
    }

    public async Task<IdeaOperationResult> DecideAsync(
        int id,
        string decision,
        string? reason,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (decision is not ("approve" or "reject"))
            return IdeaOperationResult.Failure("Quyết định của Giám đốc không hợp lệ.");
        var normalizedReason = Normalize(reason);
        if (decision == "reject" && normalizedReason is null)
            return IdeaOperationResult.Failure("Vui lòng nhập lý do từ chối.");
        if (normalizedReason?.Length > 2000)
            return IdeaOperationResult.Failure("Nội dung nhận xét không được vượt quá 2000 ký tự.");

        var idea = await _context.Ideas.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy ý tưởng.");

        var approved = decision == "approve";
        idea.DirectorReviewStatus = approved
            ? DirectorIdeaReviewStatuses.Approved
            : DirectorIdeaReviewStatuses.Rejected;
        idea.DirectorFeedback = normalizedReason;
        idea.DirectorReviewedByUserId = actorUserId;
        idea.DirectorReviewedAt = DateTime.Now;
        idea.UpdatedAt = DateTime.Now;
        if (normalizedReason is not null)
            AddComment(idea.Id, actorUserId, normalizedReason, IdeaCommentTypes.DirectorDecision);
        _auditService.AddEvent(new AuditEvent(
            AuditModules.Ideas,
            approved ? AuditActions.Approved : AuditActions.Rejected,
            $"Giám đốc {(approved ? "duyệt" : "từ chối")} ý tưởng '{idea.Title}' theo luồng độc lập Module 15.",
            actorUserId,
            "Idea",
            idea.Id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);
        return IdeaOperationResult.Success(approved
            ? "Giám đốc đã duyệt ý tưởng."
            : "Giám đốc đã từ chối ý tưởng.");
    }

    private void AddComment(int ideaId, int userId, string content, string commentType) =>
        _context.IdeaComments.Add(new IdeaComment
        {
            IdeaId = ideaId,
            AuthorUserId = userId,
            CommentType = commentType,
            Content = content,
            CreatedAt = DateTime.Now
        });

    private static DirectorIdeaItemViewModel MapItem(Idea idea) => new()
    {
        Id = idea.Id,
        Title = idea.Title,
        CreatorName = idea.CreatorEmployee?.FullName ?? "-",
        ClientName = idea.Campaign?.Client ?? idea.ClientName,
        CampaignName = idea.Campaign?.Name ?? idea.CampaignName ?? "-",
        PrimaryStaffName = idea.PrimaryStaff?.FullName ?? "-",
        ReviewerName = idea.ReviewerEmployee?.FullName ?? "-",
        Insight = idea.Insight,
        Concept = idea.Concept,
        ContentDetails = idea.ContentDetails,
        ReferenceLink = idea.ReferenceLink,
        ReferenceFileUrl = idea.ReferenceFileUrl,
        MoodboardDescription = idea.MoodboardDesc,
        MoodboardImages = idea.MoodboardImages.OrderBy(image => image.SortOrder).ThenBy(image => image.Id)
            .Select(image => new IdeaMoodboardImageViewModel(image.Id, image.FileUrl, image.SortOrder)).ToList(),
        ScriptText = idea.ScriptText,
        Deadline = idea.Deadline,
        Status = idea.Status ?? IdeaStatuses.Idea,
        StatusLabel = IdeaStatuses.GetLabel(idea.Status ?? IdeaStatuses.Idea),
        ManagerFeedback = idea.FeedbackComment,
        DirectorReviewStatus = idea.DirectorReviewStatus,
        DirectorReviewStatusLabel = DirectorIdeaReviewStatuses.GetLabel(idea.DirectorReviewStatus),
        DirectorFeedback = idea.DirectorFeedback,
        DirectorReviewedAt = idea.DirectorReviewedAt,
        Comments = idea.Comments.OrderByDescending(comment => comment.CreatedAt)
            .Select(comment => new IdeaCommentViewModel(
                comment.AuthorUser?.Username ?? "Tài khoản đã xóa",
                comment.AuthorUser is null ? "-" : AppRoles.GetLabel(comment.AuthorUser.Role),
                comment.Content,
                comment.CreatedAt)).ToList()
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
