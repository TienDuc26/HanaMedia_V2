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
    private readonly ApplicationDbContext _context;
    private readonly ISystemAuditService _auditService;

    public CampaignController(ApplicationDbContext context, ISystemAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet("Campaigns")]
    public async Task<IActionResult> Index(string? search, string? status, CancellationToken cancellationToken)
    {
        var query = _context.Campaigns
            .Include(c => c.ManagerEmployee)
            .AsQueryable();

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

        return View(campaigns);
    }

    [HttpGet("Campaigns/Details/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.ManagerEmployee)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

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

        ViewBag.RelatedTasks = relatedTasks;

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

            TempData["SuccessMessage"] = "Tạo chiến dịch thành công.";
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
            campaign.Name = input.Name;
            campaign.Client = input.Client;
            campaign.Description = input.Description;
            campaign.Budget = input.Budget;
            campaign.ManagerEmployeeId = input.ManagerEmployeeId;
            campaign.Status = input.Status;
            campaign.Notes = input.Notes;
            campaign.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

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

        campaign.Status = "cancelled";
        campaign.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);

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
            .Select(c => new { c.Id, c.Name, c.Client, c.Status })
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (campaign == null)
        {
            return NotFound();
        }

        return Json(campaign);
    }

    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
            CultureInfo.InvariantCulture, out userId);
    }
}
