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
    public class ReturnReceiptService : IReturnReceiptService
    {
        private readonly QuanLyKhoDbContext _context;
        private readonly IStockEventPublisher _eventPublisher;

        public ReturnReceiptService(QuanLyKhoDbContext context, IStockEventPublisher eventPublisher)
        {
            _context = context;
            _eventPublisher = eventPublisher;
        }

        public async Task<List<ReturnReceipt>> GetAllAsync(DateTime? from = null, DateTime? to = null)
        {
            var query = _context.ReturnReceipts
                .Include(r => r.Invoice)
                .Include(r => r.CreatedBy)
                .Include(r => r.Items)
                    .ThenInclude(i => i.Product)
                .AsQueryable();

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

        public async Task<ReturnReceipt?> GetByIdAsync(int id)
        {
            return await _context.ReturnReceipts
                .Include(r => r.Invoice)
                .Include(r => r.CreatedBy)
                .Include(r => r.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.Unit)
                .FirstOrDefaultAsync(r => r.ReturnId == id);
        }

        public async Task<Dictionary<int, int>> GetReturnedQuantitiesByInvoiceAsync(int invoiceId)
        {
            // Tính tổng số lượng đã trả thành công theo từng InvoiceItemId
            var returnedItems = await _context.ReturnReceiptItems
                .Where(ri => ri.ReturnReceipt!.InvoiceId == invoiceId && ri.ReturnReceipt.Status == "Completed")
                .GroupBy(ri => ri.InvoiceItemId)
                .Select(g => new { InvoiceItemId = g.Key, TotalReturned = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.InvoiceItemId, x => x.TotalReturned);

            return returnedItems;
        }

        public async Task<(bool Success, string Message, int ReturnId)> CreateAndCompleteReturnAsync(
            int invoiceId,
            string reason,
            List<(int InvoiceItemId, int ProductId, int Quantity, decimal RefundAmount)> items,
            int userId)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return (false, "Lý do trả hàng không được để trống.", 0);
            }

            if (items == null || items.Count == 0)
            {
                return (false, "Phải chọn ít nhất một sản phẩm cần trả lại.", 0);
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // BR.14: Tra cứu hóa đơn gốc đã Hoàn tất
                var invoice = await _context.Invoices
                    .Include(i => i.Items)
                    .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

                if (invoice == null)
                {
                    await tx.RollbackAsync();
                    return (false, "Không tìm thấy hóa đơn gốc.", 0);
                }

                if (invoice.Status != "Completed")
                {
                    await tx.RollbackAsync();
                    return (false, "Chỉ được lập phiếu trả cho hóa đơn đã hoàn tất.", 0);
                }

                var existingReturnedMap = await GetReturnedQuantitiesByInvoiceAsync(invoiceId);

                string returnCode = $"TH-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
                decimal totalRefund = 0m;

                var returnReceipt = new ReturnReceipt
                {
                    ReturnCode = returnCode,
                    InvoiceId = invoiceId,
                    Status = "Completed",
                    Reason = reason.Trim(),
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };

                foreach (var item in items)
                {
                    if (item.Quantity <= 0)
                    {
                        await tx.RollbackAsync();
                        return (false, "Số lượng trả phải lớn hơn 0.", 0);
                    }

                    var invoiceItem = invoice.Items.FirstOrDefault(ii => ii.ItemId == item.InvoiceItemId);
                    if (invoiceItem == null)
                    {
                        await tx.RollbackAsync();
                        return (false, "Mặt hàng trả không thuộc hóa đơn gốc.", 0);
                    }

                    // BR.14, EX04: Tổng lượng trả không vượt số lượng đã mua
                    int alreadyReturned = existingReturnedMap.ContainsKey(item.InvoiceItemId)
                        ? existingReturnedMap[item.InvoiceItemId]
                        : 0;

                    int remainingReturnable = invoiceItem.Quantity - alreadyReturned;
                    if (item.Quantity > remainingReturnable)
                    {
                        await tx.RollbackAsync();
                        return (false, $"Số lượng trả ({item.Quantity}) vượt quá số lượng còn được trả ({remainingReturnable}) của sản phẩm.", 0);
                    }

                    decimal refund = item.RefundAmount > 0 ? item.RefundAmount : item.Quantity * invoiceItem.UnitPrice;
                    totalRefund += refund;

                    returnReceipt.Items.Add(new ReturnReceiptItem
                    {
                        InvoiceItemId = item.InvoiceItemId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        RefundAmount = refund
                    });

                    // Tăng tồn kho và tạo giao dịch RETURN (BR.15, BR.16)
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        int oldStock = product.StockQuantity;
                        var invTx = InventoryTransactionFactory.CreateReturn(
                            product, item.Quantity, returnCode, returnReceipt.ReturnId, userId);
                        _context.InventoryTransactions.Add(invTx);

                        product.StockQuantity += item.Quantity;

                        await _eventPublisher.PublishStockChangedAsync(new StockChangedEvent
                        {
                            ProductId = product.ProductId,
                            OldStock = oldStock,
                            NewStock = product.StockQuantity,
                            Type = "RETURN",
                            SourceCode = returnCode,
                            PerformedById = userId
                        }, _context);
                    }
                }

                returnReceipt.RefundAmount = totalRefund;
                _context.ReturnReceipts.Add(returnReceipt);
                await _context.SaveChangesAsync();

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "RETURN_RECEIPT_COMPLETED",
                    EntityType = "ReturnReceipt",
                    EntityId = returnReceipt.ReturnId.ToString(),
                    Details = $"Hoàn tất phiếu trả hàng {returnReceipt.ReturnCode} theo HĐ {invoice.InvoiceCode}. Hoàn tiền: {returnReceipt.RefundAmount:N0} đ",
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return (true, $"Xử lý trả hàng thành công cho phiếu {returnReceipt.ReturnCode}!", returnReceipt.ReturnId);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, $"Lỗi xử lý trả hàng: {ex.Message}", 0);
            }
        }
    }
}
