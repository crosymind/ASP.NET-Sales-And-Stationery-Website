using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.Models.Entities;
using QuanLyKho.Services.Interfaces;

namespace QuanLyKho.Controllers
{
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        private int CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : 1;

        public async Task<IActionResult> Index(string? search = null, string? group = null, string? status = null)
        {
            var allCustomers = await _customerService.GetAllAsync(search);

            // Thống kê tổng quan toàn bộ danh sách trước khi lọc nhóm/trạng thái
            ViewBag.TotalCount = allCustomers.Count;
            ViewBag.ActiveCount = allCustomers.Count(c => c.IsActive);
            ViewBag.VipCount = allCustomers.Count(c => c.CustomerGroup == "VIP");
            ViewBag.WholesaleCount = allCustomers.Count(c => c.CustomerGroup == "Bán buôn");
            ViewBag.TotalSpentAll = allCustomers.Sum(c => c.TotalSpent);
            ViewBag.TotalPointsAll = allCustomers.Sum(c => c.Points);

            var filtered = allCustomers.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(group))
            {
                filtered = filtered.Where(c => c.CustomerGroup == group);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "active")
                    filtered = filtered.Where(c => c.IsActive);
                else if (status == "inactive")
                    filtered = filtered.Where(c => !c.IsActive);
            }

            ViewBag.Search = search;
            ViewBag.Group = group;
            ViewBag.Status = status;

            return View(filtered.ToList());
        }

        public async Task<IActionResult> Details(int id)
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng.";
                return RedirectToAction(nameof(Index));
            }

            var invoices = await _customerService.GetCustomerInvoicesAsync(id);
            ViewBag.Invoices = invoices;
            return View(customer);
        }

        public IActionResult Create()
        {
            return View(new Customer());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            if (string.IsNullOrWhiteSpace(customer.FullName) || string.IsNullOrWhiteSpace(customer.PhoneNumber))
            {
                ModelState.AddModelError("", "Họ tên và số điện thoại là bắt buộc.");
                return View(customer);
            }

            var (success, message, customerId) = await _customerService.SaveAsync(customer, CurrentUserId);
            if (success)
            {
                TempData["Success"] = message;
                return RedirectToAction(nameof(Details), new { id = customerId });
            }

            TempData["Error"] = message;
            return View(customer);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng.";
                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Customer customer)
        {
            if (string.IsNullOrWhiteSpace(customer.FullName) || string.IsNullOrWhiteSpace(customer.PhoneNumber))
            {
                ModelState.AddModelError("", "Họ tên và số điện thoại là bắt buộc.");
                return View(customer);
            }

            var (success, message, _) = await _customerService.SaveAsync(customer, CurrentUserId);
            if (success)
            {
                TempData["Success"] = message;
                return RedirectToAction(nameof(Details), new { id = customer.CustomerId });
            }

            TempData["Error"] = message;
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var (success, message) = await _customerService.ToggleStatusAsync(id, CurrentUserId);
            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var (success, message) = await _customerService.DeleteAsync(id, CurrentUserId);
            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustPoints(int id, int pointsDelta, string? reason)
        {
            var (success, message) = await _customerService.AdjustPointsAsync(id, pointsDelta, reason ?? "", CurrentUserId);
            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? search = null, string? group = null, string? status = null)
        {
            var customers = await _customerService.GetAllAsync(search);
            var filtered = customers.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(group))
                filtered = filtered.Where(c => c.CustomerGroup == group);

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "active")
                    filtered = filtered.Where(c => c.IsActive);
                else if (status == "inactive")
                    filtered = filtered.Where(c => !c.IsActive);
            }

            var list = filtered.ToList();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Danh Sách Khách Hàng");

            ws.Cell(1, 1).Value = "DANH SÁCH KHÁCH HÀNG (CRM) - VĂN PHÒNG PHẨM MINH ANH";
            ws.Range(1, 1, 1, 10).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(14)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(2, 1).Value = $"Ngày xuất báo cáo: {DateTime.Now:dd/MM/yyyy HH:mm} - Tổng số: {list.Count} khách hàng";
            ws.Range(2, 1, 2, 10).Merge().Style
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            string[] headers = { "STT", "Mã KH", "Họ và Tên", "Số Điện Thoại", "Email", "Địa Chỉ", "Phân Hạng", "Điểm Tích Lũy", "Tổng Chi Tiêu (VNĐ)", "Trạng Thái" };
            int headerRow = 4;
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(headerRow, i + 1).Value = headers[i];
            }

            var headerRange = ws.Range(headerRow, 1, headerRow, headers.Length);
            headerRange.Style.Font.SetBold(true);
            headerRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E2E8F0"));
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int row = headerRow + 1;
            int stt = 1;
            foreach (var c in list)
            {
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = c.CustomerCode;
                ws.Cell(row, 3).Value = c.FullName;
                ws.Cell(row, 4).Value = c.PhoneNumber;
                ws.Cell(row, 5).Value = c.Email ?? "";
                ws.Cell(row, 6).Value = c.Address ?? "";
                ws.Cell(row, 7).Value = c.CustomerGroup;
                ws.Cell(row, 8).Value = c.Points;
                ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 9).Value = c.TotalSpent;
                ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 10).Value = c.IsActive ? "Đang hoạt động" : "Ngừng hoạt động";

                var rowRange = ws.Range(row, 1, row, headers.Length);
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DanhSachKhachHang_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        // Endpoint for POS customer search / autocomplete
        [HttpGet]
        public async Task<IActionResult> SearchApi(string? term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return Json(new object[] { });

            var list = await _customerService.GetAllAsync(term, activeOnly: true);
            var results = list.Select(c => new
            {
                id = c.CustomerId,
                code = c.CustomerCode,
                name = c.FullName,
                phone = c.PhoneNumber,
                group = c.CustomerGroup,
                points = c.Points,
                totalSpent = c.TotalSpent
            });

            return Json(results);
        }

        public class QuickCustomerDto
        {
            public string? FullName { get; set; }
            public string? PhoneNumber { get; set; }
            public string? CustomerGroup { get; set; }
            public string? Address { get; set; }
        }

        // Endpoint for POS quick customer creation
        [HttpPost]
        public async Task<IActionResult> CreateQuickApi([FromBody] QuickCustomerDto input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.FullName) || string.IsNullOrWhiteSpace(input.PhoneNumber))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ Họ tên và Số điện thoại." });
            }

            var customer = new Customer
            {
                FullName = input.FullName.Trim(),
                PhoneNumber = input.PhoneNumber.Trim(),
                CustomerGroup = string.IsNullOrWhiteSpace(input.CustomerGroup) ? "Thường" : input.CustomerGroup,
                Address = input.Address?.Trim()
            };

            var (success, message, customerId) = await _customerService.SaveAsync(customer, CurrentUserId);
            if (!success)
            {
                return Json(new { success = false, message });
            }

            var saved = await _customerService.GetByIdAsync(customerId);
            return Json(new
            {
                success = true,
                message,
                customer = new
                {
                    id = saved!.CustomerId,
                    code = saved.CustomerCode,
                    name = saved.FullName,
                    phone = saved.PhoneNumber,
                    group = saved.CustomerGroup,
                    points = saved.Points,
                    totalSpent = saved.TotalSpent
                }
            });
        }
    }
}
