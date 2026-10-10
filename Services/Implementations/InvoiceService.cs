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
using QuanLyKho.Patterns.Strategy;
using QuanLyKho.Services.Interfaces;

namespace QuanLyKho.Services.Implementations
{
    public class InvoiceService : IInvoiceService
    {
        private readonly QuanLyKhoDbContext _context;
        private readonly IStockEventPublisher _eventPublisher;

        public InvoiceService(QuanLyKhoDbContext context, IStockEventPublisher eventPublisher)
        {
            _context = context;
            _eventPublisher = eventPublisher;
        }

        public async Task<List<Invoice>> GetAllAsync(string? search = null, string? status = null, DateTime? from = null, DateTime? to = null)
        {
            var query = _context.Invoices
                .Include(i => i.CreatedBy)
                .Include(i => i.Items)
                    .ThenInclude(it => it.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(i => i.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string kw = search.Trim().ToLower();
                query = query.Where(i =>
                    i.InvoiceCode.ToLower().Contains(kw) ||
                    (i.CustomerName != null && i.CustomerName.ToLower().Contains(kw)) ||
                    (i.CustomerPhone != null && i.CustomerPhone.Contains(kw)));
            }

            if (from.HasValue)
            {
                query = query.Where(i => i.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                var toEndOfDay = to.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(i => i.CreatedAt <= toEndOfDay);
            }

            return await query.OrderByDescending(i => i.CreatedAt).ToListAsync();
        }

        public async Task<Invoice?> GetByIdAsync(int id)
        {
            return await _context.Invoices
                .Include(i => i.CreatedBy)
                .Include(i => i.Items)
                    .ThenInclude(it => it.Product)
                        .ThenInclude(p => p!.Unit)
                .Include(i => i.ReturnReceipts)
                    .ThenInclude(r => r.Items)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);
        }

        public async Task<(bool Success, string Message, int InvoiceId)> CreateAndCompleteInvoiceAsync(
            string? customerName,
            string? customerPhone,
            string discountType,
            decimal discountValue,
            string paymentMethod,
            List<(int ProductId, int Quantity)> items,
            int userId)
        {
            if (items == null || items.Count == 0)
            {
                return (false, "Hóa đơn phải có ít nhất một mặt hàng.", 0);
            }

            // BR.13: Phương thức thanh toán hợp lệ
            if (paymentMethod != "TienMat" && paymentMethod != "ChuyenKhoan")
            {
                return (false, "Phương thức thanh toán chỉ chấp nhận 'TienMat' (Tiền mặt) hoặc 'ChuyenKhoan' (Chuyển khoản).", 0);
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal subtotal = 0m;
                var invoiceItems = new List<InvoiceItem>();

                // Kiểm tra và gom nhóm các dòng cùng sản phẩm nếu có
                var groupedItems = items
                    .GroupBy(i => i.ProductId)
                    .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                    .ToList();

                foreach (var itm in groupedItems)
                {
                    if (itm.Quantity <= 0)
                    {
                        await tx.RollbackAsync();
                        return (false, "Số lượng sản phẩm bán phải lớn hơn 0.", 0);
                    }

                    // BR.04: Backend kiểm tra lại tồn kho khả dụng của tất cả sản phẩm tại thời điểm hoàn tất
                    var product = await _context.Products.FindAsync(itm.ProductId);
                    if (product == null || !product.IsActive)
                    {
                        await tx.RollbackAsync();
                        return (false, $"Sản phẩm ID {itm.ProductId} không tồn tại hoặc đã ngừng kinh doanh.", 0);
                    }

                    if (product.StockQuantity < itm.Quantity)
                    {
                        await tx.RollbackAsync();
                        return (false, $"Sản phẩm '{product.Name}' không đủ tồn kho! Hiện chỉ còn {product.StockQuantity} trong kho.", 0);
                    }

                    decimal lineSubtotal = itm.Quantity * product.SalePrice;
                    subtotal += lineSubtotal;

                    invoiceItems.Add(new InvoiceItem
                    {
                        ProductId = itm.ProductId,
                        Quantity = itm.Quantity,
                        UnitPrice = product.SalePrice,
                        Subtotal = lineSubtotal
                    });
                }

                // Strategy Pattern tính toán giảm giá (BR.12)
                var discountStrategy = DiscountStrategyFactory.GetStrategy(discountType);
                decimal discountAmount = discountStrategy.CalculateDiscount(subtotal, discountValue);

                if (discountAmount < 0 || discountAmount > subtotal)
                {
                    await tx.RollbackAsync();
                    return (false, "Giá trị giảm giá không hợp lệ (không âm và không vượt quá tạm tính).", 0);
                }

                decimal totalAmount = subtotal - discountAmount;
                string invoiceCode = $"HD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";

                var invoice = new Invoice
                {
                    InvoiceCode = invoiceCode,
                    Status = "Completed", // Bán trực tiếp tại quầy POS hoàn tất ngay
                    Subtotal = subtotal,
                    DiscountType = discountStrategy.Name,
                    DiscountValue = discountValue,
                    DiscountAmount = discountAmount,
                    TotalAmount = totalAmount,
                    PaymentMethod = paymentMethod,
                    CustomerName = customerName?.Trim(),
                    CustomerPhone = customerPhone?.Trim(),
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };

                foreach (var invItem in invoiceItems)
                {
                    invoice.Items.Add(invItem);
                }

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                // Trừ tồn kho và ghi sổ kho cho từng sản phẩm
                foreach (var invItem in invoice.Items)
                {
                    var product = await _context.Products.FindAsync(invItem.ProductId);
                    if (product == null) continue;

                    int oldStock = product.StockQuantity;

                    // Tạo giao dịch kho bằng Factory Method
                    var invTx = InventoryTransactionFactory.CreateSale(
                        product, invItem.Quantity, invoice.InvoiceCode, invoice.InvoiceId, userId);
                    _context.InventoryTransactions.Add(invTx);

                    // Trừ tồn kho
                    product.StockQuantity -= invItem.Quantity;

                    // Phát sự kiện biến động tồn (Observer Pattern: LowStockObserver & AuditLogObserver)
                    await _eventPublisher.PublishStockChangedAsync(new StockChangedEvent
                    {
                        ProductId = product.ProductId,
                        OldStock = oldStock,
                        NewStock = product.StockQuantity,
                        Type = "SALE",
                        SourceCode = invoice.InvoiceCode,
                        PerformedById = userId
                    }, _context);
                }

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "INVOICE_COMPLETED",
                    EntityType = "Invoice",
                    EntityId = invoice.InvoiceId.ToString(),
                    Details = $"Hoàn tất hóa đơn {invoice.InvoiceCode}, tổng thu: {invoice.TotalAmount:N0} đ, PTTT: {invoice.PaymentMethod}",
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return (true, $"Thanh toán thành công hóa đơn {invoice.InvoiceCode}!", invoice.InvoiceId);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, $"Lỗi xử lý hóa đơn: {ex.Message}", 0);
            }
        }

        public async Task<(bool Success, string Message)> CancelInvoiceAsync(int invoiceId, int userId)
        {
            var invoice = await _context.Invoices.FindAsync(invoiceId);
            if (invoice == null) return (false, "Không tìm thấy hóa đơn.");

            var state = DocumentStateFactory.GetState(invoice.Status);
            if (!state.CanCancel)
            {
                return (false, $"Hóa đơn đã ở trạng thái '{invoice.Status}', không thể hủy.");
            }

            invoice.Status = "Cancelled";
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "INVOICE_CANCELLED",
                EntityType = "Invoice",
                EntityId = invoice.InvoiceId.ToString(),
                Details = $"Hủy hóa đơn {invoice.InvoiceCode}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return (true, "Đã hủy hóa đơn.");
        }
    }
}
