using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Controllers;

[Authorize(Roles = AppRoles.LegalStaff)]
public class LegalController(ApplicationDbContext db, ISystemAuditService audit) : Controller
{
    [HttpGet("Legal")]
    public async Task<IActionResult> Index(string? status, CancellationToken ct)
    {
        var query = db.Bookings.Include(b => b.PrimaryManager).Where(b => b.ContractRevision > 0 && b.Status != "huy");
        if (status != "all") query = query.Where(b => b.ContractStatus == "cho_phap_ly");
        ViewBag.Status = status;
        return View(await query.OrderBy(b => b.UpdatedAt).ToListAsync(ct));
    }

    [HttpPost("Legal/Review/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, int revision, bool approve, string? feedback, CancellationToken ct)
    {
        var b = await db.Bookings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b == null) return NotFound();
        if (b.ContractStatus != "cho_phap_ly" || b.ContractRevision != revision || b.Status == "huy")
            return Conflict("Hợp đồng đã thay đổi hoặc đã được xử lý. Tải lại trang.");
        if ((!approve && string.IsNullOrWhiteSpace(feedback)) || feedback?.Length > 2000)
            return BadRequest("Cần lý do trả sửa, tối đa 2.000 ký tự.");
        b.ContractStatus = approve ? "cho_ky" : "phap_ly_tu_choi";
        b.LegalApprovedRevision = approve ? revision : null;
        b.LegalFeedback = feedback?.Trim(); b.LegalReviewedAt = DateTime.Now;
        b.LegalReviewedByUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        b.UpdatedAt = DateTime.Now;
        audit.AddEvent(new AuditEvent(AuditModules.Booking, approve ? "legal_approved" : "legal_rejected",
            $"Pháp lý {(approve ? "duyệt" : "trả sửa")} hợp đồng #{id}, phiên bản {revision}: {feedback}", b.LegalReviewedByUserId, "Booking", id.ToString()));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("Hợp đồng vừa được người khác xử lý."); }
        TempData["SuccessMessage"] = approve ? "Đã chuyển Giám đốc ký." : "Đã trả về QL phụ trách, lý do hiển thị tại Booking.";
        return RedirectToAction(nameof(Index));
    }
}
