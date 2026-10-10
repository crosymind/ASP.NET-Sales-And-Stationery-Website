using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.Models.Entities;
using QuanLyKho.Services.Interfaces;

namespace QuanLyKho.Services.Implementations
{
    public class ProductService : IProductService
    {
        private readonly QuanLyKhoDbContext _context;

        public ProductService(QuanLyKhoDbContext context)
        {
            _context = context;
        }

        public async Task<List<Product>> GetAllAsync(string? search = null, int? categoryId = null, bool activeOnly = true)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Unit)
                .AsQueryable();

            if (activeOnly)
            {
                query = query.Where(p => p.IsActive);
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string kw = search.Trim().ToLower();
                query = query.Where(p =>
                    p.ProductCode.ToLower().Contains(kw) ||
                    p.Name.ToLower().Contains(kw) ||
                    (p.Barcode != null && p.Barcode.ToLower().Contains(kw)) ||
                    (p.Category != null && p.Category.Name.ToLower().Contains(kw)));
            }

            return await query.OrderBy(p => p.Name).ToListAsync();
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Unit)
                .FirstOrDefaultAsync(p => p.ProductId == id);
        }

        public async Task<Product?> GetByCodeOrBarcodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            string kw = code.Trim();

            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Unit)
                .FirstOrDefaultAsync(p => p.IsActive && (p.ProductCode == kw || p.Barcode == kw));
        }

        public async Task<(bool Success, string Message)> CreateAsync(Product product, int userId)
        {
            // BR.01: Mã sản phẩm và mã vạch phải duy nhất
            if (string.IsNullOrWhiteSpace(product.ProductCode))
            {
                return (false, "Mã sản phẩm không được để trống.");
            }

            bool codeExists = await _context.Products.AnyAsync(p => p.ProductCode == product.ProductCode);
            if (codeExists)
            {
                return (false, $"Mã sản phẩm '{product.ProductCode}' đã tồn tại.");
            }

            if (!string.IsNullOrWhiteSpace(product.Barcode))
            {
                bool barcodeExists = await _context.Products.AnyAsync(p => p.Barcode == product.Barcode);
                if (barcodeExists)
                {
                    return (false, $"Mã vạch '{product.Barcode}' đã được sử dụng cho sản phẩm khác.");
                }
            }

            // BR.02: Tên sản phẩm bắt buộc, tối đa 150 ký tự, đơn vị tính đang hoạt động
            if (string.IsNullOrWhiteSpace(product.Name) || product.Name.Length > 150)
            {
                return (false, "Tên sản phẩm bắt buộc và không quá 150 ký tự.");
            }

            var unit = await _context.Units.FindAsync(product.UnitId);
            if (unit == null || !unit.IsActive)
            {
                return (false, "Đơn vị tính không hợp lệ hoặc đã ngừng hoạt động.");
            }

            // BR.03: Giá nhập, giá bán, số lượng và ngưỡng tồn không âm
            if (product.PurchasePrice < 0 || product.SalePrice < 0 || product.StockQuantity < 0 || product.MinimumStock < 0)
            {
                return (false, "Giá nhập, giá bán, số lượng và ngưỡng tồn tối thiểu không được âm.");
            }

            product.CreatedAt = DateTime.UtcNow;
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Nếu số lượng tồn ban đầu > 0, tạo giao dịch kho khởi tạo
            if (product.StockQuantity > 0)
            {
                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = product.ProductId,
                    Type = "IMPORT",
                    QuantityDelta = product.StockQuantity,
                    StockBefore = 0,
                    StockAfter = product.StockQuantity,
                    SourceType = "InitProduct",
                    SourceId = product.ProductId,
                    SourceCode = $"INIT-{product.ProductCode}",
                    PerformedById = userId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Kiểm tra cảnh báo tồn thấp nếu tồn <= ngưỡng
            if (product.StockQuantity <= product.MinimumStock)
            {
                _context.LowStockAlerts.Add(new LowStockAlert
                {
                    ProductId = product.ProductId,
                    Status = "Open",
                    CurrentStock = product.StockQuantity,
                    MinimumStock = product.MinimumStock,
                    OpenedAt = DateTime.UtcNow
                });
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "PRODUCT_CREATED",
                EntityType = "Product",
                EntityId = product.ProductId.ToString(),
                Details = $"Thêm sản phẩm mới: [{product.ProductCode}] {product.Name}, Tồn: {product.StockQuantity}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return (true, "Thêm sản phẩm thành công.");
        }

        public async Task<(bool Success, string Message)> UpdateAsync(Product product, int userId)
        {
            var existing = await _context.Products.FindAsync(product.ProductId);
            if (existing == null) return (false, "Sản phẩm không tồn tại.");

            // BR.01: Check duplicate barcode
            if (!string.IsNullOrWhiteSpace(product.Barcode))
            {
                bool barcodeExists = await _context.Products
                    .AnyAsync(p => p.ProductId != product.ProductId && p.Barcode == product.Barcode);
                if (barcodeExists)
                {
                    return (false, $"Mã vạch '{product.Barcode}' đã được sử dụng cho sản phẩm khác.");
                }
            }

            if (string.IsNullOrWhiteSpace(product.Name) || product.Name.Length > 150)
            {
                return (false, "Tên sản phẩm bắt buộc và không quá 150 ký tự.");
            }

            if (product.PurchasePrice < 0 || product.SalePrice < 0 || product.MinimumStock < 0)
            {
                return (false, "Giá nhập, giá bán và ngưỡng tồn không được âm.");
            }

            existing.Name = product.Name;
            existing.Barcode = product.Barcode;
            existing.CategoryId = product.CategoryId;
            existing.UnitId = product.UnitId;
            existing.PurchasePrice = product.PurchasePrice;
            existing.SalePrice = product.SalePrice;
            existing.MinimumStock = product.MinimumStock;
            existing.IsActive = product.IsActive;

            // Kiểm tra lại cảnh báo tồn thấp theo MinimumStock mới
            var openAlert = await _context.LowStockAlerts
                .FirstOrDefaultAsync(a => a.ProductId == existing.ProductId && a.Status == "Open");

            if (existing.StockQuantity <= existing.MinimumStock)
            {
                if (openAlert == null)
                {
                    _context.LowStockAlerts.Add(new LowStockAlert
                    {
                        ProductId = existing.ProductId,
                        Status = "Open",
                        CurrentStock = existing.StockQuantity,
                        MinimumStock = existing.MinimumStock,
                        OpenedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    openAlert.MinimumStock = existing.MinimumStock;
                }
            }
            else if (openAlert != null)
            {
                openAlert.Status = "Closed";
                openAlert.ClosedAt = DateTime.UtcNow;
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "PRODUCT_UPDATED",
                EntityType = "Product",
                EntityId = existing.ProductId.ToString(),
                Details = $"Cập nhật sản phẩm [{existing.ProductCode}] {existing.Name}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return (true, "Cập nhật sản phẩm thành công.");
        }

        public async Task<(bool Success, string Message)> ToggleStatusAsync(int id, int userId)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return (false, "Không tìm thấy sản phẩm.");

            product.IsActive = !product.IsActive;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "PRODUCT_STATUS_CHANGED",
                EntityType = "Product",
                EntityId = product.ProductId.ToString(),
                Details = $"Đổi trạng thái SP [{product.ProductCode}] {product.Name}: Active={product.IsActive}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return (true, $"Đã {(product.IsActive ? "kích hoạt kinh doanh" : "ngừng kinh doanh")} sản phẩm.");
        }

        public async Task<List<Category>> GetCategoriesAsync(bool activeOnly = false)
        {
            var query = _context.Categories.AsQueryable();
            if (activeOnly) query = query.Where(c => c.IsActive);
            return await query.OrderBy(c => c.Name).ToListAsync();
        }

        public async Task<(bool Success, string Message)> SaveCategoryAsync(Category category, int userId)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                return (false, "Tên danh mục không được để trống.");
            }

            if (category.CategoryId == 0)
            {
                category.CreatedAt = DateTime.UtcNow;
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "CATEGORY_CREATED",
                    EntityType = "Category",
                    EntityId = category.CategoryId.ToString(),
                    Details = $"Tạo danh mục mới: {category.Name}",
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                var existing = await _context.Categories.FindAsync(category.CategoryId);
                if (existing == null) return (false, "Danh mục không tồn tại.");

                existing.Name = category.Name;
                existing.Description = category.Description;
                existing.IsActive = category.IsActive;

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "CATEGORY_UPDATED",
                    EntityType = "Category",
                    EntityId = existing.CategoryId.ToString(),
                    Details = $"Cập nhật danh mục: {existing.Name}",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return (true, "Lưu danh mục thành công.");
        }

        public async Task<List<Unit>> GetUnitsAsync(bool activeOnly = false)
        {
            var query = _context.Units.AsQueryable();
            if (activeOnly) query = query.Where(u => u.IsActive);
            return await query.OrderBy(u => u.Name).ToListAsync();
        }

        public async Task<(bool Success, string Message)> SaveUnitAsync(Unit unit, int userId)
        {
            if (string.IsNullOrWhiteSpace(unit.Name))
            {
                return (false, "Tên đơn vị tính không được để trống.");
            }

            if (unit.UnitId == 0)
            {
                unit.CreatedAt = DateTime.UtcNow;
                _context.Units.Add(unit);
                await _context.SaveChangesAsync();

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "UNIT_CREATED",
                    EntityType = "Unit",
                    EntityId = unit.UnitId.ToString(),
                    Details = $"Tạo đơn vị tính mới: {unit.Name}",
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                var existing = await _context.Units.FindAsync(unit.UnitId);
                if (existing == null) return (false, "Đơn vị tính không tồn tại.");

                existing.Name = unit.Name;
                existing.IsActive = unit.IsActive;

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "UNIT_UPDATED",
                    EntityType = "Unit",
                    EntityId = existing.UnitId.ToString(),
                    Details = $"Cập nhật đơn vị tính: {existing.Name}",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return (true, "Lưu đơn vị tính thành công.");
        }
    }
}
