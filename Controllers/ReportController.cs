using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.Services.Interfaces;

namespace QuanLyKho.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportController : Controller
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        public async Task<IActionResult> Revenue(DateTime? from = null, DateTime? to = null)
        {
            DateTime fromDate = from ?? DateTime.UtcNow.AddDays(-30).Date;
            DateTime toDate = to ?? DateTime.UtcNow.Date;

            var report = await _reportService.GetRevenueReportAsync(fromDate, toDate);
            ViewBag.From = fromDate.ToString("yyyy-MM-dd");
            ViewBag.To = toDate.ToString("yyyy-MM-dd");

            return View(report);
        }

        public async Task<IActionResult> Stock()
        {
            var report = await _reportService.GetStockReportAsync();
            return View(report);
        }

        public async Task<IActionResult> BestSellers(DateTime? from = null, DateTime? to = null, int top = 10)
        {
            DateTime fromDate = from ?? DateTime.UtcNow.AddDays(-30).Date;
            DateTime toDate = to ?? DateTime.UtcNow.Date;

            var bestSellers = await _reportService.GetBestSellersAsync(fromDate, toDate, top);
            ViewBag.From = fromDate.ToString("yyyy-MM-dd");
            ViewBag.To = toDate.ToString("yyyy-MM-dd");
            ViewBag.Top = top;

            return View(bestSellers);
        }

        public async Task<IActionResult> AuditLogs(int? userId = null, string? entityType = null, DateTime? from = null, DateTime? to = null)
        {
            var logs = await _reportService.GetAuditLogsAsync(userId, entityType, from, to);
            ViewBag.UserId = userId;
            ViewBag.EntityType = entityType;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");

            return View(logs);
        }
    }
}
