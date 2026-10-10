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
    public class InventoryService : IInventoryService
    {
        private readonly QuanLyKhoDbContext _context;
        private readonly IStockEventPublisher _eventPublisher;

        public InventoryService(QuanLyKhoDbContext context, IStockEventPublisher eventPublisher)
        {
            _context = context;
            _eventPublisher = eventPublisher;
        }

        public async Task<List<InventoryTransaction>> GetTransactionsAsync(int? productId = null, string? type = null, DateTime? from = null, DateTime? to = null)
        {
            var query = _context.InventoryTransactions
                .Include(t => t.Product)
                    .ThenInclude(p => p!.Unit)
                .Include(t => t.PerformedBy)
                .AsQueryable();

            if (productId.HasValue && productId.Value > 0)
            {
                query = query.Where(t => t.ProductId == productId.Value);
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(t => t.Type == type);
            }

            if (from.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                var toEndOfDay = to.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(t => t.CreatedAt <= toEndOfDay);
            }

            return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        }

        public async Task<List<LowStockAlert>> GetAlertsAsync(string? status = "Open")
        {
            var query = _context.LowStockAlerts
                .Include(a => a.Product)
                    .ThenInclude(p => p!.Unit)
                .Include(a => a.Product)
                    .ThenInclude(p => p!.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            return await query.OrderByDescending(a => a.OpenedAt).ToListAsync();
        }

        public async Task<List<StockCount>> GetStockCountsAsync()
        {
            return await _context.StockCounts
                .Include(c => c.CreatedBy)
                .Include(c => c.ApprovedBy)
                .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<StockCount?> GetStockCountByIdAsync(int id)
        {
            return await _context.StockCounts
                .Include(c => c.CreatedBy)
                .Include(c => c.ApprovedBy)
                .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.Unit)
                .FirstOrDefaultAsync(c => c.CountId == id);
        }

        public async Task<(bool Success, string Message, int CountId)> CreateStockCountAsync(
            string reason, List<(int ProductId, int CountedQuantity)> items, int userId)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return (false, "Lý do kiểm kê không được để trống.", 0);
            }

            if (items == null || items.Count == 0)
            {
                return (false, "Phiếu kiểm kê phải có ít nhất một sản phẩm.", 0);
            }

            string countCode = $"KK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
            bool requiresApproval = false;

            var stockCount = new StockCount
            {
                CountCode = countCode,
                Status = "Draft",
                Reason = reason.Trim(),
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var item in items)
            {
                if (item.CountedQuantity < 0)
                {
                    return (false, "Số lượng thực tế đếm được không thể âm.", 0);
                }

                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null) continue;

                int diff = item.CountedQuantity - product.StockQuantity;

                // BR.08: Chênh lệch lớn (|diff| >= 10) cần phê duyệt từ Admin
                if (Math.Abs(diff) >= 10)
                {
                    requiresApproval = true;
                }

                stockCount.Items.Add(new StockCountItem
                {
                    ProductId = item.ProductId,
                    SystemQuantity = product.StockQuantity,
                    CountedQuantity = item.CountedQuantity,
                    Difference = diff
                });
            }

            stockCount.RequiresApproval = requiresApproval;
            _context.StockCounts.Add(stockCount);
            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "STOCK_COUNT_CREATED",
                EntityType = "StockCount",
                EntityId = stockCount.CountId.ToString(),
                Details = $"Tạo phiếu kiểm kê {stockCount.CountCode}. Cần duyệt: {requiresApproval}",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return (true, "Tạo phiếu kiểm kê thành công.", stockCount.CountId);
        }

        public async Task<(bool Success, string Message)> CompleteStockCountAsync(int countId, int userId, bool isAdmin)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var count = await _context.StockCounts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.CountId == countId);

                if (count == null)
                {
                    return (false, "Không tìm thấy phiếu kiểm kê.");
                }

                var state = DocumentStateFactory.GetState(count.Status);
                if (!state.CanComplete)
                {
                    return (false, $"Phiếu kiểm kê ở trạng thái '{count.Status}' không thể hoàn tất.");
                }

                // BR.08: Chênh lệch vượt ngưỡng cấu hình cần Admin duyệt
                if (count.RequiresApproval && !isAdmin)
                {
                    return (false, "Phiếu kiểm kê này có chênh lệch tồn lớn vượt ngưỡng cho phép, bắt buộc phải do Admin phê duyệt.");
                }

                foreach (var item in count.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null) continue;

                    int oldStock = product.StockQuantity;
                    int diff = item.CountedQuantity - oldStock;

                    if (diff != 0)
                    {
                        var invTx = InventoryTransactionFactory.CreateAdjustment(
                            product, diff, count.CountCode, count.CountId, userId);
                        _context.InventoryTransactions.Add(invTx);

                        product.StockQuantity = item.CountedQuantity;

                        await _eventPublisher.PublishStockChangedAsync(new StockChangedEvent
                        {
                            ProductId = product.ProductId,
                            OldStock = oldStock,
                            NewStock = product.StockQuantity,
                            Type = "ADJUSTMENT",
                            SourceCode = count.CountCode,
                            PerformedById = userId
                        }, _context);
                    }
                }

                count.Status = "Completed";
                count.CompletedAt = DateTime.UtcNow;
                if (count.RequiresApproval)
                {
                    count.ApprovedById = userId;
                }

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "STOCK_COUNT_COMPLETED",
                    EntityType = "StockCount",
                    EntityId = count.CountId.ToString(),
                    Details = $"Hoàn tất và điều chỉnh tồn theo phiếu kiểm kê {count.CountCode}",
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return (true, $"Hoàn tất điều chỉnh tồn kho theo phiếu {count.CountCode} thành công!");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, $"Lỗi khi điều chỉnh tồn kho: {ex.Message}");
            }
        }
    }

    public class ReportService : IReportService
    {
        private readonly QuanLyKhoDbContext _context;

        public ReportService(QuanLyKhoDbContext context)
        {
            _context = context;
        }

        public async Task<RevenueReportDto> GetRevenueReportAsync(DateTime from, DateTime to)
        {
            var toEndOfDay = to.Date.AddDays(1).AddTicks(-1);

            // BR.17: Doanh thu thuần bằng tổng hóa đơn hoàn tất trừ tổng phiếu trả hoàn tất trong kỳ lọc
            var invoices = await _context.Invoices
                .Include(i => i.CreatedBy)
                .Where(i => i.Status == "Completed" && i.CreatedAt >= from && i.CreatedAt <= toEndOfDay)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var returns = await _context.ReturnReceipts
                .Include(r => r.CreatedBy)
                .Where(r => r.Status == "Completed" && r.CreatedAt >= from && r.CreatedAt <= toEndOfDay)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            decimal grossRevenue = invoices.Sum(i => i.TotalAmount);
            decimal totalRefund = returns.Sum(r => r.RefundAmount);
            decimal cash = invoices.Where(i => i.PaymentMethod == "TienMat").Sum(i => i.TotalAmount);
            decimal transfer = invoices.Where(i => i.PaymentMethod == "ChuyenKhoan").Sum(i => i.TotalAmount);

            return new RevenueReportDto
            {
                FromDate = from,
                ToDate = to,
                TotalInvoiceCount = invoices.Count,
                GrossRevenue = grossRevenue,
                TotalReturnCount = returns.Count,
                TotalRefundAmount = totalRefund,
                CashAmount = cash,
                BankTransferAmount = transfer,
                Invoices = invoices,
                Returns = returns
            };
        }

        public async Task<StockReportDto> GetStockReportAsync()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Unit)
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            // BR.19: Giá trị tồn kho được ước tính bằng tồn hiện tại nhân giá nhập hiện hành
            decimal totalValue = products.Sum(p => p.StockQuantity * p.PurchasePrice);
            int lowStockCount = products.Count(p => p.StockQuantity <= p.MinimumStock);

            return new StockReportDto
            {
                TotalProductCount = products.Count,
                TotalStockQuantity = products.Sum(p => p.StockQuantity),
                EstimatedTotalValue = totalValue,
                LowStockProductCount = lowStockCount,
                Products = products
            };
        }

        public async Task<List<BestSellerDto>> GetBestSellersAsync(DateTime from, DateTime to, int top = 10)
        {
            var toEndOfDay = to.Date.AddDays(1).AddTicks(-1);

            // Số lượng bán từ hóa đơn hoàn tất
            var sales = await _context.InvoiceItems
                .Where(ii => ii.Invoice!.Status == "Completed" && ii.Invoice.CreatedAt >= from && ii.Invoice.CreatedAt <= toEndOfDay)
                .GroupBy(ii => new { ii.ProductId, ii.Product!.ProductCode, ii.Product.Name, CategoryName = ii.Product.Category!.Name, UnitName = ii.Product.Unit!.Name })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.ProductCode,
                    g.Key.Name,
                    g.Key.CategoryName,
                    g.Key.UnitName,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.Subtotal)
                })
                .ToListAsync();

            // Số lượng trả từ phiếu trả hoàn tất
            var returns = await _context.ReturnReceiptItems
                .Where(ri => ri.ReturnReceipt!.Status == "Completed" && ri.ReturnReceipt.CreatedAt >= from && ri.ReturnReceipt.CreatedAt <= toEndOfDay)
                .GroupBy(ri => ri.ProductId)
                .Select(g => new { ProductId = g.Key, QuantityReturned = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.QuantityReturned);

            var list = sales.Select(s =>
            {
                int returned = returns.ContainsKey(s.ProductId) ? returns[s.ProductId] : 0;
                return new BestSellerDto
                {
                    ProductId = s.ProductId,
                    ProductCode = s.ProductCode,
                    ProductName = s.Name,
                    CategoryName = s.CategoryName,
                    UnitName = s.UnitName,
                    QuantitySold = s.QuantitySold,
                    QuantityReturned = returned,
                    TotalRevenue = s.Revenue
                };
            })
            .OrderByDescending(x => x.NetQuantitySold)
            .Take(top)
            .ToList();

            return list;
        }

        public async Task<List<AuditLog>> GetAuditLogsAsync(int? userId = null, string? entityType = null, DateTime? from = null, DateTime? to = null)
        {
            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            if (userId.HasValue && userId.Value > 0)
            {
                query = query.Where(a => a.UserId == userId.Value);
            }

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                query = query.Where(a => a.EntityType == entityType);
            }

            if (from.HasValue)
            {
                query = query.Where(a => a.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                var toEndOfDay = to.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(a => a.CreatedAt <= toEndOfDay);
            }

            return await query.OrderByDescending(a => a.CreatedAt).Take(200).ToListAsync();
        }
    }
}
