using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.Models.Entities;

namespace QuanLyKho.Patterns.Observer
{
    public class StockChangedEvent
    {
        public int ProductId { get; set; }
        public int OldStock { get; set; }
        public int NewStock { get; set; }
        public string Type { get; set; } = string.Empty; // IMPORT, SALE, RETURN, ADJUSTMENT
        public string SourceCode { get; set; } = string.Empty;
        public int PerformedById { get; set; }
    }

    public interface IStockChangedObserver
    {
        Task OnStockChangedAsync(StockChangedEvent e, QuanLyKhoDbContext context);
    }

    public class LowStockObserver : IStockChangedObserver
    {
        public async Task OnStockChangedAsync(StockChangedEvent e, QuanLyKhoDbContext context)
        {
            var product = await context.Products.FindAsync(e.ProductId);
            if (product == null) return;

            // BR.09: Khi tồn <= ngưỡng tối thiểu, mỗi sản phẩm chỉ có một cảnh báo mở; cảnh báo đóng khi tồn vượt ngưỡng.
            var openAlert = await context.LowStockAlerts
                .FirstOrDefaultAsync(a => a.ProductId == e.ProductId && a.Status == "Open");

            if (product.StockQuantity <= product.MinimumStock)
            {
                if (openAlert == null)
                {
                    context.LowStockAlerts.Add(new LowStockAlert
                    {
                        ProductId = e.ProductId,
                        Status = "Open",
                        CurrentStock = product.StockQuantity,
                        MinimumStock = product.MinimumStock,
                        OpenedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    openAlert.CurrentStock = product.StockQuantity;
                }
            }
            else
            {
                if (openAlert != null)
                {
                    openAlert.Status = "Closed";
                    openAlert.ClosedAt = DateTime.UtcNow;
                    openAlert.CurrentStock = product.StockQuantity;
                }
            }
        }
    }

    public class AuditLogObserver : IStockChangedObserver
    {
        public Task OnStockChangedAsync(StockChangedEvent e, QuanLyKhoDbContext context)
        {
            context.AuditLogs.Add(new AuditLog
            {
                UserId = e.PerformedById,
                Action = $"STOCK_CHANGE_{e.Type}",
                EntityType = "Product",
                EntityId = e.ProductId.ToString(),
                Details = $"Biến động tồn từ {e.OldStock} -> {e.NewStock} (delta: {e.NewStock - e.OldStock}). Chứng từ: {e.SourceCode}",
                CreatedAt = DateTime.UtcNow
            });
            return Task.CompletedTask;
        }
    }

    public interface IStockEventPublisher
    {
        Task PublishStockChangedAsync(StockChangedEvent e, QuanLyKhoDbContext context);
    }

    public class StockEventPublisher : IStockEventPublisher
    {
        private readonly IEnumerable<IStockChangedObserver> _observers;

        public StockEventPublisher(IEnumerable<IStockChangedObserver> observers)
        {
            _observers = observers;
        }

        public async Task PublishStockChangedAsync(StockChangedEvent e, QuanLyKhoDbContext context)
        {
            foreach (var observer in _observers)
            {
                await observer.OnStockChangedAsync(e, context);
            }
        }
    }
}
