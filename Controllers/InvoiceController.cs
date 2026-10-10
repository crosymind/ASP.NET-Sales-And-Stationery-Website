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
    public class InvoiceController : Controller
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IProductService _productService;

        public InvoiceController(IInvoiceService invoiceService, IProductService productService)
        {
            _invoiceService = invoiceService;
            _productService = productService;
        }

        public async Task<IActionResult> Index(string? search = null, string? status = null, DateTime? from = null, DateTime? to = null)
        {
            var invoices = await _invoiceService.GetAllAsync(search, status, from, to);
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            return View(invoices);
        }

        [Authorize(Roles = "Admin,BanHang")]
        [HttpGet]
        public async Task<IActionResult> Pos()
        {
            var products = await _productService.GetAllAsync(activeOnly: true);
            var categories = await _productService.GetCategoriesAsync(activeOnly: true);
            ViewBag.Categories = categories;
            return View(products);
        }

        public class PosItemInput
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        public class PosCheckoutRequest
        {
            public string? CustomerName { get; set; }
            public string? CustomerPhone { get; set; }
            public string DiscountType { get; set; } = "None"; // None, Percentage, Fixed
            public decimal DiscountValue { get; set; }
            public string PaymentMethod { get; set; } = "TienMat"; // TienMat, ChuyenKhoan
            public List<PosItemInput> Items { get; set; } = new();
        }

        [Authorize(Roles = "Admin,BanHang")]
        [HttpPost]
        public async Task<IActionResult> Checkout([FromBody] PosCheckoutRequest request)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return Json(new { success = false, message = "Giỏ hàng trống, vui lòng chọn ít nhất một sản phẩm." });
            }

            var items = new List<(int ProductId, int Quantity)>();
            foreach (var item in request.Items)
            {
                items.Add((item.ProductId, item.Quantity));
            }

            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var res = await _invoiceService.CreateAndCompleteInvoiceAsync(
                request.CustomerName,
                request.CustomerPhone,
                request.DiscountType,
                request.DiscountValue,
                request.PaymentMethod,
                items,
                userId);

            if (res.Success)
            {
                return Json(new { success = true, message = res.Message, invoiceId = res.InvoiceId });
            }

            return Json(new { success = false, message = res.Message });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _invoiceService.GetByIdAsync(id);
            if (invoice == null) return NotFound();
            return View(invoice);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var res = await _invoiceService.CancelInvoiceAsync(id, userId);

            if (res.Success) TempData["Success"] = res.Message;
            else TempData["Error"] = res.Message;

            return RedirectToAction("Details", new { id });
        }
    }
}
