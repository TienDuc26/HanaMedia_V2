using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Controllers;

[Authorize(Roles = AppRoles.Director + "," + AppRoles.BookingManager + "," + AppRoles.BookingStaff + "," + AppRoles.IdeaManager + "," + AppRoles.IdeaStaff)]
public sealed class CampaignController : Controller
{
    private bool IsIdea => User.IsInRole(AppRoles.IdeaManager) || User.IsInRole(AppRoles.IdeaStaff);
    private readonly ApplicationDbContext _context;
    private readonly ISystemAuditService _auditService;

    public CampaignController(ApplicationDbContext context, ISystemAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet("Campaigns")]
    public async Task<IActionResult> Index(string? search, string? status, bool pendingAcceptance, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(status) && !CampaignStatuses.IsSelectable(status))
            return RedirectToAction(nameof(Index), new { search });

        var query = _context.Campaigns
            .Include(c => c.ManagerEmployee)
            .AsQueryable();
        if (IsIdea) query = query.Where(c => c.ConfirmedAt != null);
        if (pendingAcceptance) query = query.Where(c => c.Status != CampaignStatuses.Accepted && c.Status != "cancelled");

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search) || c.Client.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }
        else
        {
            query = query.Where(c => c.Status != "cancelled");
        }

        var campaigns = await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        ViewBag.Employees = await _context.Employees
            .Where(e => e.Status == "dang_lam_viec" || e.Status == "thu_viec")
            .OrderBy(e => e.FullName)
            .ToListAsync(cancellationToken);

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.PendingAcceptance = pendingAcceptance;

        return View(campaigns);
    }

    [HttpGet("Campaigns/Details/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.ManagerEmployee)
            .FirstOrDefaultAsync(c => c.Id == id && (!IsIdea || c.ConfirmedAt != null), cancellationToken);

        if (campaign == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy chiến dịch.";
            return RedirectToAction(nameof(Index));
        }

        var relatedTasks = await _context.WorkTasks
            .Include(t => t.AssignedEmployee)
            .Where(t => t.CampaignId == id)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var relatedBookings = await _context.Bookings
            .Include(b => b.PrimaryManager)
            .Include(b => b.BookingKols)
            .Where(b => b.CampaignId == id)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        var relatedIdeas = await _context.Ideas
            .Include(i => i.PrimaryStaff)
            .Where(i => i.CampaignId == id)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        ViewBag.RelatedTasks = relatedTasks;
        ViewBag.RelatedBookings = relatedBookings;
        ViewBag.RelatedIdeas = relatedIdeas;

        return View(campaign);
    }

    [HttpPost("Campaigns/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Campaign model, string startDateStr, string endDateStr, CancellationToken cancellationToken)
    {
        if (User.FindFirstValue(ClaimTypes.Role) != AppRoles.BookingManager)
        {
            return Forbid();
        }

        // Creation cannot bypass director confirmation, even with a forged form.
        model.Status = CampaignStatuses.AwaitingSignature;
        ModelState.Remove(nameof(Campaign.Status));

        if (DateOnly.TryParse(startDateStr, out var startDate))
        {
            model.StartDate = startDate;
        }
        else
        {
            ModelState.AddModelError("StartDate", "Ngày bắt đầu không hợp lệ.");
        }

        if (DateOnly.TryParse(endDateStr, out var endDate))
        {
            model.EndDate = endDate;
        }
        else
        {
            ModelState.AddModelError("EndDate", "Ngày kết thúc không hợp lệ.");
        }

        if (model.EndDate < model.StartDate)
        {
            ModelState.AddModelError("EndDate", "Ngày kết thúc phải sau hoặc trùng ngày bắt đầu.");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError("Name", "Tên chiến dịch bắt buộc nhập.");
        }

        if (string.IsNullOrWhiteSpace(model.Client))
        {
            ModelState.AddModelError("Client", "Client bắt buộc nhập.");
        }

        ModelState.Remove(nameof(model.ManagerEmployee));
        ModelState.Remove(nameof(model.WorkTasks));

        if (ModelState.IsValid)
        {
            model.ConfirmedAt = null; model.ConfirmedByUserId = null;
            model.CompletedAt = null; model.CompletedByUserId = null;
            model.AcceptedAt = null; model.AcceptedByUserId = null;
            model.CreatedAt = DateTime.Now;
            model.UpdatedAt = DateTime.Now;
            _context.Campaigns.Add(model);
            await _context.SaveChangesAsync(cancellationToken);

            TryGetUserId(out var userId);
            await _auditService.WriteAsync(new AuditEvent(
                AuditModules.Booking,
                AuditActions.Created,
                $"Đã tạo chiến dịch: {model.Name} (Client: {model.Client})",
                userId,
                "Campaign",
                model.Id.ToString()
            ), cancellationToken);

            TempData["SuccessMessage"] = "Đã tạo chiến dịch ở trạng thái Đang chờ ký. Giám đốc chốt để bắt đầu chạy.";
            return RedirectToAction(nameof(Index));
        }

        TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Campaigns/Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Campaign input, string startDateStr, string endDateStr, CancellationToken cancellationToken)
    {
        if (User.FindFirstValue(ClaimTypes.Role) != AppRoles.BookingManager)
        {
            return Forbid();
        }

        var campaign = await _context.Campaigns.FindAsync(new object[] { id }, cancellationToken);
        if (campaign == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy chiến dịch.";
            return RedirectToAction(nameof(Index));
        }

        if (campaign.Status == CampaignStatuses.Accepted)
            return BadRequest("Chiến dịch đã nghiệm thu không thể chỉnh sửa.");

        // The workflow owns status; editing content cannot start, reopen or unconfirm it.
        ModelState.Remove(nameof(Campaign.Status));

        if (DateOnly.TryParse(startDateStr, out var startDate))
        {
            campaign.StartDate = startDate;
        }
        else
        {
            ModelState.AddModelError("StartDate", "Ngày bắt đầu không hợp lệ.");
        }

        if (DateOnly.TryParse(endDateStr, out var endDate))
        {
            campaign.EndDate = endDate;
        }
        else
        {
            ModelState.AddModelError("EndDate", "Ngày kết thúc không hợp lệ.");
        }

        if (campaign.EndDate < campaign.StartDate)
        {
            campaign.EndDate = campaign.StartDate; // Auto correct or fail: Let's fail
            ModelState.AddModelError("EndDate", "Ngày kết thúc phải sau hoặc trùng ngày bắt đầu.");
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            ModelState.AddModelError("Name", "Tên chiến dịch bắt buộc nhập.");
        }

        if (string.IsNullOrWhiteSpace(input.Client))
        {
            ModelState.AddModelError("Client", "Client bắt buộc nhập.");
        }

        ModelState.Remove("ManagerEmployee");
        ModelState.Remove("WorkTasks");

        if (ModelState.IsValid)
        {
            if (campaign.ConfirmedAt != null && (campaign.Name != input.Name || campaign.Client != input.Client || campaign.Budget != input.Budget))
                return BadRequest("Chiến dịch đã chốt: không đổi tên, client hoặc ngân sách.");
            campaign.Name = input.Name;
            campaign.Client = input.Client;
            campaign.Description = input.Description;
            campaign.Budget = input.Budget;
            campaign.ManagerEmployeeId = input.ManagerEmployeeId;
            campaign.Notes = input.Notes;
            campaign.UpdatedAt = DateTime.Now;

            try { await _context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                TempData["ErrorMessage"] = "Chiến dịch vừa được Giám đốc chốt hoặc cập nhật. Tải lại trang rồi thử lại.";
                return RedirectToAction(nameof(Index));
            }

            TryGetUserId(out var userId);
            await _auditService.WriteAsync(new AuditEvent(
                AuditModules.Booking,
                AuditActions.Updated,
                $"Đã cập nhật chiến dịch: {campaign.Name}",
                userId,
                "Campaign",
                campaign.Id.ToString()
            ), cancellationToken);

            TempData["SuccessMessage"] = "Cập nhật chiến dịch thành công.";
            return RedirectToAction(nameof(Index));
        }

        TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Campaigns/Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (User.FindFirstValue(ClaimTypes.Role) != AppRoles.BookingManager)
        {
            return Forbid();
        }

        var campaign = await _context.Campaigns.FindAsync(new object[] { id }, cancellationToken);
        if (campaign == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy chiến dịch.";
            return RedirectToAction(nameof(Index));
        }

        if (campaign.Status is CampaignStatuses.Completed or CampaignStatuses.Accepted)
            return BadRequest("Chiến dịch đã hoàn thành hoặc nghiệm thu không thể hủy.");

        campaign.Status = "cancelled";
        campaign.UpdatedAt = DateTime.Now;
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict("Chiến dịch vừa đổi trạng thái. Tải lại trang."); }

        TryGetUserId(out var userId);
        await _auditService.WriteAsync(new AuditEvent(
            AuditModules.Booking,
            AuditActions.Deleted,
            $"Đã ngừng hoạt động (hủy) chiến dịch: {campaign.Name}",
            userId,
            "Campaign",
            campaign.Id.ToString()
        ), cancellationToken);

        TempData["SuccessMessage"] = "Đã ngừng hoạt động chiến dịch thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("api/Campaigns/{id:int}")]
    public async Task<IActionResult> GetApi(int id, CancellationToken cancellationToken)
    {
        var campaign = await _context.Campaigns
            .Where(c => !IsIdea || c.ConfirmedAt != null)
            .Select(c => new { c.Id, c.Name, c.Client, c.Status })
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (campaign == null)
        {
            return NotFound();
        }

        return Json(campaign);
    }

    [HttpPost("Campaigns/Confirm/{id:int}"), ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Director)]
    public async Task<IActionResult> Confirm(int id, CancellationToken ct)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign == null) return NotFound();
        if (campaign.ConfirmedAt != null) return RedirectToAction(nameof(Index));
        if (campaign.Status != CampaignStatuses.AwaitingSignature) return BadRequest("Chỉ chốt chiến dịch Đang chờ ký.");
        campaign.ConfirmedAt = DateTime.Now;
        campaign.Status = CampaignStatuses.Running;
        campaign.UpdatedAt = DateTime.Now;
        TryGetUserId(out var userId); campaign.ConfirmedByUserId = userId;
        _auditService.AddEvent(new AuditEvent(AuditModules.Booking, "campaign_confirmed",
            $"Giám đốc chốt chiến dịch #{id}: {campaign.Name}; chuyển sang Đang chạy.", userId, "Campaign", id.ToString()));
        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("Chiến dịch vừa được chốt. Tải lại trang."); }
        TempData["SuccessMessage"] = "Đã chốt chiến dịch và chuyển sang Đang chạy; có thể tạo Booking và ý tưởng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Campaigns/Complete/{id:int}"), ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.BookingManager)]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
    {
        var campaign = await _context.Campaigns.FindAsync(new object[] { id }, ct);
        if (campaign == null) return NotFound();
        if (campaign.Status is CampaignStatuses.Completed or CampaignStatuses.Accepted)
            return RedirectToAction(nameof(Details), new { id });
        if (campaign.Status != CampaignStatuses.Running || campaign.ConfirmedAt == null)
            return BadRequest("Chỉ đánh dấu hoàn thành chiến dịch Đang chạy đã được Giám đốc chốt.");
        TryGetUserId(out var userId);
        campaign.Status = CampaignStatuses.Completed;
        campaign.CompletedAt = DateTime.Now;
        campaign.CompletedByUserId = userId;
        campaign.UpdatedAt = campaign.CompletedAt;
        _auditService.AddEvent(new AuditEvent(AuditModules.Booking, "campaign_completed",
            $"Hoàn thành chiến dịch #{id}: {campaign.Name}. Chưa xác nhận nhận tiền/nghiệm thu.", userId, "Campaign", id.ToString()));
        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("Chiến dịch vừa đổi trạng thái. Tải lại trang."); }
        TempData["SuccessMessage"] = "Chiến dịch đã Hoàn thành. Sau khi nhận được tiền, QL Booking xác nhận Nghiệm thu.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Campaigns/Accept/{id:int}"), ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.BookingManager)]
    public async Task<IActionResult> Accept(int id, bool receivedPayment, CancellationToken ct)
    {
        if (!receivedPayment) return BadRequest("Cần xác nhận đã nhận được tiền trước khi nghiệm thu chiến dịch.");
        var campaign = await _context.Campaigns.FindAsync(new object[] { id }, ct);
        if (campaign == null) return NotFound();
        if (campaign.Status == CampaignStatuses.Accepted)
            return RedirectToAction(nameof(Details), new { id });
        if (campaign.Status != CampaignStatuses.Completed || campaign.ConfirmedAt == null)
            return BadRequest("Chỉ nghiệm thu chiến dịch đã Hoàn thành và được Giám đốc chốt.");
        TryGetUserId(out var userId);
        campaign.Status = CampaignStatuses.Accepted;
        campaign.AcceptedAt = DateTime.Now;
        campaign.AcceptedByUserId = userId;
        campaign.UpdatedAt = campaign.AcceptedAt;
        _auditService.AddEvent(new AuditEvent(AuditModules.Booking, "campaign_accepted",
            $"QL Booking xác nhận đã nhận được tiền và nghiệm thu chiến dịch #{id}: {campaign.Name}.", userId, "Campaign", id.ToString()));
        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("Chiến dịch vừa đổi trạng thái. Tải lại trang."); }
        TempData["SuccessMessage"] = "Đã nghiệm thu chiến dịch — xác nhận đã nhận được tiền.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
            CultureInfo.InvariantCulture, out userId);
    }
}
