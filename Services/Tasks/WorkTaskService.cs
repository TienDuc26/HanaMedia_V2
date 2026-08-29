using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using HanaMedia.ViewModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
        int? employeeId = null, bool openCreate = default, string? status = null,
        int? reviewerId = null, bool? overdue = null, CancellationToken cancellationToken = default)
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

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(task => task.Status == status);
        }

        if (reviewerId.HasValue)
        {
            query = query.Where(task => task.ReviewerUserId == reviewerId.Value);
        }

        if (overdue.HasValue)
        {
            var today = DateTime.Today;
            if (overdue.Value)
            {
                query = query.Where(task => task.Deadline.Date < today && task.Status != WorkTaskStatuses.Done);
            }
            else
            {
                query = query.Where(task => task.Deadline.Date >= today || task.Status == WorkTaskStatuses.Done);
            }
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
            var departments = allowedModules.Select(WorkTaskModules.GetDepartment).ToArray();
            employees = await _context.Employees.AsNoTracking()
                .Where(employee =>
                    employee.UserId.HasValue &&
                    departments.Contains(employee.Department) &&
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

        var reviewers = await GetAvailableReviewersAsync(cancellationToken);

        return new WorkTaskPageViewModel
        {
            Module = selectedModule,
            ModuleLabel = WorkTaskModules.GetLabel(selectedModule),
            CanCreate = CanCreate(actorRole),
            Search = normalizedSearch,
            Page = page,
            TotalPages = totalPages,
            SelectedEmployeeId = selectedEmployeeId,
            OpenCreateModal = openCreate && selectedEmployeeId.HasValue,
            ActiveEmployeesWithoutAccount = activeEmployeesWithoutAccount,
            Employees = employees,
            AvailableModules = allowedModules.Select(item => new WorkTaskModuleOptionViewModel(item, WorkTaskModules.GetLabel(item))).ToList(),
            SelectedStatus = status,
            SelectedReviewerId = reviewerId,
            SelectedOverdue = overdue,
            Reviewers = reviewers,
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
                RelatedType = task.RelatedType,
                RelatedId = task.RelatedId,
                RelatedLabel = task.RelatedType is null
                    ? null
                    : $"{WorkTaskRelatedTypes.GetLabel(task.RelatedType)} #{task.RelatedId}",
                AllowedTransitions = GetAllowedTransitions(task, actorUserId, actorRole)
            }).ToList()
        };
    }

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

        var relatedType = string.IsNullOrWhiteSpace(input.RelatedType) ? null : input.RelatedType.Trim();
        if (!WorkTaskRelatedTypes.IsValid(relatedType))
            return WorkTaskOperationResult.Failure("Loại liên kết không hợp lệ.");
        if (relatedType is not null && relatedType != WorkTaskRelatedTypes.None && (!input.RelatedId.HasValue || input.RelatedId <= 0))
            return WorkTaskOperationResult.Failure("Cần chọn đối tượng liên kết khi đã chọn loại liên kết.");

        var department = WorkTaskModules.GetDepartment(module);
        var employee = await _context.Employees
            .FirstOrDefaultAsync(item => item.Id == input.AssignedEmployeeId, cancellationToken);
        if (employee is null || employee.Department != department || !employee.UserId.HasValue ||
            !ActiveEmployeeStatuses.Contains(employee.Status ?? string.Empty))
        {
            return WorkTaskOperationResult.Failure("Nhân sự được giao phải là nhân sự thật, đang hoạt động, có tài khoản và thuộc đúng phòng ban.");
        }

        // Check selected reviewer and ensure they have the proper permission
        int reviewerId;
        if (input.ReviewerUserId > 0)
        {
            var reviewer = await _context.Users.FirstOrDefaultAsync(u => u.Id == input.ReviewerUserId, cancellationToken);
            if (reviewer is null || !new[] { AppRoles.Director, AppRoles.BookingManager, AppRoles.IdeaManager }.Contains(reviewer.Role))
            {
                return WorkTaskOperationResult.Failure("Người duyệt được chọn không hợp lệ hoặc không có quyền duyệt công việc.");
            }
            reviewerId = reviewer.Id;
        }
        else
        {
            var resolved = await ResolveReviewerIdAsync(actorUserId, actorRole, module, cancellationToken);
            if (!resolved.HasValue)
            {
                return WorkTaskOperationResult.Failure("Chưa có tài khoản đủ quyền duyệt công việc cho phân hệ này.");
            }
            reviewerId = resolved.Value;
        }

        var now = DateTime.UtcNow;
        var task = new WorkTask
        {
            Title = title,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            Module = module,
            AssignedEmployeeId = employee.Id,
            CreatedByUserId = actorUserId,
            ReviewerUserId = reviewerId,
            Deadline = input.Deadline,
            Status = WorkTaskStatuses.Todo,
            RelatedType = string.IsNullOrWhiteSpace(relatedType) ? null : relatedType,
            RelatedId = relatedType is not null && relatedType != WorkTaskRelatedTypes.None ? input.RelatedId : null,
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

    public async Task<WorkTaskWorkspaceViewModel> GetWorkspaceDetailsAsync(
        int taskId, int actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        var task = await _context.WorkTasks
            .Include(t => t.AssignedEmployee)
            .Include(t => t.CreatedByUser)
            .Include(t => t.ReviewerUser)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task is null)
        {
            throw new KeyNotFoundException("Không tìm thấy công việc.");
        }

        // Validate access
        var allowedModules = GetAllowedModules(actorRole);
        if (!allowedModules.Contains(task.Module))
        {
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập công việc thuộc phòng ban này.");
        }

        // Managers can view everything. Employees can only view tasks assigned to them.
        var isAssignee = task.AssignedEmployee.UserId == actorUserId;
        var isManagerOrDirector = IsManagerRole(actorRole) || actorRole == AppRoles.Director;
        if (!isAssignee && !isManagerOrDirector)
        {
            throw new UnauthorizedAccessException("Bạn không được phân công thực hiện công việc này.");
        }

        var submissionsRaw = await _context.WorkTaskSubmissions
            .Include(s => s.SubmittedByUser)
            .Include(s => s.ReviewedByUser)
            .Where(s => s.WorkTaskId == taskId)
            .OrderByDescending(s => s.Version)
            .ToListAsync(cancellationToken);

        var submissions = submissionsRaw.Select(s => new WorkTaskSubmissionViewModel
        {
            Id = s.Id,
            Version = s.Version,
            SubmittedByName = s.SubmittedByUser.Username,
            SubmittedAt = s.SubmittedAt,
            Result = s.Result,
            Notes = s.Notes,
            Feedback = s.Feedback,
            ReviewedByName = s.ReviewedByUser != null ? s.ReviewedByUser.Username : null,
            ReviewedAt = s.ReviewedAt,
            Status = s.Status,
            StatusLabel = s.Status switch
            {
                "approved" => "Đã duyệt",
                "need_revision" => "Yêu cầu chỉnh sửa",
                _ => "Chờ duyệt"
            },
            Files = string.IsNullOrEmpty(s.FilesJson)
                ? new List<WorkspaceFileViewModel>()
                : JsonSerializer.Deserialize<List<WorkspaceFileViewModel>>(s.FilesJson, (JsonSerializerOptions?)null) ?? new List<WorkspaceFileViewModel>()
        }).ToList();

        WorkspaceEmployeeContext? empContext = null;
        WorkspaceBookingContext? bookingContext = null;
        WorkspaceIdeaContext? ideaContext = null;

        if (task.RelatedType == WorkTaskRelatedTypes.Employee && task.RelatedId.HasValue)
        {
            var emp = await _context.Employees
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.Id == task.RelatedId.Value, cancellationToken);
            if (emp != null)
            {
                empContext = new WorkspaceEmployeeContext
                {
                    Id = emp.Id,
                    FullName = emp.FullName,
                    AvatarUrl = emp.AvatarUrl,
                    Email = emp.Email,
                    Phone = emp.Phone,
                    Address = emp.Address,
                    Dob = emp.Dob.ToString("dd/MM/yyyy"),
                    JoinedDate = emp.JoinedDate.ToString("dd/MM/yyyy"),
                    Department = emp.Department,
                    Position = emp.Position,
                    ContractType = emp.ContractType switch
                    {
                        "thu_viec" => "Thử việc",
                        "chinh_thuc_1_nam" => "Chính thức 1 năm",
                        "vo_thoi_han" => "Vô thời hạn",
                        _ => emp.ContractType
                    },
                    Status = emp.Status switch
                    {
                        "dang_lam_viec" => "Đang làm việc",
                        "thu_viec" => "Thử việc",
                        "cho_duyet_nghi" => "Chờ duyệt nghỉ",
                        "ngung_hoat_dong" => "Ngừng hoạt động",
                        _ => emp.Status ?? ""
                    },
                    ManagerName = emp.Manager?.FullName
                };
            }
        }
        else if (task.RelatedType == WorkTaskRelatedTypes.Booking && task.RelatedId.HasValue)
        {
            var booking = await _context.Bookings
                .Include(b => b.Kol)
                .Include(b => b.PrimaryManager)
                .FirstOrDefaultAsync(b => b.Id == task.RelatedId.Value, cancellationToken);
            if (booking != null)
            {
                bookingContext = new WorkspaceBookingContext
                {
                    Id = booking.Id,
                    ClientName = booking.ClientName,
                    CampaignName = booking.CampaignName,
                    KolName = booking.Kol?.Name,
                    JobDescription = booking.JobDescription,
                    Deadline = booking.Deadline.ToString("dd/MM/yyyy"),
                    PostingDate = booking.PostingDate?.ToString("dd/MM/yyyy"),
                    BookingPrice = booking.BookingPrice,
                    ActualCost = booking.ActualCost,
                    PrimaryManagerName = booking.PrimaryManager?.FullName,
                    Status = booking.Status switch
                    {
                        "dang_cho" => "Đang chờ",
                        "thuong_luong" => "Thương lượng",
                        "da_chot" => "Đã chốt",
                        "dang_trien_khai" => "Đang triển khai",
                        "hoan_thanh" => "Hoàn thành",
                        "huy" => "Đã hủy",
                        _ => booking.Status ?? ""
                    },
                    ContractFileUrl = booking.ContractFileUrl,
                    QuotationFileUrl = booking.QuotationFileUrl,
                    PostLink = booking.PostLink,
                    Notes = booking.Notes
                };
            }
        }
        else if (task.RelatedType == WorkTaskRelatedTypes.Idea && task.RelatedId.HasValue)
        {
            var idea = await _context.Ideas
                .Include(i => i.CreatorEmployee)
                .Include(i => i.PrimaryStaff)
                .Include(i => i.ReviewerEmployee)
                .FirstOrDefaultAsync(i => i.Id == task.RelatedId.Value, cancellationToken);
            if (idea != null)
            {
                ideaContext = new WorkspaceIdeaContext
                {
                    Id = idea.Id,
                    Title = idea.Title,
                    ClientName = idea.ClientName,
                    CampaignName = idea.CampaignName,
                    Industry = idea.Industry,
                    Category = idea.Category switch
                    {
                        "trend" => "Xu hướng (Trend)",
                        "viral" => "Lan truyền (Viral)",
                        "da_trien_khai" => "Đã triển khai",
                        "chua_su_dung" => "Chưa sử dụng",
                        _ => idea.Category
                    },
                    Insight = idea.Insight,
                    Concept = idea.Concept,
                    ContentDetails = idea.ContentDetails,
                    ReferenceLink = idea.ReferenceLink,
                    MoodboardDesc = idea.MoodboardDesc,
                    ScriptText = idea.ScriptText,
                    Deadline = idea.Deadline.ToString("dd/MM/yyyy"),
                    CreatorName = idea.CreatorEmployee?.FullName,
                    PrimaryStaffName = idea.PrimaryStaff?.FullName,
                    ReviewerName = idea.ReviewerEmployee?.FullName,
                    Status = idea.Status switch
                    {
                        "y_tuong" => "Ý tưởng",
                        "review" => "Chờ duyệt",
                        "need_revision" => "Cần sửa",
                        "approved" => "Đã duyệt",
                        "done" => "Hoàn thành",
                        _ => idea.Status ?? ""
                    },
                    FeedbackComment = idea.FeedbackComment
                };
            }
        }

        var reviewers = await GetAvailableReviewersAsync(cancellationToken);
        var canApprove = actorRole == AppRoles.Director ||
            (actorRole == AppRoles.BookingManager && task.Module == WorkTaskModules.Booking) ||
            (actorRole == AppRoles.IdeaManager && task.Module == WorkTaskModules.Ideas);

        var canReview = canApprove && task.Status == WorkTaskStatuses.Review;

        return new WorkTaskWorkspaceViewModel
        {
            Task = new WorkTaskListItemViewModel
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
                RelatedType = task.RelatedType,
                RelatedId = task.RelatedId,
                RelatedLabel = task.RelatedType is null
                    ? null
                    : $"{WorkTaskRelatedTypes.GetLabel(task.RelatedType)} #{task.RelatedId}",
                AllowedTransitions = GetAllowedTransitions(task, actorUserId, actorRole)
            },
            DraftData = task.DraftData,
            Submissions = submissions,
            EmployeeContext = empContext,
            BookingContext = bookingContext,
            IdeaContext = ideaContext,
            AvailableReviewers = reviewers,
            CanReview = canReview
        };
    }

    public async Task<WorkTaskOperationResult> SaveDraftAsync(
        int taskId, string draftDataJson, int actorUserId, CancellationToken cancellationToken = default)
    {
        var task = await _context.WorkTasks
            .Include(t => t.AssignedEmployee)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            return WorkTaskOperationResult.Failure("Không tìm thấy công việc.");
        }

        if (task.AssignedEmployee.UserId != actorUserId)
        {
            return WorkTaskOperationResult.Failure("Bạn không phải nhân sự được giao thực hiện công việc này.");
        }

        if (task.Status != WorkTaskStatuses.Todo && task.Status != WorkTaskStatuses.InProgress && task.Status != WorkTaskStatuses.NeedRevision)
        {
            return WorkTaskOperationResult.Failure("Trạng thái công việc không cho phép lưu bản nháp.");
        }

        var oldStatus = task.Status;
        if (task.Status == WorkTaskStatuses.Todo)
        {
            task.Status = WorkTaskStatuses.InProgress;
        }

        task.DraftData = draftDataJson;
        task.UpdatedAt = DateTime.UtcNow;

        task.History.Add(new WorkTaskHistory
        {
            ActorUserId = actorUserId,
            FromStatus = oldStatus,
            ToStatus = task.Status,
            Comment = "Lưu bản nháp công việc",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return WorkTaskOperationResult.Success("Đã lưu bản nháp thành công.");
    }

    public async Task<WorkTaskOperationResult> SubmitReviewAsync(
        int taskId, string result, string notes, int actorUserId, CancellationToken cancellationToken = default)
    {
        var task = await _context.WorkTasks
            .Include(t => t.AssignedEmployee)
            .Include(t => t.Submissions)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task is null)
        {
            return WorkTaskOperationResult.Failure("Không tìm thấy công việc.");
        }

        if (task.AssignedEmployee.UserId != actorUserId)
        {
            return WorkTaskOperationResult.Failure("Bạn không phải nhân sự được giao thực hiện công việc này.");
        }

        if (task.Status != WorkTaskStatuses.Todo && task.Status != WorkTaskStatuses.InProgress && task.Status != WorkTaskStatuses.NeedRevision)
        {
            return WorkTaskOperationResult.Failure("Không thể gửi duyệt công việc ở trạng thái này.");
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            return WorkTaskOperationResult.Failure("Vui lòng điền nội dung/kết quả thực tế trước khi gửi duyệt.");
        }

        // Get files list from draft data
        string? filesJson = null;
        if (!string.IsNullOrEmpty(task.DraftData))
        {
            try
            {
                using var doc = JsonDocument.Parse(task.DraftData);
                if (doc.RootElement.TryGetProperty("files", out var filesProp))
                {
                    filesJson = filesProp.GetRawText();
                }
            }
            catch
            {
                // ignore invalid draft JSON
            }
        }

        var version = task.Submissions.Count > 0
            ? task.Submissions.Max(s => s.Version) + 1
            : 1;

        var submission = new WorkTaskSubmission
        {
            WorkTaskId = taskId,
            Version = version,
            SubmittedByUserId = actorUserId,
            SubmittedAt = DateTime.UtcNow,
            Result = result.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            FilesJson = filesJson,
            Status = "review"
        };

        _context.WorkTaskSubmissions.Add(submission);

        var oldStatus = task.Status;
        task.Status = WorkTaskStatuses.Review;
        task.UpdatedAt = DateTime.UtcNow;

        task.History.Add(new WorkTaskHistory
        {
            ActorUserId = actorUserId,
            FromStatus = oldStatus,
            ToStatus = WorkTaskStatuses.Review,
            Comment = $"Gửi yêu cầu duyệt kết quả công việc (Version {version})",
            CreatedAt = DateTime.UtcNow
        });

        _auditService.AddEvent(new AuditEvent(task.Module, "task_submitted",
            $"Nhân viên gửi duyệt công việc '{task.Title}' (Version {version}).",
            actorUserId, "WorkTask", task.Id.ToString()));

        await _context.SaveChangesAsync(cancellationToken);
        return WorkTaskOperationResult.Success($"Đã gửi duyệt kết quả công việc thành công (Version {version}).");
    }

    public async Task<WorkTaskOperationResult> ReviewTaskAsync(
        int taskId, string targetStatus, string? feedback, int reviewerUserId, string reviewerRole, CancellationToken cancellationToken = default)
    {
        var task = await _context.WorkTasks
            .Include(t => t.AssignedEmployee)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task is null)
        {
            return WorkTaskOperationResult.Failure("Không tìm thấy công việc.");
        }

        var isDirector = reviewerRole == AppRoles.Director;
        var isBookingManager = reviewerRole == AppRoles.BookingManager && task.Module == WorkTaskModules.Booking;
        var isIdeaManager = reviewerRole == AppRoles.IdeaManager && task.Module == WorkTaskModules.Ideas;

        if (!isDirector && !isBookingManager && !isIdeaManager)
        {
            return WorkTaskOperationResult.Failure("Bạn không có quyền duyệt công việc của phân hệ này.");
        }

        if (task.Status != WorkTaskStatuses.Review)
        {
            return WorkTaskOperationResult.Failure("Công việc không ở trạng thái chờ duyệt.");
        }

        if (targetStatus == WorkTaskStatuses.NeedRevision && string.IsNullOrWhiteSpace(feedback))
        {
            return WorkTaskOperationResult.Failure("Vui lòng nhập nội dung yêu cầu chỉnh sửa.");
        }

        var latestSubmission = await _context.WorkTaskSubmissions
            .Where(s => s.WorkTaskId == taskId)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestSubmission != null)
        {
            latestSubmission.Status = targetStatus == WorkTaskStatuses.Approved ? "approved" : "need_revision";
            latestSubmission.Feedback = feedback?.Trim();
            latestSubmission.ReviewedByUserId = reviewerUserId;
            latestSubmission.ReviewedAt = DateTime.UtcNow;
        }

        var oldStatus = task.Status;
        task.Status = targetStatus;
        task.UpdatedAt = DateTime.UtcNow;

        if (targetStatus == WorkTaskStatuses.Approved)
        {
            // Auto transition to Done
            task.Status = WorkTaskStatuses.Done;
            task.CompletedAt = DateTime.UtcNow;
        }

        task.History.Add(new WorkTaskHistory
        {
            ActorUserId = reviewerUserId,
            FromStatus = oldStatus,
            ToStatus = task.Status,
            Comment = targetStatus == WorkTaskStatuses.Approved
                ? "Duyệt công việc và hoàn thành"
                : $"Yêu cầu chỉnh sửa: {feedback}",
            CreatedAt = DateTime.UtcNow
        });

        _auditService.AddEvent(new AuditEvent(task.Module,
            targetStatus == WorkTaskStatuses.Approved ? "task_approved" : "task_revision_requested",
            targetStatus == WorkTaskStatuses.Approved
                ? $"Duyệt công việc '{task.Title}'."
                : $"Yêu cầu chỉnh sửa công việc '{task.Title}'. Phản hồi: {feedback}",
            reviewerUserId, "WorkTask", task.Id.ToString()));

        await _context.SaveChangesAsync(cancellationToken);
        return WorkTaskOperationResult.Success(targetStatus == WorkTaskStatuses.Approved
            ? "Đã duyệt và hoàn thành công việc thành công."
            : "Đã từ chối và yêu cầu chỉnh sửa công việc.");
    }

    public async Task<List<ReviewerOptionViewModel>> GetAvailableReviewersAsync(CancellationToken cancellationToken = default)
    {
        var roles = new[] { AppRoles.Director, AppRoles.BookingManager, AppRoles.IdeaManager };
        var usersRaw = await _context.Users.AsNoTracking()
            .Include(u => u.Employee)
            .Where(u => roles.Contains(u.Role) && u.Status == AccountStatuses.Active)
            .ToListAsync(cancellationToken);

        return usersRaw.Select(u => new ReviewerOptionViewModel
        {
            Id = u.Id,
            Username = u.Username,
            FullName = u.Employee != null ? u.Employee.FullName : u.Username,
            Role = u.Role switch
            {
                AppRoles.Director => "Giám đốc",
                AppRoles.BookingManager => "Quản lý Booking",
                AppRoles.IdeaManager => "Quản lý Ý tưởng",
                _ => u.Role
            }
        }).ToList();
    }

    private async Task<int?> ResolveReviewerIdAsync(int actorUserId, string actorRole, string module, CancellationToken cancellationToken)
    {
        if (actorRole == AppRoles.Director) return actorUserId;
        if ((actorRole == AppRoles.HumanResourcesManager && module == WorkTaskModules.HumanResources) ||
            (actorRole == AppRoles.BookingManager && module == WorkTaskModules.Booking) ||
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
