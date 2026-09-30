using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Controllers;

public record Payable(string Kind, int PayeeId, string Name, decimal Amount, bool Paid);
public static class BookingPayables
{
    public static List<Payable> For(Booking b)
    {
        var rows = b.BookingKols.Select(k => new Payable("cast", k.KolId, k.Kol.Name, k.CastAmount, false)).ToList();
        rows.AddRange(b.BookingWages.Select(w => new Payable("staff", w.EmployeeId, w.Employee.FullName, w.AllocatedWage, false)));
        if (b.PrimaryManagerId is int managerId)
            rows.Add(new("manager", managerId, b.PrimaryManager?.FullName ?? "QL phụ trách", b.CommissionPool - b.BookingWages.Sum(w => w.AllocatedWage), false));
        return rows.Select(r => r with { Paid = b.Payments.Any(p => p.Kind == r.Kind && p.PayeeId == r.PayeeId && p.IsPaid) }).ToList();
    }
}

[Authorize(Roles = AppRoles.Accountant + "," + AppRoles.Director)]
public class AccountingController(ApplicationDbContext db, ISystemAuditService audit) : Controller
{
    private IQueryable<Booking> Query() => db.Bookings.Include(b => b.PrimaryManager).Include(b => b.BookingKols).ThenInclude(k => k.Kol)
        .Include(b => b.BookingWages).ThenInclude(w => w.Employee).Include(b => b.Payments).Where(b => b.Status != "huy");
    private (DateTime Start, DateTime End) Period(string? period, DateTime? date)
    {
        var value = (date ?? DateTime.Today).Date;
        return period switch {
            "day" => (value, value.AddDays(1)),
            "quarter" => (new(value.Year, (value.Month - 1) / 3 * 3 + 1, 1), new DateTime(value.Year, (value.Month - 1) / 3 * 3 + 1, 1).AddMonths(3)),
            _ => (new(value.Year, value.Month, 1), new DateTime(value.Year, value.Month, 1).AddMonths(1)) };
    }
    [HttpGet("Accounting")]
    public async Task<IActionResult> Index(string? period, DateTime? date, CancellationToken ct)
    {
        var range = Period(period, date);
        ViewBag.Period = period ?? "month"; ViewBag.Date = (date ?? DateTime.Today).ToString("yyyy-MM-dd");
        return View(await Query().Where(b => b.CreatedAt >= range.Start && b.CreatedAt < range.End).OrderByDescending(b => b.Id).ToListAsync(ct));
    }
    [HttpPost("Accounting/Payment/{id:int}"), ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Accountant)]
    public async Task<IActionResult> Payment(int id, string kind, int payeeId, bool paid, decimal amount, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var b = await Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b == null) return NotFound();
        if (b.FinanceVersion != 1 || b.ContractStatus != "da_ky") return BadRequest("Chỉ đối soát Booking v4 có hợp đồng đã ký.");
        var payable = BookingPayables.For(b).SingleOrDefault(p => p.Kind == kind && p.PayeeId == payeeId);
        if (payable == null || payable.Amount <= 0 || payable.Amount != amount) return Conflict("Số tiền hoặc người nhận đã thay đổi. Tải lại trang.");
        var row = b.Payments.SingleOrDefault(p => p.Kind == kind && p.PayeeId == payeeId);
        if (row == null) { row = new BookingPayment { Kind = kind, PayeeId = payeeId }; b.Payments.Add(row); }
        if (row.IsPaid == paid) return RedirectToAction(nameof(Index));
        row.Amount = payable.Amount; row.IsPaid = paid; row.UpdatedAt = DateTime.Now;
        row.UpdatedByUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        audit.AddEvent(new AuditEvent(AuditModules.Booking, "payment_reconciled",
            $"Booking #{id}: {kind}/{payeeId}, {row.Amount:N2} đ, {(paid ? "đã thanh toán" : "chưa thanh toán")}. Đây là đối soát, không chuyển tiền.", row.UpdatedByUserId, "Booking", id.ToString()));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        TempData["SuccessMessage"] = "Đã cập nhật đối soát và ghi nhật ký.";
        return RedirectToAction(nameof(Index));
    }
    [HttpGet("Accounting/Export")]
    public async Task<IActionResult> Export(string? period, DateTime? date, CancellationToken ct)
    {
        var range = Period(period, date);
        var rows = await Query().Where(b => b.CreatedAt >= range.Start && b.CreatedAt < range.End).ToListAsync(ct);
        static string Cell(string value) => "\"" + ((value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) ? "'" : "") + value.Replace("\"", "\"\"") + "\"";
        var csv = new StringBuilder("ID,Client,Doanh thu,Chi phi,Loi nhuan,Cast,Hoa hong,Loai du lieu\r\n");
        foreach (var b in rows) csv.AppendLine(string.Join(",", b.Id, Cell(b.ClientName), b.BookingPrice.ToString(CultureInfo.InvariantCulture), b.ActualCost.ToString(CultureInfo.InvariantCulture), (b.BookingPrice-b.ActualCost).ToString(CultureInfo.InvariantCulture), b.CastPool.ToString(CultureInfo.InvariantCulture), b.CommissionPool.ToString(CultureInfo.InvariantCulture), b.FinanceVersion == 1 ? "v4" : "Lich su"));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", "doi-soat.csv");
    }
}
