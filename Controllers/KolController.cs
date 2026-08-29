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

[Authorize(Roles = AppRoles.Director + "," + AppRoles.BookingManager + "," + AppRoles.BookingStaff)]
public sealed class KolController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ISystemAuditService _auditService;

    public KolController(ApplicationDbContext context, ISystemAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet("Kols")]
    public async Task<IActionResult> Index(string? search, string? platform, string? status, CancellationToken cancellationToken)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        
        var query = _context.Kols
            .Include(k => k.ResponsibleStaff)
            .Where(k => k.IsActive) // Only active (soft-deleted are hidden)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(k => k.Name.Contains(search) || (k.Niche != null && k.Niche.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            query = query.Where(k => (k.Platform != null && k.Platform.Contains(platform)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(k => k.Status == status);
        }

        var kols = await query
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);

        // Fetch active employees for dropdown
        ViewBag.Employees = await _context.Employees
            .Where(e => e.Status == "dang_lam_viec" || e.Status == "thu_viec")
            .OrderBy(e => e.FullName)
            .ToListAsync(cancellationToken);

        ViewBag.Search = search;
        ViewBag.Platform = platform;
        ViewBag.Status = status;

        return View(kols);
    }

    [HttpGet("Kols/Details/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var kol = await _context.Kols
            .Include(k => k.ResponsibleStaff)
            .FirstOrDefaultAsync(k => k.Id == id && k.IsActive, cancellationToken);

        if (kol == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin KOL/KOC.";
            return RedirectToAction(nameof(Index));
        }

        return View(kol);
    }

    [HttpPost("Kols/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Kol model, string[] platforms, string[] niches, CancellationToken cancellationToken)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != AppRoles.BookingManager && role != AppRoles.BookingStaff)
        {
            return Forbid();
        }

        TryGetUserId(out var userId);
        var currentUser = await _context.Users
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        var currentEmployeeId = currentUser?.Employee?.Id;

        // Process platforms multi-select
        if (platforms != null && platforms.Length > 0)
        {
            model.Platform = string.Join(",", platforms.Select(p => p.Trim()));
        }
        else
        {
            ModelState.AddModelError("Platform", "Vui lòng chọn ít nhất một nền tảng.");
        }

        // Process niches multi-select or tags
        if (niches != null && niches.Length > 0)
        {
            model.Niche = string.Join(",", niches.Select(n => n.Trim()));
        }
        else
        {
            ModelState.AddModelError("Niche", "Vui lòng nhập/chọn ít nhất một chủ đề.");
        }

        // Rule for ResponsibleStaffId based on role
        if (role == AppRoles.BookingStaff)
        {
            model.ResponsibleStaffId = currentEmployeeId;
        }

        ModelState.Remove(nameof(model.ResponsibleStaff));
        ModelState.Remove(nameof(model.Bookings));
        ModelState.Remove(nameof(model.Platform));
        ModelState.Remove(nameof(model.Niche));

        if (ModelState.IsValid)
        {
            model.IsActive = true;
            model.CreatedAt = DateTime.Now;
            model.UpdatedAt = DateTime.Now;

            _context.Kols.Add(model);
            await _context.SaveChangesAsync(cancellationToken);

            await _auditService.WriteAsync(new AuditEvent(
                AuditModules.Booking,
                AuditActions.Created,
                $"Đã thêm KOL/KOC mới: {model.Name} (Nền tảng: {model.Platform})",
                userId,
                "Kol",
                model.Id.ToString()
            ), cancellationToken);

            TempData["SuccessMessage"] = "Đã thêm KOL/KOC thành công.";
            return RedirectToAction(nameof(Index));
        }

        TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Kols/Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Kol input, string[] platforms, string[] niches, CancellationToken cancellationToken)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != AppRoles.BookingManager && role != AppRoles.BookingStaff)
        {
            return Forbid();
        }

        var kol = await _context.Kols.FindAsync(new object[] { id }, cancellationToken);
        if (kol == null || !kol.IsActive)
        {
            TempData["ErrorMessage"] = "Không tìm thấy KOL/KOC.";
            return RedirectToAction(nameof(Index));
        }

        // Process platforms multi-select
        string updatedPlatform = "";
        if (platforms != null && platforms.Length > 0)
        {
            updatedPlatform = string.Join(",", platforms.Select(p => p.Trim()));
        }
        else
        {
            ModelState.AddModelError("Platform", "Vui lòng chọn ít nhất một nền tảng.");
        }

        // Process niches multi-select
        string updatedNiche = "";
        if (niches != null && niches.Length > 0)
        {
            updatedNiche = string.Join(",", niches.Select(n => n.Trim()));
        }
        else
        {
            ModelState.AddModelError("Niche", "Vui lòng nhập/chọn ít nhất một chủ đề.");
        }

        ModelState.Remove("ResponsibleStaff");
        ModelState.Remove("Bookings");
        ModelState.Remove("Platform");
        ModelState.Remove("Niche");

        if (ModelState.IsValid)
        {
            kol.Name = input.Name;
            kol.Platform = updatedPlatform;
            kol.ProfileLink = input.ProfileLink;
            kol.FollowersCount = input.FollowersCount;
            kol.EngagementRate = input.EngagementRate;
            kol.Niche = updatedNiche;
            kol.BookingPrice = input.BookingPrice;
            kol.Location = input.Location;
            kol.ContactInfo = input.ContactInfo;
            kol.RatingScore = input.RatingScore;
            kol.Status = input.Status;
            kol.UpdatedAt = DateTime.Now;

            // Only BookingManager (QL Booking) can change ResponsibleStaffId
            if (role == AppRoles.BookingManager)
            {
                kol.ResponsibleStaffId = input.ResponsibleStaffId;
            }

            await _context.SaveChangesAsync(cancellationToken);

            TryGetUserId(out var userId);
            await _auditService.WriteAsync(new AuditEvent(
                AuditModules.Booking,
                AuditActions.Updated,
                $"Đã cập nhật thông tin KOL/KOC: {kol.Name}",
                userId,
                "Kol",
                kol.Id.ToString()
            ), cancellationToken);

            TempData["SuccessMessage"] = "Đã cập nhật thông tin KOL/KOC thành công.";
            return RedirectToAction(nameof(Index));
        }

        TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Kols/Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        // Only QL Booking (BookingManager) can soft-delete
        if (role != AppRoles.BookingManager)
        {
            return Forbid();
        }

        var kol = await _context.Kols.FindAsync(new object[] { id }, cancellationToken);
        if (kol == null || !kol.IsActive)
        {
            TempData["ErrorMessage"] = "Không tìm thấy KOL/KOC.";
            return RedirectToAction(nameof(Index));
        }

        kol.IsActive = false;
        kol.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);

        TryGetUserId(out var userId);
        await _auditService.WriteAsync(new AuditEvent(
            AuditModules.Booking,
            AuditActions.Deleted,
            $"Đã ngừng hoạt động (xóa) KOL/KOC: {kol.Name}",
            userId,
            "Kol",
            kol.Id.ToString()
        ), cancellationToken);

        TempData["SuccessMessage"] = "Đã ngừng hoạt động KOL/KOC thành công.";
        return RedirectToAction(nameof(Index));
    }

    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
            CultureInfo.InvariantCulture, out userId);
    }
}
