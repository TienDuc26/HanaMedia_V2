using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Ideas;

public sealed class IdeaLibraryService : IIdeaLibraryService
{
    private const int PageSize = 20;
    private readonly ApplicationDbContext _context;
    private readonly ISystemAuditService _auditService;

    public IdeaLibraryService(ApplicationDbContext context, ISystemAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<IdeaLibraryPageViewModel> GetPageAsync(
        int actorUserId,
        string actorRole,
        IdeaLibraryQueryModel query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var actorEmployeeId = await GetActorEmployeeIdAsync(actorUserId, cancellationToken);
        var ideas = BuildVisibleQuery(actorRole, actorEmployeeId);
        var search = Normalize(query.Search);
        var industry = Normalize(query.Industry);
        var client = Normalize(query.Client);
        var category = Normalize(query.Category);

        if (search is not null)
        {
            ideas = ideas.Where(item =>
                item.Title.Contains(search) ||
                item.ClientName.Contains(search) ||
                item.Industry.Contains(search) ||
                (item.CampaignName ?? string.Empty).Contains(search) ||
                (item.Insight ?? string.Empty).Contains(search) ||
                (item.Concept ?? string.Empty).Contains(search));
        }

        if (industry is not null) ideas = ideas.Where(item => item.Industry == industry);
        if (client is not null) ideas = ideas.Where(item => item.ClientName == client);
        if (query.CampaignId is > 0) ideas = ideas.Where(item => item.CampaignId == query.CampaignId);
        if (category is not null)
        {
            if (!IdeaLibraryCategories.IsValid(category))
                throw new ArgumentException("Phân loại kho ý tưởng không hợp lệ.", nameof(query));
            ideas = ideas.Where(item => item.Category == category);
        }

        var totalItems = await ideas.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);
        var items = await ideas
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var filterSource = BuildVisibleQuery(actorRole, actorEmployeeId);
        var industries = await filterSource.Select(item => item.Industry).Distinct()
            .OrderBy(value => value).ToListAsync(cancellationToken);
        var clients = await filterSource.Select(item => item.ClientName).Distinct()
            .OrderBy(value => value).ToListAsync(cancellationToken);
        var campaigns = await filterSource.Where(item => item.CampaignId != null)
            .Select(item => new { Id = item.CampaignId!.Value, Name = item.CampaignName!, Client = item.ClientName })
            .Distinct().OrderBy(item => item.Name)
            .Select(item => new IdeaCampaignOptionViewModel(item.Id, item.Name, item.Client))
            .ToListAsync(cancellationToken);

        return new IdeaLibraryPageViewModel
        {
            Items = items.Select(MapItem).ToList(),
            Industries = industries,
            Clients = clients,
            Campaigns = campaigns,
            Categories = IdeaLibraryCategories.All
                .Select(code => new IdeaLibraryCategoryViewModel(code, IdeaLibraryCategories.GetLabel(code)))
                .ToList(),
            Page = page,
            PageSize = PageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public async Task<IdeaLibraryItemViewModel?> GetByIdAsync(
        int id,
        int actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var actorEmployeeId = await GetActorEmployeeIdAsync(actorUserId, cancellationToken);
        var idea = await BuildVisibleQuery(actorRole, actorEmployeeId)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return idea is null ? null : MapItem(idea);
    }

    public async Task<IdeaOperationResult> UpdateClassificationAsync(
        int id,
        UpdateIdeaClassificationInputModel input,
        int actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.IdeaManager)
            return IdeaOperationResult.Failure("Chỉ Quản lý Ý tưởng được cập nhật phân loại kho ý tưởng.");

        var industry = Normalize(input.Industry);
        var category = Normalize(input.Category);
        if (industry is null || industry.Length is < 2 or > 100)
            return IdeaOperationResult.Failure("Ngành hàng phải có từ 2 đến 100 ký tự.");
        if (!IdeaLibraryCategories.IsValid(category))
            return IdeaOperationResult.Failure("Phân loại kho ý tưởng không hợp lệ.");

        var idea = await _context.Ideas.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (idea is null) return IdeaOperationResult.Failure("Không tìm thấy ý tưởng.");

        var oldIndustry = idea.Industry;
        var oldCategory = idea.Category;
        idea.Industry = industry;
        idea.Category = category!;
        idea.UpdatedAt = DateTime.Now;
        _auditService.AddEvent(new AuditEvent(
            AuditModules.Ideas,
            AuditActions.Updated,
            $"Phân loại ý tưởng '{idea.Title}': ngành hàng '{oldIndustry}' → '{industry}', " +
            $"nhóm '{IdeaLibraryCategories.GetLabel(oldCategory)}' → '{IdeaLibraryCategories.GetLabel(category!)}'.",
            actorUserId,
            "Idea",
            idea.Id.ToString()));
        await _context.SaveChangesAsync(cancellationToken);
        return IdeaOperationResult.Success("Đã cập nhật phân loại kho ý tưởng.");
    }

    private IQueryable<Idea> BuildVisibleQuery(string actorRole, int? actorEmployeeId)
    {
        var query = _context.Ideas.AsNoTracking()
            .Include(item => item.Campaign)
            .Include(item => item.CreatorEmployee)
            .Include(item => item.PrimaryStaff)
            .Include(item => item.MoodboardImages)
            .AsQueryable();

        if (actorRole is AppRoles.Director or AppRoles.IdeaManager) return query;
        if (actorRole != AppRoles.IdeaStaff || !actorEmployeeId.HasValue)
            return query.Where(_ => false);

        var employeeId = actorEmployeeId.Value;
        return query.Where(item =>
            item.CreatorEmployeeId == employeeId ||
            item.PrimaryStaffId == employeeId ||
            item.Status == IdeaStatuses.Approved ||
            item.Status == IdeaStatuses.InProgress ||
            item.Status == IdeaStatuses.Done);
    }

    private async Task<int?> GetActorEmployeeIdAsync(int userId, CancellationToken cancellationToken) =>
        await _context.Employees.AsNoTracking()
            .Where(employee => employee.UserId == userId)
            .Select(employee => (int?)employee.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static IdeaLibraryItemViewModel MapItem(Idea idea) => new()
    {
        Id = idea.Id,
        Title = idea.Title,
        Industry = idea.Industry,
        Client = idea.Campaign?.Client ?? idea.ClientName,
        CampaignId = idea.CampaignId,
        CampaignName = idea.Campaign?.Name ?? idea.CampaignName,
        Category = idea.Category,
        CategoryLabel = IdeaLibraryCategories.GetLabel(idea.Category),
        Status = idea.Status ?? IdeaStatuses.Idea,
        StatusLabel = IdeaStatuses.GetLabel(idea.Status ?? IdeaStatuses.Idea),
        Insight = idea.Insight,
        Concept = idea.Concept,
        ContentDetails = idea.ContentDetails,
        ReferenceLink = idea.ReferenceLink,
        ReferenceFileUrl = idea.ReferenceFileUrl,
        MoodboardDescription = idea.MoodboardDesc,
        MoodboardImages = idea.MoodboardImages.OrderBy(image => image.SortOrder).ThenBy(image => image.Id)
            .Select(image => new IdeaMoodboardImageViewModel(image.Id, image.FileUrl, image.SortOrder)).ToList(),
        Script = idea.ScriptText,
        CreatorName = idea.CreatorEmployee?.FullName ?? "-",
        PrimaryStaffName = idea.PrimaryStaff?.FullName ?? "-",
        Deadline = idea.Deadline,
        UpdatedAt = idea.UpdatedAt
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
