using System.Security.Claims;
using HanaMedia.Constants;
using HanaMedia.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Controllers;

[Authorize]
public class ProtectedFilesController(ApplicationDbContext db, IWebHostEnvironment env) : Controller
{
    [HttpGet("uploads/ideas/{ideaId:int}/{name}")]
    public async Task<IActionResult> IdeaFile(int ideaId, string name, CancellationToken ct)
    {
        if (name != Path.GetFileName(name)) return NotFound();
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role is not (AppRoles.Director or AppRoles.IdeaManager or AppRoles.IdeaStaff)) return Forbid();
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var url = $"/uploads/ideas/{ideaId}/{name}";
        var query = db.Ideas.Where(i => i.Id == ideaId &&
            (i.ReferenceFileUrl == url || i.MoodboardFileUrl == url || i.MoodboardImages.Any(m => m.FileUrl == url)));
        if (role != AppRoles.Director) query = query.Where(i => i.Campaign != null && i.Campaign.ConfirmedAt != null);
        if (role == AppRoles.IdeaStaff) query = query.Where(i => i.CreatorEmployee!.UserId == userId || i.PrimaryStaff!.UserId == userId ||
            i.Status == IdeaStatuses.Approved || i.Status == IdeaStatuses.InProgress || i.Status == IdeaStatuses.Done);
        if (!await query.AnyAsync(ct)) return NotFound();
        var file = Path.Combine(env.WebRootPath, "uploads", "ideas", ideaId.ToString(), name);
        if (!System.IO.File.Exists(file)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        var type = Path.GetExtension(name).ToLowerInvariant() switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", _ => "application/octet-stream" };
        return type.StartsWith("image/") ? PhysicalFile(file, type) : PhysicalFile(file, type, name);
    }

    [HttpGet("uploads/users/qrcodes/{name}")]
    [HttpGet("private-qr/{name}")]
    public async Task<IActionResult> QrByName(string name, CancellationToken ct)
    {
        if (name != Path.GetFileName(name)) return NotFound();
        var id = await db.Users.Where(u => u.QrCodeUrl == "/uploads/users/qrcodes/" + name || u.QrCodeUrl == "/private-qr/" + name).Select(u => u.Id).FirstOrDefaultAsync(ct);
        return id == 0 ? NotFound() : await Qr(id, ct);
    }
    [HttpGet("Profile/Qr/{userId:int}")]
    public async Task<IActionResult> Qr(int userId, CancellationToken ct)
    {
        var actor = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (actor != userId && role is not (AppRoles.Director or AppRoles.HumanResourcesManager or AppRoles.HumanResourcesStaff)) return Forbid();
        var url = await db.Users.Where(u => u.Id == userId).Select(u => u.QrCodeUrl).FirstOrDefaultAsync(ct);
        if (url == null) return NotFound();
        var name = Path.GetFileName(url);
        var root = url.StartsWith("/private-qr/") ? Path.Combine(env.ContentRootPath, "App_Data", "qrcodes") : Path.Combine(env.WebRootPath, "uploads", "users", "qrcodes");
        var file = Path.Combine(root, name);
        if (!System.IO.File.Exists(file)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(file, Path.GetExtension(name) == ".png" ? "image/png" : Path.GetExtension(name) == ".webp" ? "image/webp" : "image/jpeg");
    }
    [HttpGet("BookingDocuments/{name}")]
    [HttpGet("uploads/bookings/{name}")]
    public async Task<IActionResult> Document(string name, CancellationToken ct)
    {
        if (name != Path.GetFileName(name)) return NotFound();
        var url = "/BookingDocuments/" + name;
        var legacy = "/uploads/bookings/" + name;
        var b = await db.Bookings.Include(x => x.BookingWages).FirstOrDefaultAsync(x => x.ContractFileUrl == url || x.AcceptanceFileUrl == url || x.ContractFileUrl == legacy || x.QuotationFileUrl == legacy, ct);
        if (b == null) return NotFound();
        var role = User.FindFirstValue(ClaimTypes.Role);
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var employeeId = await db.Employees.Where(e => e.UserId == userId).Select(e => e.Id).FirstOrDefaultAsync(ct);
        var contract = b.ContractFileUrl == url || b.ContractFileUrl == legacy;
        var allowed = role is AppRoles.Director or AppRoles.BookingManager ||
            (role == AppRoles.BookingStaff && b.BookingWages.Any(w => w.EmployeeId == employeeId)) ||
            (role == AppRoles.LegalStaff && contract && b.ContractRevision > 0) ||
            (role == AppRoles.Accountant && contract && b.ContractStatus == "da_ky");
        if (!allowed) return Forbid();
        var file = Path.Combine(env.ContentRootPath, "App_Data", "booking-documents", name);
        if (!System.IO.File.Exists(file)) file = Path.Combine(env.WebRootPath, "uploads", "bookings", name);
        if (!System.IO.File.Exists(file)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(file, "application/octet-stream", name);
    }
}
