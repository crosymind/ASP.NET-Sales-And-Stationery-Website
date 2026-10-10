using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.Services.Interfaces;

namespace QuanLyKho.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IReportService _reportService;
        private readonly IInventoryService _inventoryService;
        private readonly IInvoiceService _invoiceService;

        public HomeController(IReportService reportService, IInventoryService inventoryService, IInvoiceService invoiceService)
        {
            _reportService = reportService;
            _inventoryService = inventoryService;
            _invoiceService = invoiceService;
        }

        public async Task<IActionResult> Index()
        {
            DateTime today = DateTime.UtcNow.Date;
            var todayRevenue = await _reportService.GetRevenueReportAsync(today, today);
            var stockSummary = await _reportService.GetStockReportAsync();
            var openAlerts = await _inventoryService.GetAlertsAsync("Open");
            var recentInvoices = (await _invoiceService.GetAllAsync(null, "Completed", today.AddDays(-7), null)).Take(5).ToList();
            var recentTransactions = (await _inventoryService.GetTransactionsAsync(null, null, today.AddDays(-7), null)).Take(6).ToList();

            ViewBag.TodayRevenue = todayRevenue;
            ViewBag.StockSummary = stockSummary;
            ViewBag.OpenAlerts = openAlerts;
            ViewBag.RecentInvoices = recentInvoices;
            ViewBag.RecentTransactions = recentTransactions;

            return View();
        }
    }
}
