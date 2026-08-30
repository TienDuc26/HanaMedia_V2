using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Auditing;

namespace HanaMedia.Controllers
{
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISystemAuditService _auditService;

        public BookingController(ApplicationDbContext context, ISystemAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        private bool IsAuthorized(out string role, out int employeeId, out string userId)
        {
            role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            
            var emp = _context.Employees.FirstOrDefault(e => e.Email == User.Identity.Name);
            employeeId = emp?.Id ?? 0;

            return role == AppRoles.Director || role == AppRoles.BookingManager || role == AppRoles.BookingStaff;
        }

        [HttpGet("Bookings")]
        public async Task<IActionResult> Index(string? search, int? campaignId, int? kolId, string? status, int? managerId, CancellationToken cancellationToken)
        {
            if (!IsAuthorized(out var role, out var employeeId, out _))
            {
                return Forbid();
            }

            var query = _context.Bookings
                .Include(b => b.Campaign)
                .Include(b => b.Kol)
                .Include(b => b.PrimaryManager)
                .Include(b => b.BookingWages)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(b => b.ClientName.Contains(search) || 
                                         b.CampaignName.Contains(search) || 
                                         (b.Notes != null && b.Notes.Contains(search)) ||
                                         (b.JobDescription != null && b.JobDescription.Contains(search)));
            }

            if (campaignId.HasValue)
            {
                query = query.Where(b => b.CampaignId == campaignId);
            }

            if (kolId.HasValue)
            {
                query = query.Where(b => b.KolId == kolId);
            }

            if (managerId.HasValue)
            {
                query = query.Where(b => b.PrimaryManagerId == managerId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(b => b.Status == status);
            }

            var bookings = await query.OrderByDescending(b => b.CreatedAt).ToListAsync(cancellationToken);

            ViewBag.Campaigns = await _context.Campaigns.OrderBy(c => c.Name).ToListAsync(cancellationToken);
            ViewBag.Kols = await _context.Kols.Where(k => k.IsActive).OrderBy(k => k.Name).ToListAsync(cancellationToken);
            ViewBag.Employees = await _context.Employees.OrderBy(e => e.FullName).ToListAsync(cancellationToken);
            
            ViewBag.Search = search;
            ViewBag.CampaignId = campaignId;
            ViewBag.KolId = kolId;
            ViewBag.Status = status;
            ViewBag.ManagerId = managerId;
            
            ViewBag.IsWritable = role == AppRoles.BookingManager;

            return View(bookings);
        }

        [HttpGet("Bookings/Details/{id}")]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            if (!IsAuthorized(out _, out _, out _))
            {
                return Forbid();
            }

            var booking = await _context.Bookings
                .Include(b => b.Campaign)
                .Include(b => b.Kol)
                .Include(b => b.PrimaryManager)
                .Include(b => b.BookingWages)
                    .ThenInclude(bw => bw.Employee)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        [HttpPost("Bookings/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Booking model, int[] participantIds, IFormFile? contractFile, IFormFile? quotationFile, string deadlineStr, string? postingDateStr, CancellationToken cancellationToken)
        {
            if (!IsAuthorized(out var role, out _, out var userId) || role != AppRoles.BookingManager)
            {
                return Forbid();
            }

            // Sync Client and Campaign from Campaign model if selected
            if (model.CampaignId.HasValue)
            {
                var campaign = await _context.Campaigns.FindAsync(new object[] { model.CampaignId.Value }, cancellationToken);
                if (campaign != null)
                {
                    model.CampaignName = campaign.Name;
                    model.ClientName = campaign.Client;
                }
            }

            if (DateOnly.TryParse(deadlineStr, out var dl))
            {
                model.Deadline = dl;
            }
            else
            {
                ModelState.AddModelError("Deadline", "Hạn chót không hợp lệ.");
            }

            if (!string.IsNullOrEmpty(postingDateStr) && DateOnly.TryParse(postingDateStr, out var pd))
            {
                model.PostingDate = pd;
            }

            // File upload logic
            if (contractFile != null && contractFile.Length > 0)
            {
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "bookings");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                var fileName = $"contract_{Guid.NewGuid()}{Path.GetExtension(contractFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await contractFile.CopyToAsync(stream, cancellationToken);
                }
                model.ContractFileUrl = $"/uploads/bookings/{fileName}";
            }

            if (quotationFile != null && quotationFile.Length > 0)
            {
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "bookings");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                var fileName = $"quotation_{Guid.NewGuid()}{Path.GetExtension(quotationFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await quotationFile.CopyToAsync(stream, cancellationToken);
                }
                model.QuotationFileUrl = $"/uploads/bookings/{fileName}";
            }
            if (model.BookingPrice > 9999999999999.99m || model.BookingPrice < 0)
            {
                ModelState.AddModelError("BookingPrice", "Giá trị Booking không hợp lệ hoặc quá lớn.");
            }
            if (model.ActualCost > 9999999999999.99m || model.ActualCost < 0)
            {
                ModelState.AddModelError("ActualCost", "Chi phí thực tế không hợp lệ hoặc quá lớn.");
            }
            ModelState.Remove(nameof(model.Kol));
            ModelState.Remove(nameof(model.PrimaryManager));
            ModelState.Remove(nameof(model.Campaign));
            ModelState.Remove(nameof(model.BookingWages));
            ModelState.Remove(nameof(model.BookingWageAuditLogs));
            ModelState.Remove(nameof(model.CampaignName));
            ModelState.Remove(nameof(model.ClientName));

            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                model.UpdatedAt = DateTime.Now;

