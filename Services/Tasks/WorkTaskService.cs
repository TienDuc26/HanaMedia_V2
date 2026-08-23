using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Tasks;

public sealed class WorkTaskService : IWorkTaskService
{
    private const int PageSize = 20;
    private static readonly string[] ActiveEmployeeStatuses = ["dang_lam_viec", "thu_viec"];
    private readonly ApplicationDbContext _context;
    private readonly ISystemAuditService _auditService;

    public WorkTaskService(ApplicationDbContext context, ISystemAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<WorkTaskPageViewModel> GetPageAsync(
        int actorUserId, string actorRole, string? module, string? search, int page,
        int? employeeId = null, bool openCreate = false,
        int? prefillIdeaId = null,
        CancellationToken cancellationToken = default)
    {
        var allowedModules = GetAllowedModules(actorRole);
        if (allowedModules.Count == 0)
        {
            throw new UnauthorizedAccessException("Tài khoản không có quyền sử dụng phân hệ công việc.");
        }

        var selectedModule = WorkTaskModules.IsValid(module) && allowedModules.Contains(module!)
            ? module!
            : allowedModules[0];
        page = Math.Max(1, page);
        var normalizedSearch = search?.Trim();

        var query = _context.WorkTasks.AsNoTracking()
            .Include(task => task.AssignedEmployee)
            .Include(task => task.CreatedByUser)
            .Include(task => task.ReviewerUser)
            .Include(task => task.Idea)
            .Where(task => task.Module == selectedModule);

        if (!IsManagerRole(actorRole) && actorRole != AppRoles.Director)
        {
            query = query.Where(task => task.AssignedEmployee.UserId == actorUserId);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(task =>
                EF.Functions.Like(task.Title, pattern) ||
                EF.Functions.Like(task.AssignedEmployee.FullName, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Min(page, totalPages);
        var entities = await query
            .OrderBy(task => task.Status == WorkTaskStatuses.Done)
            .ThenBy(task => task.Deadline)
            .ThenByDescending(task => task.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var employees = new List<WorkTaskEmployeeOptionViewModel>();
        var activeEmployeesWithoutAccount = 0;
        if (CanCreate(actorRole))
        {
            // Chỉ lấy nhân sự của phân hệ đang chọn, không gộp của nhiều phân hệ (tránh dropdown
            // bị trộn giữa HCNS/Booking/Y tưởng). Fix bug: trước đây filterEmployees() phải
            // client-side ẩn option, dễ hiển thị nhầm; giờ server chỉ trả đúng phân hệ.
            var selectedDepartment = WorkTaskModules.GetDepartment(selectedModule);
            employees = await _context.Employees.AsNoTracking()
                .Where(employee =>
                    employee.UserId.HasValue &&
                    employee.Department == selectedDepartment &&
                    ActiveEmployeeStatuses.Contains(employee.Status!))
                .OrderBy(employee => employee.FullName)
                .Select(employee => new WorkTaskEmployeeOptionViewModel(employee.Id, employee.FullName, employee.Department))
                .ToListAsync(cancellationToken);

            activeEmployeesWithoutAccount = await _context.Employees.AsNoTracking()
                .CountAsync(employee =>
                    !employee.UserId.HasValue &&
                    employee.Department == WorkTaskModules.GetDepartment(selectedModule) &&
                    ActiveEmployeeStatuses.Contains(employee.Status!), cancellationToken);
        }

        var selectedEmployeeId = employeeId.HasValue && employees.Any(item => item.Id == employeeId.Value)
            ? employeeId
            : null;

        // Chỉ Module = Ideas mới cần dropdown ý tưởng + hạng mục.
        var ideaOptions = new List<WorkTaskIdeaOptionViewModel>();
        var workCategories = new List<string>();
        string? prefillIdeaTitle = null;
        if (selectedModule == WorkTaskModules.Ideas && CanCreate(actorRole))
        {
            ideaOptions = await _context.Ideas.AsNoTracking()
                .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
                .Take(200)
                .Select(i => new WorkTaskIdeaOptionViewModel(i.Id, i.Title, i.Status ?? "y_tuong", i.Status ?? "y_tuong"))
                .ToListAsync(cancellationToken);
            // Bổ sung label cho trạng thái ý tưởng ở server side.
            ideaOptions = ideaOptions.Select(i => i with { StatusLabel = IdeaStatuses.GetLabel(i.Status) }).ToList();
            workCategories = IdeaTaskCategories.All.ToList();
            if (prefillIdeaId.HasValue)
            {
                prefillIdeaTitle = ideaOptions.FirstOrDefault(i => i.Id == prefillIdeaId.Value)?.Title;
            }
        }

        return new WorkTaskPageViewModel
        {
            Module = selectedModule,
            ModuleLabel = WorkTaskModules.GetLabel(selectedModule),
            CanCreate = CanCreate(actorRole),
            Search = normalizedSearch,
            Page = page,
            TotalPages = totalPages,
            SelectedEmployeeId = selectedEmployeeId,
            // Mở modal khi có openCreate=true (kể cả khi chưa chọn nhân viên — chỉ QL mới truy cập).
            OpenCreateModal = openCreate,
            ActiveEmployeesWithoutAccount = activeEmployeesWithoutAccount,
            Employees = employees,
            AvailableModules = allowedModules.Select(item => new WorkTaskModuleOptionViewModel(item, WorkTaskModules.GetLabel(item))).ToList(),
            IdeaOptions = ideaOptions,
            WorkCategories = workCategories,
            PrefillIdeaId = prefillIdeaId,
            PrefillIdeaTitle = prefillIdeaTitle,
            Tasks = entities.Select(task => new WorkTaskListItemViewModel
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                Module = task.Module,
                ModuleLabel = WorkTaskModules.GetLabel(task.Module),
                EmployeeName = task.AssignedEmployee.FullName,
                CreatorName = task.CreatedByUser.Username,
                ReviewerName = task.ReviewerUser.Username,
                Deadline = task.Deadline,
                Status = task.Status,
                StatusLabel = WorkTaskStatuses.GetLabel(task.Status),
                RowVersion = Convert.ToBase64String(task.RowVersion),
                AllowedTransitions = GetAllowedTransitions(task, actorUserId, actorRole),
                IdeaId = task.IdeaId,
                IdeaTitle = task.Idea?.Title,
                IdeaStatusLabel = task.Idea?.Status is null ? null : IdeaStatuses.GetLabel(task.Idea.Status),
                WorkCategory = task.WorkCategory,
                WorkCategoryLabel = task.WorkCategory is null ? null : IdeaTaskCategories.GetLabel(task.WorkCategory)
            }).ToList()
        };
    }

    private static bool HasPrefillIdeaId(int? employeeId) => false; // marker, không dùng — logic thực ở controller

    public async Task<WorkTaskOperationResult> CreateAsync(
        CreateWorkTaskInputModel input, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (!CanCreate(actorRole)) return WorkTaskOperationResult.Failure("Tài khoản không có quyền giao việc.");
        var module = ForceModuleForRole(actorRole, input.Module);
        if (!WorkTaskModules.IsValid(module)) return WorkTaskOperationResult.Failure("Phân hệ công việc không hợp lệ.");
        var title = input.Title?.Trim() ?? string.Empty;
        if (title.Length is < 3 or > 200) return WorkTaskOperationResult.Failure("Tiêu đề phải có từ 3 đến 200 ký tự.");
        var description = input.Description?.Trim();
        if (description?.Length > 2000) return WorkTaskOperationResult.Failure("Mô tả không được vượt quá 2000 ký tự.");
        if (input.Deadline.Date < DateTime.Today) return WorkTaskOperationResult.Failure("Deadline không được nằm trong quá khứ.");

        var department = WorkTaskModules.GetDepartment(module);
        var employee = await _context.Employees
            .FirstOrDefaultAsync(item => item.Id == input.AssignedEmployeeId, cancellationToken);
        if (employee is null || employee.Department != department || !employee.UserId.HasValue ||
            !ActiveEmployeeStatuses.Contains(employee.Status ?? string.Empty))
        {
            return WorkTaskOperationResult.Failure("Nhân sự được giao phải là nhân sự thật, đang hoạt động, có tài khoản và thuộc đúng phòng ban.");
        }

        var reviewerId = await ResolveReviewerIdAsync(actorUserId, actorRole, module, cancellationToken);
        if (!reviewerId.HasValue)
        {
            return WorkTaskOperationResult.Failure("Chưa có tài khoản đủ quyền duyệt công việc cho phân hệ này.");
        }

        // Validate IdeaId nếu có: phải tồn tại thực sự.
        if (input.IdeaId.HasValue)
        {
            var ideaExists = await _context.Ideas.AsNoTracking()
                .AnyAsync(i => i.Id == input.IdeaId.Value, cancellationToken);
            if (!ideaExists)
            {
                return WorkTaskOperationResult.Failure("Ý tưởng liên kết không tồn tại.");
            }
            // Khi gắn với ý tưởng → WorkCategory bắt buộc để NV biết đang làm phần nào.
            var category = input.WorkCategory?.Trim();
            if (string.IsNullOrWhiteSpace(category))
            {
                return WorkTaskOperationResult.Failure("Vui lòng chọn hạng mục công việc cho task gắn với ý tưởng.");
            }
            if (!IdeaTaskCategories.All.Contains(category))
            {
                return WorkTaskOperationResult.Failure("Hạng mục công việc không hợp lệ.");
            }
            input.WorkCategory = category;
        }
        else
        {
            // Không gắn ý tưởng → reset về null để khỏi lưu giá trị rác.
            input.WorkCategory = null;
        }

        var now = DateTime.UtcNow;
        var task = new WorkTask
        {
            Title = title,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            Module = module,
            AssignedEmployeeId = employee.Id,
            CreatedByUserId = actorUserId,
            ReviewerUserId = reviewerId.Value,
            Deadline = input.Deadline,
            Status = WorkTaskStatuses.Todo,
            IdeaId = input.IdeaId,
            WorkCategory = input.WorkCategory,
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.WorkTasks.Add(task);
        task.History.Add(new WorkTaskHistory
        {
            ActorUserId = actorUserId,
            ToStatus = WorkTaskStatuses.Todo,
            Comment = "Công việc được tạo",
            CreatedAt = now
        });
        _auditService.AddEvent(new AuditEvent(module, "task_created",
            $"Tạo công việc '{title}' cho {employee.FullName}, deadline {input.Deadline:dd/MM/yyyy}.",
            actorUserId, "WorkTask"));
        await _context.SaveChangesAsync(cancellationToken);
        return WorkTaskOperationResult.Success("Đã giao công việc thành công.");
    }

    public async Task<WorkTaskOperationResult> TransitionAsync(
        TransitionWorkTaskInputModel input, int actorUserId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (!WorkTaskStatuses.All.Contains(input.TargetStatus))
            return WorkTaskOperationResult.Failure("Trạng thái đích không hợp lệ.");
        byte[] expectedVersion;
        try { expectedVersion = Convert.FromBase64String(input.RowVersion); }
        catch (FormatException) { return WorkTaskOperationResult.Failure("Phiên bản công việc không hợp lệ. Hãy tải lại trang."); }

        var task = await _context.WorkTasks
            .Include(item => item.AssignedEmployee)
            .FirstOrDefaultAsync(item => item.Id == input.Id, cancellationToken);
        if (task is null) return WorkTaskOperationResult.Failure("Không tìm thấy công việc.");
        if (!GetAllowedTransitions(task, actorUserId, actorRole).Any(item => item.Status == input.TargetStatus))
            return WorkTaskOperationResult.Failure("Bạn không có quyền thực hiện bước chuyển trạng thái này.");

        var comment = input.Comment?.Trim();
        if (input.TargetStatus == WorkTaskStatuses.NeedRevision && string.IsNullOrWhiteSpace(comment))
            return WorkTaskOperationResult.Failure("Cần nhập nội dung yêu cầu chỉnh sửa.");
        if (comment?.Length > 1000) return WorkTaskOperationResult.Failure("Ghi chú không được vượt quá 1000 ký tự.");

        _context.Entry(task).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        var previousStatus = task.Status;
        task.Status = input.TargetStatus;
        task.UpdatedAt = DateTime.UtcNow;
        task.CompletedAt = input.TargetStatus == WorkTaskStatuses.Done ? DateTime.UtcNow : null;
        task.History.Add(new WorkTaskHistory
        {
            ActorUserId = actorUserId,
            FromStatus = previousStatus,
            ToStatus = input.TargetStatus,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment,
            CreatedAt = DateTime.UtcNow
        });
        _auditService.AddEvent(new AuditEvent(task.Module,
            input.TargetStatus == WorkTaskStatuses.Approved ? "task_approved" :
            input.TargetStatus == WorkTaskStatuses.NeedRevision ? "task_revision_requested" : "task_status_changed",
            $"Công việc '{task.Title}': {WorkTaskStatuses.GetLabel(previousStatus)} → {WorkTaskStatuses.GetLabel(input.TargetStatus)}" +
            (string.IsNullOrWhiteSpace(comment) ? "." : $". Ghi chú: {comment}"),
            actorUserId, "WorkTask", task.Id.ToString()));
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return WorkTaskOperationResult.Failure("Công việc vừa được người khác cập nhật. Hãy tải lại trang rồi thử lại.");
        }
        return WorkTaskOperationResult.Success("Đã cập nhật trạng thái công việc.");
    }

    private async Task<int?> ResolveReviewerIdAsync(int actorUserId, string actorRole, string module, CancellationToken cancellationToken)
    {
        if (actorRole == AppRoles.Director) return actorUserId;
        if ((actorRole == AppRoles.BookingManager && module == WorkTaskModules.Booking) ||
            (actorRole == AppRoles.IdeaManager && module == WorkTaskModules.Ideas)) return actorUserId;
        return await _context.Users.AsNoTracking()
            .Where(user => user.Role == AppRoles.Director && user.Status == AccountStatuses.Active)
            .OrderBy(user => user.Id).Select(user => (int?)user.Id).FirstOrDefaultAsync(cancellationToken);
    }

    private static IReadOnlyList<WorkTaskTransitionViewModel> GetAllowedTransitions(WorkTask task, int actorUserId, string actorRole)
    {
        var result = new List<WorkTaskTransitionViewModel>();
        var isAssignee = task.AssignedEmployee.UserId == actorUserId;
        var canApprove = actorRole == AppRoles.Director ||
            (actorRole == AppRoles.BookingManager && task.Module == WorkTaskModules.Booking) ||
            (actorRole == AppRoles.IdeaManager && task.Module == WorkTaskModules.Ideas);

        if (isAssignee && task.Status == WorkTaskStatuses.Todo)
            result.Add(new(WorkTaskStatuses.InProgress, "Bắt đầu", false));
        if (isAssignee && task.Status == WorkTaskStatuses.InProgress)
            result.Add(new(WorkTaskStatuses.Review, "Gửi duyệt", false));
        if (isAssignee && task.Status == WorkTaskStatuses.NeedRevision)
            result.Add(new(WorkTaskStatuses.InProgress, "Chỉnh sửa lại", false));
        if (isAssignee && task.Status == WorkTaskStatuses.Approved)
            result.Add(new(WorkTaskStatuses.Done, "Hoàn thành", false));
        if (canApprove && task.Status == WorkTaskStatuses.Review)
        {
            result.Add(new(WorkTaskStatuses.Approved, "Duyệt", false));
            result.Add(new(WorkTaskStatuses.NeedRevision, "Yêu cầu sửa", true));
        }
        if (canApprove && task.Status == WorkTaskStatuses.Approved)
            result.Add(new(WorkTaskStatuses.Done, "Đóng công việc", false));
        return result;
    }

    private static bool CanCreate(string role) => role is AppRoles.Director or AppRoles.HumanResourcesManager or AppRoles.BookingManager or AppRoles.IdeaManager;
    private static bool IsManagerRole(string role) => role is AppRoles.HumanResourcesManager or AppRoles.BookingManager or AppRoles.IdeaManager;
    private static List<string> GetAllowedModules(string role) => role switch
    {
        AppRoles.Director => [WorkTaskModules.HumanResources, WorkTaskModules.Booking, WorkTaskModules.Ideas],
        AppRoles.HumanResourcesManager or AppRoles.HumanResourcesStaff => [WorkTaskModules.HumanResources],
        AppRoles.BookingManager or AppRoles.BookingStaff => [WorkTaskModules.Booking],
        AppRoles.IdeaManager or AppRoles.IdeaStaff => [WorkTaskModules.Ideas],
        _ => []
    };
    private static string ForceModuleForRole(string role, string requested) => role switch
    {
        AppRoles.HumanResourcesManager => WorkTaskModules.HumanResources,
        AppRoles.BookingManager => WorkTaskModules.Booking,
        AppRoles.IdeaManager => WorkTaskModules.Ideas,
        _ => requested
    };
}
