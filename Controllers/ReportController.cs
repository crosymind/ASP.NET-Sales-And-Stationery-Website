using System;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;
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

        [HttpGet]
        public async Task<IActionResult> ExportRevenueExcel(DateTime? from = null, DateTime? to = null)
        {
            DateTime fromDate = from ?? DateTime.UtcNow.AddDays(-30).Date;
            DateTime toDate = to ?? DateTime.UtcNow.Date;
            var report = await _reportService.GetRevenueReportAsync(fromDate, toDate);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Doanh Thu");

            // Tiêu đề
            ws.Cell(1, 1).Value = "BÁO CÁO DOANH THU THUẦN";
            ws.Range(1, 1, 1, 7).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(14)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(2, 1).Value = $"Từ ngày: {fromDate:dd/MM/yyyy}  →  Đến ngày: {toDate:dd/MM/yyyy}";
            ws.Range(2, 1, 2, 7).Merge().Style
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // KPI tổng hợp
            int row = 4;
            ws.Cell(row, 1).Value = "Tổng doanh thu bán hàng"; ws.Cell(row, 2).Value = report.GrossRevenue; ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            row++;
            ws.Cell(row, 1).Value = "Tiền hoàn trả hàng"; ws.Cell(row, 2).Value = -report.TotalRefundAmount; ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            row++;
            ws.Cell(row, 1).Value = "DOANH THU THUẦN"; ws.Cell(row, 2).Value = report.NetRevenue;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            ws.Range(row, 1, row, 2).Style.Font.SetBold(true).Fill.SetBackgroundColor(XLColor.LightGreen);
            row++;
            ws.Cell(row, 1).Value = "Tiền mặt"; ws.Cell(row, 2).Value = report.CashAmount; ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            row++;
            ws.Cell(row, 1).Value = "Chuyển khoản"; ws.Cell(row, 2).Value = report.BankTransferAmount; ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            row += 2;

            // Header bảng hóa đơn
            var headers = new[] { "Mã HĐ", "Thời gian", "Thu ngân", "Khách hàng", "Hình thức TT", "Tổng tiền (đ)", "Trạng thái" };
            for (int c = 0; c < headers.Length; c++)
            {
                ws.Cell(row, c + 1).Value = headers[c];
            }
            ws.Row(row).Style.Font.SetBold(true).Fill.SetBackgroundColor(XLColor.FromHtml("#4f46e5"));
            ws.Row(row).Style.Font.SetFontColor(XLColor.White);
            ws.Row(row).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row++;

            foreach (var inv in report.Invoices)
            {
                ws.Cell(row, 1).Value = inv.InvoiceCode;
                ws.Cell(row, 2).Value = inv.CreatedAt.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(row, 3).Value = inv.CreatedBy?.FullName ?? inv.CreatedBy?.UserName ?? "";
                ws.Cell(row, 4).Value = inv.CustomerName ?? "(Khách lẻ)";
                ws.Cell(row, 5).Value = inv.PaymentMethod == "TienMat" ? "Tiền mặt" : "Chuyển khoản";
                ws.Cell(row, 6).Value = inv.TotalAmount;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 7).Value = inv.Status == "Completed" ? "Hoàn tất" : inv.Status;
                row++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var fileName = $"BaoCaoDoanhThu_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        public async Task<IActionResult> Stock()
        {
            var report = await _reportService.GetStockReportAsync();
            return View(report);
        }

        [HttpGet]
        public async Task<IActionResult> ExportStockExcel()
        {
            var report = await _reportService.GetStockReportAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Tồn Kho");

            ws.Cell(1, 1).Value = "BÁO CÁO TỒN KHO & GIÁ TRỊ TỒN";
            ws.Range(1, 1, 1, 8).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(14)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(2, 1).Value = $"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}";
            ws.Range(2, 1, 2, 8).Merge().Style
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // KPI
            int row = 4;
            ws.Cell(row, 1).Value = "Tổng mặt hàng"; ws.Cell(row, 2).Value = report.TotalProductCount;
            row++;
            ws.Cell(row, 1).Value = "Tổng số lượng tồn"; ws.Cell(row, 2).Value = report.TotalStockQuantity;
            row++;
            ws.Cell(row, 1).Value = "Tổng giá trị tồn ước tính"; ws.Cell(row, 2).Value = report.EstimatedTotalValue;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            ws.Range(row, 1, row, 2).Style.Font.SetBold(true).Fill.SetBackgroundColor(XLColor.LightCyan);
            row += 2;

            // Header
            var headers = new[] { "Mã SP", "Tên sản phẩm", "Danh mục", "ĐVT", "Giá nhập (đ)", "Tồn kho", "Ngưỡng tối thiểu", "Giá trị tồn (đ)" };
            for (int c = 0; c < headers.Length; c++)
                ws.Cell(row, c + 1).Value = headers[c];
            ws.Row(row).Style.Font.SetBold(true).Fill.SetBackgroundColor(XLColor.FromHtml("#0ea5e9"));
            ws.Row(row).Style.Font.SetFontColor(XLColor.White);
            row++;

            foreach (var p in report.Products)
            {
                bool isLow = p.StockQuantity <= p.MinimumStock;
                ws.Cell(row, 1).Value = p.ProductCode;
                ws.Cell(row, 2).Value = p.Name;
                ws.Cell(row, 3).Value = p.Category?.Name ?? "";
                ws.Cell(row, 4).Value = p.Unit?.Name ?? "";
                ws.Cell(row, 5).Value = p.PurchasePrice; ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 6).Value = p.StockQuantity;
                ws.Cell(row, 7).Value = p.MinimumStock;
                ws.Cell(row, 8).Value = p.StockQuantity * p.PurchasePrice; ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
                if (isLow)
                    ws.Row(row).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#fee2e2"));
                row++;
            }

            // Dòng tổng
            ws.Cell(row, 7).Value = "TỔNG GIÁ TRỊ KHO:";
            ws.Cell(row, 8).Value = report.EstimatedTotalValue;
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
            ws.Range(row, 7, row, 8).Style.Font.SetBold(true).Fill.SetBackgroundColor(XLColor.LightGreen);

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BaoCaoTonKho_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
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

        [HttpGet]
        public async Task<IActionResult> ExportBestSellersExcel(DateTime? from = null, DateTime? to = null, int top = 10)
        {
            DateTime fromDate = from ?? DateTime.UtcNow.AddDays(-30).Date;
            DateTime toDate = to ?? DateTime.UtcNow.Date;
            var list = await _reportService.GetBestSellersAsync(fromDate, toDate, top);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Bán Chạy");

            ws.Cell(1, 1).Value = $"TOP {top} SẢN PHẨM BÁN CHẠY";
            ws.Range(1, 1, 1, 9).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(14)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(2, 1).Value = $"Từ {fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy}";
            ws.Range(2, 1, 2, 9).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            int row = 4;
            var headers = new[] { "Hạng", "Mã SP", "Tên sản phẩm", "Danh mục", "ĐVT", "Số lượng bán", "Số lượng trả", "Bán ròng", "Doanh thu (đ)" };
            for (int c = 0; c < headers.Length; c++)
                ws.Cell(row, c + 1).Value = headers[c];
            ws.Row(row).Style.Font.SetBold(true).Fill.SetBackgroundColor(XLColor.FromHtml("#f59e0b"));
            ws.Row(row).Style.Font.SetFontColor(XLColor.White);
            row++;

            int rank = 1;
            foreach (var item in list)
            {
                ws.Cell(row, 1).Value = rank++;
                ws.Cell(row, 2).Value = item.ProductCode;
                ws.Cell(row, 3).Value = item.ProductName;
                ws.Cell(row, 4).Value = item.CategoryName;
                ws.Cell(row, 5).Value = item.UnitName;
                ws.Cell(row, 6).Value = item.QuantitySold;
                ws.Cell(row, 7).Value = item.QuantityReturned;
                ws.Cell(row, 8).Value = item.NetQuantitySold;
                ws.Cell(row, 9).Value = item.TotalRevenue; ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
                row++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BanChay_Top{top}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx");
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
