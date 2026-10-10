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
    public class PurchaseReceiptController : Controller
    {
        private readonly IPurchaseReceiptService _receiptService;
        private readonly ISupplierService _supplierService;
        private readonly IProductService _productService;

        public PurchaseReceiptController(
            IPurchaseReceiptService receiptService,
            ISupplierService supplierService,
            IProductService productService)
        {
            _receiptService = receiptService;
            _supplierService = supplierService;
            _productService = productService;
        }

        public async Task<IActionResult> Index(string? status = null, DateTime? from = null, DateTime? to = null)
        {
            var receipts = await _receiptService.GetAllAsync(status, from, to);
            ViewBag.Status = status;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            return View(receipts);
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Suppliers = await _supplierService.GetAllAsync(activeOnly: true);
            ViewBag.Products = await _productService.GetAllAsync(activeOnly: true);
            return View();
        }

        public class ReceiptItemInput
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public decimal UnitCost { get; set; }
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int supplierId, string? notes, string itemsJson)
        {
            if (string.IsNullOrWhiteSpace(itemsJson))
            {
                TempData["Error"] = "Vui lòng thêm ít nhất một sản phẩm vào phiếu nhập.";
                return RedirectToAction("Create");
            }

            try
            {
                var parsedItems = JsonSerializer.Deserialize<List<ReceiptItemInput>>(itemsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsedItems == null || parsedItems.Count == 0)
                {
                    TempData["Error"] = "Danh sách sản phẩm không hợp lệ.";
                    return RedirectToAction("Create");
                }

                var items = new List<(int ProductId, int Quantity, decimal UnitCost)>();
                foreach (var pi in parsedItems)
                {
                    items.Add((pi.ProductId, pi.Quantity, pi.UnitCost));
                }

                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var res = await _receiptService.CreateReceiptAsync(supplierId, notes, items, userId);

                if (res.Success)
                {
                    TempData["Success"] = res.Message;
                    return RedirectToAction("Details", new { id = res.ReceiptId });
                }

                TempData["Error"] = res.Message;
                return RedirectToAction("Create");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi xử lý: {ex.Message}";
                return RedirectToAction("Create");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var receipt = await _receiptService.GetByIdAsync(id);
            if (receipt == null) return NotFound();
            return View(receipt);
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var res = await _receiptService.CompleteReceiptAsync(id, userId);

            if (res.Success) TempData["Success"] = res.Message;
            else TempData["Error"] = res.Message;

            return RedirectToAction("Details", new { id });
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var res = await _receiptService.CancelReceiptAsync(id, userId);

            if (res.Success) TempData["Success"] = res.Message;
            else TempData["Error"] = res.Message;

            return RedirectToAction("Details", new { id });
        }
    }
}
