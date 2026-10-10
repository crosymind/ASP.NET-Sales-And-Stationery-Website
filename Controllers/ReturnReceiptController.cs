using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.Services.Interfaces;

namespace QuanLyKho.Controllers
{
    [Authorize]
    public class ReturnReceiptController : Controller
    {
        private readonly IReturnReceiptService _returnService;
        private readonly IInvoiceService _invoiceService;

        public ReturnReceiptController(IReturnReceiptService returnService, IInvoiceService invoiceService)
        {
            _returnService = returnService;
            _invoiceService = invoiceService;
        }

        public async Task<IActionResult> Index(DateTime? from = null, DateTime? to = null)
        {
            var returns = await _returnService.GetAllAsync(from, to);
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            return View(returns);
        }

        [Authorize(Roles = "Admin,BanHang")]
        [HttpGet]
        public async Task<IActionResult> Create(int? invoiceId = null, string? searchCode = null)
        {
            if (invoiceId.HasValue && invoiceId.Value > 0)
            {
                var invoice = await _invoiceService.GetByIdAsync(invoiceId.Value);
                if (invoice != null && invoice.Status == "Completed")
                {
                    var returnedMap = await _returnService.GetReturnedQuantitiesByInvoiceAsync(invoice.InvoiceId);
                    ViewBag.Invoice = invoice;
                    ViewBag.ReturnedMap = returnedMap;
                }
                else
                {
                    ViewBag.Error = "Hóa đơn không tồn tại hoặc chưa hoàn tất.";
                }
            }
            else if (!string.IsNullOrWhiteSpace(searchCode))
            {
                var invoices = await _invoiceService.GetAllAsync(searchCode.Trim(), "Completed", null, null);
                var invoice = invoices.Count > 0 ? invoices[0] : null;
                if (invoice != null)
                {
                    return RedirectToAction("Create", new { invoiceId = invoice.InvoiceId });
                }
                ViewBag.Error = $"Không tìm thấy hóa đơn hợp lệ với mã '{searchCode}'.";
            }

            return View();
        }

        public class ReturnItemInput
        {
            public int InvoiceItemId { get; set; }
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public decimal RefundAmount { get; set; }
        }

        [Authorize(Roles = "Admin,BanHang")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int invoiceId, string reason, string itemsJson)
        {
            if (string.IsNullOrWhiteSpace(itemsJson))
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một sản phẩm cần trả.";
                return RedirectToAction("Create", new { invoiceId });
            }

            try
            {
                var parsedItems = JsonSerializer.Deserialize<List<ReturnItemInput>>(itemsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsedItems == null || parsedItems.Count == 0)
                {
                    TempData["Error"] = "Danh sách sản phẩm trả không hợp lệ.";
                    return RedirectToAction("Create", new { invoiceId });
                }

                var items = new List<(int InvoiceItemId, int ProductId, int Quantity, decimal RefundAmount)>();
                foreach (var pi in parsedItems)
                {
                    if (pi.Quantity > 0)
                    {
                        items.Add((pi.InvoiceItemId, pi.ProductId, pi.Quantity, pi.RefundAmount));
                    }
                }

                if (items.Count == 0)
                {
                    TempData["Error"] = "Số lượng trả của sản phẩm phải lớn hơn 0.";
                    return RedirectToAction("Create", new { invoiceId });
                }

                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var res = await _returnService.CreateAndCompleteReturnAsync(invoiceId, reason, items, userId);

                if (res.Success)
                {
                    TempData["Success"] = res.Message;
                    return RedirectToAction("Details", new { id = res.ReturnId });
                }

                TempData["Error"] = res.Message;
                return RedirectToAction("Create", new { invoiceId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi xử lý trả hàng: {ex.Message}";
                return RedirectToAction("Create", new { invoiceId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var returnReceipt = await _returnService.GetByIdAsync(id);
            if (returnReceipt == null) return NotFound();
            return View(returnReceipt);
        }
    }
}
