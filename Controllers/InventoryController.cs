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
    public class InventoryController : Controller
    {
        private readonly IInventoryService _inventoryService;
        private readonly IProductService _productService;

        public InventoryController(IInventoryService inventoryService, IProductService productService)
        {
            _inventoryService = inventoryService;
            _productService = productService;
        }

        public async Task<IActionResult> Ledger(int? productId = null, string? type = null, DateTime? from = null, DateTime? to = null)
        {
            var transactions = await _inventoryService.GetTransactionsAsync(productId, type, from, to);
            var products = await _productService.GetAllAsync(activeOnly: false);

            ViewBag.ProductId = productId;
            ViewBag.Type = type;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.Products = products;

            return View(transactions);
        }

        public async Task<IActionResult> Alerts(string? status = "Open")
        {
            var alerts = await _inventoryService.GetAlertsAsync(status);
            ViewBag.Status = status;
            return View(alerts);
        }

        [Authorize(Roles = "Admin,Kho")]
        public async Task<IActionResult> Counts()
        {
            var counts = await _inventoryService.GetStockCountsAsync();
            return View(counts);
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpGet]
        public async Task<IActionResult> CreateCount()
        {
            var products = await _productService.GetAllAsync(activeOnly: true);
            return View(products);
        }

        public class StockCountItemInput
        {
            public int ProductId { get; set; }
            public int CountedQuantity { get; set; }
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCount(string reason, string itemsJson)
        {
            if (string.IsNullOrWhiteSpace(itemsJson))
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một sản phẩm để kiểm kê.";
                return RedirectToAction("CreateCount");
            }

            try
            {
                var parsedItems = JsonSerializer.Deserialize<List<StockCountItemInput>>(itemsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsedItems == null || parsedItems.Count == 0)
                {
                    TempData["Error"] = "Danh sách sản phẩm kiểm kê không hợp lệ.";
                    return RedirectToAction("CreateCount");
                }

                var items = new List<(int ProductId, int CountedQuantity)>();
                foreach (var pi in parsedItems)
                {
                    items.Add((pi.ProductId, pi.CountedQuantity));
                }

                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var res = await _inventoryService.CreateStockCountAsync(reason, items, userId);

                if (res.Success)
                {
                    TempData["Success"] = res.Message;
                    return RedirectToAction("DetailsCount", new { id = res.CountId });
                }

                TempData["Error"] = res.Message;
                return RedirectToAction("CreateCount");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi xử lý kiểm kê: {ex.Message}";
                return RedirectToAction("CreateCount");
            }
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpGet]
        public async Task<IActionResult> DetailsCount(int id)
        {
            var count = await _inventoryService.GetStockCountByIdAsync(id);
            if (count == null) return NotFound();
            return View(count);
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteCount(int id)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            bool isAdmin = User.IsInRole("Admin");

            var res = await _inventoryService.CompleteStockCountAsync(id, userId, isAdmin);

            if (res.Success) TempData["Success"] = res.Message;
            else TempData["Error"] = res.Message;

            return RedirectToAction("DetailsCount", new { id });
        }
    }
}
