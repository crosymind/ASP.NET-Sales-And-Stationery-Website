using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

            // --- Chart data: doanh thu 7 ngày gần nhất ---
            var revLabels = new List<string>();
            var revData = new List<decimal>();
            for (int i = 6; i >= 0; i--)
            {
                var d = today.AddDays(-i);
                var dayReport = await _reportService.GetRevenueReportAsync(d, d);
                revLabels.Add(d.ToString("dd/MM"));
                revData.Add(dayReport.NetRevenue);
            }
            ViewBag.ChartRevLabels = JsonSerializer.Serialize(revLabels);
            ViewBag.ChartRevData = JsonSerializer.Serialize(revData);

            // --- Chart data: top 5 sản phẩm bán chạy 30 ngày ---
            var bestSellers = await _reportService.GetBestSellersAsync(today.AddDays(-30), today, 5);
            ViewBag.ChartBestLabels = JsonSerializer.Serialize(bestSellers.Select(x => x.ProductName).ToList());
            ViewBag.ChartBestData = JsonSerializer.Serialize(bestSellers.Select(x => x.NetQuantitySold).ToList());

            // --- Chart data: tỷ lệ nhập/xuất/trả 30 ngày ---
            var txAll = await _inventoryService.GetTransactionsAsync(null, null, today.AddDays(-30), today);
            ViewBag.ChartTxImport = txAll.Count(t => t.Type == "IMPORT");
            ViewBag.ChartTxSale   = txAll.Count(t => t.Type == "SALE");
            ViewBag.ChartTxReturn = txAll.Count(t => t.Type == "RETURN");
            ViewBag.ChartTxAdj    = txAll.Count(t => t.Type == "ADJUSTMENT");

            // --- So sánh hôm nay vs hôm qua ---
            var yesterdayRevenue = await _reportService.GetRevenueReportAsync(today.AddDays(-1), today.AddDays(-1));
            ViewBag.YesterdayRevenue = yesterdayRevenue;

            return View();
        }
    }
}
