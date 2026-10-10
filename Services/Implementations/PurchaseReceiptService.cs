using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.Models.Entities;
using QuanLyKho.Patterns.Factory;
using QuanLyKho.Patterns.Observer;
using QuanLyKho.Patterns.State;
using QuanLyKho.Services.Interfaces;

namespace QuanLyKho.Services.Implementations
{
    public class PurchaseReceiptService : IPurchaseReceiptService
    {
        private readonly QuanLyKhoDbContext _context;
        private readonly IStockEventPublisher _eventPublisher;

        public PurchaseReceiptService(QuanLyKhoDbContext context, IStockEventPublisher eventPublisher)
        {
            _context = context;
            _eventPublisher = eventPublisher;
        }

        public async Task<List<PurchaseReceipt>> GetAllAsync(string? status = null, DateTime? from = null, DateTime? to = null)
        {
            var query = _context.PurchaseReceipts
                .Include(r => r.Supplier)
                .Include(r => r.CreatedBy)
                .Include(r => r.Items)
                    .ThenInclude(i => i.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }

            if (from.HasValue)
            {
                query = query.Where(r => r.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                var toEndOfDay = to.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(r => r.CreatedAt <= toEndOfDay);
            }

            return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        }

        public async Task<PurchaseReceipt?> GetByIdAsync(int id)
        {
            return await _context.PurchaseReceipts
                .Include(r => r.Supplier)
                .Include(r => r.CreatedBy)
                .Include(r => r.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.Unit)
                .FirstOrDefaultAsync(r => r.ReceiptId == id);
        }

        public async Task<(bool Success, string Message, int ReceiptId)> CreateReceiptAsync(
            int supplierId, string? notes, List<(int ProductId, int Quantity, decimal UnitCost)> items, int userId)
        {
            var supplier = await _context.Suppliers.FindAsync(supplierId);
            if (supplier == null || !supplier.IsActive)
            {
                return (false, "Nhà cung cấp không tồn tại hoặc đã ngừng hoạt động.", 0);
            }

            if (items == null || items.Count == 0)
            {
                return (false, "Phiếu nhập phải có ít nhất một mặt hàng.", 0);
            }

            string receiptCode = $"PN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";

            var receipt = new PurchaseReceipt
            {
                ReceiptCode = receiptCode,
                SupplierId = supplierId,
                Status = "Draft",
                Notes = notes,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow
            };

            decimal total = 0m;
            foreach (var item in items)
            {
                if (item.Quantity <= 0)
                {
                    return (false, "Số lượng nhập của mỗi sản phẩm phải lớn hơn 0.", 0);
                }
                if (item.UnitCost < 0)
                {
                    return (false, "Đơn giá nhập không được âm.", 0);
                }

                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null || !product.IsActive)
                {
                    return (false, $"Sản phẩm ID {item.ProductId} không hợp lệ hoặc đã ngừng kinh doanh.", 0);
                }

                decimal lineSubtotal = item.Quantity * item.UnitCost;
                total += lineSubtotal;

                receipt.Items.Add(new PurchaseReceiptItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost,
                    Subtotal = lineSubtotal
                });
            }

            receipt.TotalAmount = total;
            _context.PurchaseReceipts.Add(receipt);
            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "PURCHASE_RECEIPT_CREATED",
                EntityType = "PurchaseReceipt",
                EntityId = receipt.ReceiptId.ToString(),
                Details = $"Tạo phiếu nhập nháp {receipt.ReceiptCode} từ NCC {supplier.Name}, tổng tiền: {receipt.TotalAmount:N0} đ",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return (true, "Tạo phiếu nhập nháp thành công.", receipt.ReceiptId);
        }

        public async Task<(bool Success, string Message)> CompleteReceiptAsync(int receiptId, int userId)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var receipt = await _context.PurchaseReceipts
                    .Include(r => r.Supplier)
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.ReceiptId == receiptId);

                if (receipt == null)
                {
                    return (false, "Không tìm thấy phiếu nhập.");
                }

                // Kiểm tra trạng thái bằng State Pattern (BR.05, BR.07)
                var state = DocumentStateFactory.GetState(receipt.Status);
                if (!state.CanComplete)
                {
                    return (false, $"Phiếu nhập ở trạng thái '{receipt.Status}' không thể hoàn tất. Không cho phép hoàn tất lặp lại (Idempotency).");
                }

                if (receipt.Supplier == null || !receipt.Supplier.IsActive)
                {
                    return (false, "Nhà cung cấp đã ngừng hoạt động, không thể hoàn tất nhập hàng.");
                }

                if (receipt.Items.Count == 0)
                {
                    return (false, "Phiếu nhập không có chi tiết hàng hóa.");
                }

                foreach (var item in receipt.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null || !product.IsActive)
                    {
                        await tx.RollbackAsync();
                        return (false, $"Sản phẩm ID {item.ProductId} không hoạt động.");
                    }

                    int oldStock = product.StockQuantity;

                    // Tạo giao dịch kho bằng Factory Method
                    var invTx = InventoryTransactionFactory.CreateImport(
                        product, item.Quantity, receipt.ReceiptCode, receipt.ReceiptId, userId);
                    _context.InventoryTransactions.Add(invTx);

                    // Tăng tồn kho và cập nhật giá nhập mới nhất nếu có
                    product.StockQuantity += item.Quantity;
                    product.PurchasePrice = item.UnitCost;

                    // Phát sự kiện biến động tồn (Observer Pattern)
                    await _eventPublisher.PublishStockChangedAsync(new StockChangedEvent
                    {
                        ProductId = product.ProductId,
                        OldStock = oldStock,
                        NewStock = product.StockQuantity,
                        Type = "IMPORT",
                        SourceCode = receipt.ReceiptCode,
                        PerformedById = userId
                    }, _context);
                }

                receipt.Status = "Completed";
                receipt.CompletedAt = DateTime.UtcNow;

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "PURCHASE_RECEIPT_COMPLETED",
                    EntityType = "PurchaseReceipt",
                    EntityId = receipt.ReceiptId.ToString(),
                    Details = $"Hoàn tất phiếu nhập {receipt.ReceiptCode}, tăng tồn cho {receipt.Items.Count} sản phẩm",
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return (true, $"Hoàn tất phiếu nhập {receipt.ReceiptCode} thành công. Tồn kho đã được cập nhật.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, $"Lỗi khi hoàn tất phiếu nhập: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> CancelReceiptAsync(int receiptId, int userId)
        {
            var receipt = await _context.PurchaseReceipts.FindAsync(receiptId);
            if (receipt == null) return (false, "Không tìm thấy phiếu nhập.");

            var state = DocumentStateFactory.GetState(receipt.Status);
            if (!state.CanCancel)
            {
                return (false, $"Không thể hủy phiếu nhập ở trạng thái '{receipt.Status}'.");
            }

            receipt.Status = "Cancelled";

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "PURCHASE_RECEIPT_CANCELLED",
                EntityType = "PurchaseReceipt",
                EntityId = receipt.ReceiptId.ToString(),
                Details = $"Hủy phiếu nhập nháp {receipt.ReceiptCode}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return (true, "Đã hủy phiếu nhập nháp.");
        }
    }
}
