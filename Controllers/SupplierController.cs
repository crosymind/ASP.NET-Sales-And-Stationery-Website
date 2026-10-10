using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKho.Models.Entities;
using QuanLyKho.Services.Interfaces;

namespace QuanLyKho.Controllers
{
    [Authorize]
    public class SupplierController : Controller
    {
        private readonly ISupplierService _supplierService;

        public SupplierController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        public async Task<IActionResult> Index(string? search = null, bool showInactive = false)
        {
            var suppliers = await _supplierService.GetAllAsync(search, activeOnly: !showInactive);
            ViewBag.Search = search;
            ViewBag.ShowInactive = showInactive;
            return View(suppliers);
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Supplier supplier)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var res = await _supplierService.SaveAsync(supplier, userId);

            if (res.Success) TempData["Success"] = res.Message;
            else TempData["Error"] = res.Message;

            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin,Kho")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var res = await _supplierService.ToggleStatusAsync(id, userId);

            if (res.Success) TempData["Success"] = res.Message;
            else TempData["Error"] = res.Message;

            return RedirectToAction("Index");
        }
    }
}