                _context.Bookings.Add(model);
                await _context.SaveChangesAsync(cancellationToken);

                // Save participating staff into booking_wages
                if (participantIds != null && participantIds.Length > 0)
                {
                    foreach (var empId in participantIds)
                    {
                        _context.BookingWages.Add(new BookingWage
                        {
                            BookingId = model.Id,
                            EmployeeId = empId,
                            AllocatedWage = 0, // Wage distribution is handled in Module 10
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        });
                    }
                    await _context.SaveChangesAsync(cancellationToken);
                }

                TryGetUserId(out var userIdInt);
                await _auditService.WriteAsync(new AuditEvent(
                    AuditModules.Booking,
                    AuditActions.Created,
                    $"Đã tạo Booking mới cho Client: {model.ClientName} (Campaign: {model.CampaignName})",
                    userIdInt,
                    "Booking",
                    model.Id.ToString()
                ), cancellationToken);

                TempData["SuccessMessage"] = "Đã tạo Booking mới thành công.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Có lỗi xảy ra khi tạo Booking. Vui lòng kiểm tra lại thông tin.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Bookings/Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Booking input, int[] participantIds, IFormFile? contractFile, IFormFile? quotationFile, string deadlineStr, string? postingDateStr, CancellationToken cancellationToken)
        {
            if (!IsAuthorized(out var role, out _, out var userId) || role != AppRoles.BookingManager)
            {
                return Forbid();
            }

            var booking = await _context.Bookings
                .Include(b => b.BookingWages)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

            if (booking == null)
            {
                return NotFound();
            }

            if (DateOnly.TryParse(deadlineStr, out var dl))
            {
                booking.Deadline = dl;
            }
            else
            {
                ModelState.AddModelError("Deadline", "Hạn chót không hợp lệ.");
            }

            if (!string.IsNullOrEmpty(postingDateStr) && DateOnly.TryParse(postingDateStr, out var pd))
            {
                booking.PostingDate = pd;
            }
            else
            {
                booking.PostingDate = null;
            }

            // Sync Client and Campaign from Campaign model if selected
            if (input.CampaignId.HasValue)
            {
                var campaign = await _context.Campaigns.FindAsync(new object[] { input.CampaignId.Value }, cancellationToken);
                if (campaign != null)
                {
                    booking.CampaignId = input.CampaignId;
                    booking.CampaignName = campaign.Name;
                    booking.ClientName = campaign.Client;
                }
            }
            else
            {
                booking.CampaignId = null;
                booking.CampaignName = input.CampaignName;
                booking.ClientName = input.ClientName;
            }

            // File uploads
            if (contractFile != null && contractFile.Length > 0)
            {
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "bookings");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                var fileName = $"contract_{Guid.NewGuid()}{Path.GetExtension(contractFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await contractFile.CopyToAsync(stream, cancellationToken);
                }
                booking.ContractFileUrl = $"/uploads/bookings/{fileName}";
            }

            if (quotationFile != null && quotationFile.Length > 0)
            {
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "bookings");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                var fileName = $"quotation_{Guid.NewGuid()}{Path.GetExtension(quotationFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await quotationFile.CopyToAsync(stream, cancellationToken);
                }
                booking.QuotationFileUrl = $"/uploads/bookings/{fileName}";
            }
            if (input.BookingPrice > 9999999999999.99m || input.BookingPrice < 0)
            {
                ModelState.AddModelError("BookingPrice", "Giá trị Booking không hợp lệ hoặc quá lớn.");
            }
            if (input.ActualCost > 9999999999999.99m || input.ActualCost < 0)
            {
                ModelState.AddModelError("ActualCost", "Chi phí thực tế không hợp lệ hoặc quá lớn.");
            }
            ModelState.Remove(nameof(input.Kol));
            ModelState.Remove(nameof(input.PrimaryManager));
            ModelState.Remove(nameof(input.Campaign));
            ModelState.Remove(nameof(input.BookingWages));
            ModelState.Remove(nameof(input.BookingWageAuditLogs));
            ModelState.Remove(nameof(input.CampaignName));
            ModelState.Remove(nameof(input.ClientName));

            if (ModelState.IsValid)
            {
                booking.KolId = input.KolId;
                booking.JobDescription = input.JobDescription;
                booking.BookingPrice = input.BookingPrice;
                booking.ActualCost = input.ActualCost;
                booking.PrimaryManagerId = input.PrimaryManagerId;
                booking.Status = input.Status;
                booking.PostLink = input.PostLink;
                booking.Notes = input.Notes;
                booking.UpdatedAt = DateTime.Now;

                // Sync participants in booking_wages
                var existingParticipants = booking.BookingWages.ToList();
                _context.BookingWages.RemoveRange(existingParticipants);

                if (participantIds != null && participantIds.Length > 0)
                {
                    foreach (var empId in participantIds)
                    {
                        var oldAlloc = existingParticipants.FirstOrDefault(x => x.EmployeeId == empId)?.AllocatedWage ?? 0;
                        _context.BookingWages.Add(new BookingWage
                        {
                            BookingId = booking.Id,
                            EmployeeId = empId,
                            AllocatedWage = oldAlloc,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        });
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);

                TryGetUserId(out var userIdInt);
                await _auditService.WriteAsync(new AuditEvent(
                    AuditModules.Booking,
                    AuditActions.Updated,
                    $"Đã cập nhật Booking ID: {booking.Id} cho Client: {booking.ClientName}",
                    userIdInt,
                    "Booking",
                    booking.Id.ToString()
                ), cancellationToken);

                TempData["SuccessMessage"] = "Đã cập nhật Booking thành công.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật thông tin Booking.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Bookings/Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            if (!IsAuthorized(out var role, out _, out var userId) || role != AppRoles.BookingManager)
            {
                return Forbid();
            }

            var booking = await _context.Bookings
                .Include(b => b.BookingWages)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

            if (booking == null)
            {
                return NotFound();
            }

            _context.BookingWages.RemoveRange(booking.BookingWages);
            _context.Bookings.Remove(booking);
            await _context.SaveChangesAsync(cancellationToken);

            TryGetUserId(out var userIdInt);
            await _auditService.WriteAsync(new AuditEvent(
                AuditModules.Booking,
                AuditActions.Deleted,
                $"Đã xóa Booking ID: {id} của Client: {booking.ClientName}",
                userIdInt,
                "Booking",
                id.ToString()
            ), cancellationToken);

            TempData["SuccessMessage"] = "Đã xóa Booking thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Bookings/Dashboard")]
        public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
        {
            if (!IsAuthorized(out _, out _, out _))
            {
                return Forbid();
            }

            var bookings = await _context.Bookings
                .Include(b => b.PrimaryManager)
                .ToListAsync(cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.Today);

            // 1. Count by Status
            var statuses = new Dictionary<string, int>
            {
                { "dang_cho", bookings.Count(b => b.Status == "dang_cho") },
                { "thuong_luong", bookings.Count(b => b.Status == "thuong_luong") },
                { "da_chot", bookings.Count(b => b.Status == "da_chot") },
                { "dang_trien_khai", bookings.Count(b => b.Status == "dang_trien_khai") },
                { "hoan_thanh", bookings.Count(b => b.Status == "hoan_thanh") },
                { "huy", bookings.Count(b => b.Status == "huy") }
            };
            ViewBag.Statuses = statuses;

            // 2. Financial Metrics
            decimal revenue = bookings.Where(b => b.Status != "huy").Sum(b => b.BookingPrice);
            decimal cost = bookings.Where(b => b.Status != "huy").Sum(b => b.ActualCost);
            decimal profit = revenue - cost;

            ViewBag.Revenue = revenue;
            ViewBag.Cost = cost;
            ViewBag.Profit = profit;

            // 3. Overdue Bookings (Deadline passed, status is not hoan_thanh or huy)
            var overdueBookings = bookings
                .Where(b => b.Deadline < today && b.Status != "hoan_thanh" && b.Status != "huy")
                .OrderBy(b => b.Deadline)
                .ToList();
            ViewBag.OverdueBookings = overdueBookings;

            // 4. Employee Performance (Group by PrimaryManagerId)
            var managerStats = bookings
                .Where(b => b.PrimaryManagerId.HasValue)
                .GroupBy(b => b.PrimaryManager)
                .Select(g => new ManagerPerformanceViewModel
                {
                    ManagerName = g.Key.FullName,
                    Position = g.Key.Position,
                    CompletedCount = g.Count(b => b.Status == "hoan_thanh"),
                    RunningCount = g.Count(b => b.Status == "dang_trien_khai"),
                    TotalCount = g.Count()
                })
                .OrderByDescending(x => x.CompletedCount)
                .ToList();
            ViewBag.ManagerStats = managerStats;

            return View();
        }

        private bool TryGetUserId(out int userId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }
    }

    public class ManagerPerformanceViewModel
    {
        public string ManagerName { get; set; } = null!;
        public string Position { get; set; } = null!;
        public int CompletedCount { get; set; }
        public int RunningCount { get; set; }
        public int TotalCount { get; set; }
    }
}
