using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HanaMedia.Constants;
using HanaMedia.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.BookingManager + "," + AppRoles.Director + "," + AppRoles.BookingStaff)]
    public class ManageBookingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ManageBookingController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
        {
            var bookings = await _context.Bookings
                .Include(b => b.PrimaryManager)
                .ToListAsync(cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.Today);

            // Status counts
            ViewBag.PendingCount = bookings.Count(b => b.Status == "dang_cho");
            ViewBag.NegotiatingCount = bookings.Count(b => b.Status == "thuong_luong");
            ViewBag.ClosedCount = bookings.Count(b => b.Status == "da_chot");
            ViewBag.RunningCount = bookings.Count(b => b.Status == "dang_trien_khai");
            ViewBag.CompletedCount = bookings.Count(b => b.Status == "hoan_thanh");
            ViewBag.CancelledCount = bookings.Count(b => b.Status == "huy");

            // Financial Metrics
            decimal totalRevenue = bookings.Where(b => b.Status != "huy").Sum(b => b.BookingPrice);
            decimal totalCost = bookings.Where(b => b.Status != "huy").Sum(b => b.ActualCost);
            decimal totalProfit = totalRevenue - totalCost;

            string FormatMoney(decimal amount)
            {
                if (amount >= 1000000000)
                {
                    return $"{(amount / 1000000000m):F2} tỷ".Replace(".00", "");
                }
                if (amount >= 1000000)
                {
                    return $"{(amount / 1000000m):F0} tr";
                }
                return $"{amount:N0} VNĐ";
            }

            ViewBag.RevenueText = FormatMoney(totalRevenue);
            ViewBag.CostText = FormatMoney(totalCost);
            ViewBag.ProfitText = FormatMoney(totalProfit);
            
            decimal margin = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0;
            ViewBag.ProfitMarginText = $"Biên lợi nhuận đạt ~{margin:F1}%";

            // Overdue bookings count
            ViewBag.OverdueCount = bookings.Count(b => b.Deadline < today && b.Status != "hoan_thanh" && b.Status != "huy");

            // Employee performance stats
            var managerStats = bookings
                .Where(b => b.PrimaryManagerId.HasValue)
                .GroupBy(b => b.PrimaryManager)
                .Select(g => {
                    int total = g.Count();
                    int completed = g.Count(b => b.Status == "hoan_thanh");
                    int kpi = total > 0 ? (completed * 100 / total) : 0;
                    return new ManagerStatViewModel
                    {
                        ManagerName = g.Key.FullName,
                        TotalCount = total,
                        CompletedCount = completed,
                        Kpi = kpi,
                        Rating = kpi >= 85 ? "Tốt" : "Cần cố gắng",
                        RatingClass = kpi >= 85 ? "status-good" : "status-warn"
                    };
                })
                .OrderByDescending(x => x.Kpi)
                .ToList();
            ViewBag.ManagerStats = managerStats;

            return View();
        }

        public IActionResult Booking()
        {
            return RedirectToAction("Index", "Booking");
        }

        public IActionResult KOL_KOC()
        {
            return RedirectToAction("Index", "Kol");
        }

        public IActionResult Reported()
        {
            return View();
        }

        public IActionResult StaffHuman()
        {
            return RedirectToAction("Index", "WorkTasks", new { module = WorkTaskModules.Booking });
        }
    }

    public class ManagerStatViewModel
    {
        public string ManagerName { get; set; } = null!;
        public int TotalCount { get; set; }
        public int CompletedCount { get; set; }
        public int Kpi { get; set; }
        public string Rating { get; set; } = null!;
        public string RatingClass { get; set; } = null!;
    }
}
