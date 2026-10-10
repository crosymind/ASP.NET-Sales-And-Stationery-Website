using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.Models.Entities;
using QuanLyKho.Services.Interfaces;

namespace QuanLyKho.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        public async Task<IActionResult> Index(string? search = null, int? categoryId = null, bool showInactive = false)
        {
            var products = await _productService.GetAllAsync(search, categoryId, activeOnly: !showInactive);
            var categories = await _productService.GetCategoriesAsync(activeOnly: false);

            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.ShowInactive = showInactive;
            ViewBag.Categories = categories;

            return View(products);
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _productService.GetCategoriesAsync(activeOnly: true);
            ViewBag.Units = await _productService.GetUnitsAsync(activeOnly: true);
            return View(new Product { MinimumStock = 5 });
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            if (ModelState.IsValid)
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var res = await _productService.CreateAsync(product, userId);
                if (res.Success)
                {
                    TempData["Success"] = res.Message;
                    return RedirectToAction("Index");
                }
                ModelState.AddModelError(string.Empty, res.Message);
            }

            ViewBag.Categories = await _productService.GetCategoriesAsync(activeOnly: true);
            ViewBag.Units = await _productService.GetUnitsAsync(activeOnly: true);
            return View(product);
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _productService.GetByIdAsync(id);
            if (product == null) return NotFound();

            ViewBag.Categories = await _productService.GetCategoriesAsync(activeOnly: false);
            ViewBag.Units = await _productService.GetUnitsAsync(activeOnly: false);
            return View(product);
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product product)
        {
            if (ModelState.IsValid)
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var res = await _productService.UpdateAsync(product, userId);
                if (res.Success)
                {
                    TempData["Success"] = res.Message;
                    return RedirectToAction("Index");
                }
                ModelState.AddModelError(string.Empty, res.Message);
            }

            ViewBag.Categories = await _productService.GetCategoriesAsync(activeOnly: false);
            ViewBag.Units = await _productService.GetUnitsAsync(activeOnly: false);
            return View(product);
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var res = await _productService.ToggleStatusAsync(id, userId);

            if (res.Success) TempData["Success"] = res.Message;
            else TempData["Error"] = res.Message;

            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var categories = await _productService.GetCategoriesAsync();
            return View(categories);
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCategory(Category category)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var res = await _productService.SaveCategoryAsync(category, userId);

            if (res.Success) TempData["Success"] = res.Message;
            else TempData["Error"] = res.Message;

            return RedirectToAction("Categories");
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpGet]
        public async Task<IActionResult> Units()
        {
            var units = await _productService.GetUnitsAsync();
            return View(units);
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveUnit(Unit unit)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var res = await _productService.SaveUnitAsync(unit, userId);

            if (res.Success) TempData["Success"] = res.Message;
            else TempData["Error"] = res.Message;

            return RedirectToAction("Units");
        }

        [HttpGet]
        public async Task<IActionResult> SearchApi(string? q)
        {
            var products = await _productService.GetAllAsync(q, null, activeOnly: true);
            var result = products.Select(p => new
            {
                id = p.ProductId,
                code = p.ProductCode,
                barcode = p.Barcode,
                name = p.Name,
                unit = p.Unit?.Name ?? "",
                category = p.Category?.Name ?? "",
                salePrice = p.SalePrice,
                purchasePrice = p.PurchasePrice,
                stock = p.StockQuantity
            });
            return Json(result);
        }
    }
}
