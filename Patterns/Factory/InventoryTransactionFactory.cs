using System;
using QuanLyKho.Models.Entities;

namespace QuanLyKho.Patterns.Factory
{
    public static class InventoryTransactionFactory
    {
        public static InventoryTransaction CreateImport(Product product, int quantity, string receiptCode, int receiptId, int userId)
        {
            int oldStock = product.StockQuantity;
            int newStock = oldStock + quantity;

            return new InventoryTransaction
            {
                ProductId = product.ProductId,
                Type = "IMPORT",
                QuantityDelta = quantity,
                StockBefore = oldStock,
                StockAfter = newStock,
                SourceType = "PurchaseReceipt",
                SourceId = receiptId,
                SourceCode = receiptCode,
                PerformedById = userId,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static InventoryTransaction CreateSale(Product product, int quantity, string invoiceCode, int invoiceId, int userId)
        {
            int oldStock = product.StockQuantity;
            int newStock = oldStock - quantity;

            return new InventoryTransaction
            {
                ProductId = product.ProductId,
                Type = "SALE",
                QuantityDelta = -quantity,
                StockBefore = oldStock,
                StockAfter = newStock,
                SourceType = "Invoice",
                SourceId = invoiceId,
                SourceCode = invoiceCode,
                PerformedById = userId,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static InventoryTransaction CreateReturn(Product product, int quantity, string returnCode, int returnId, int userId)
        {
            int oldStock = product.StockQuantity;
            int newStock = oldStock + quantity;

            return new InventoryTransaction
            {
                ProductId = product.ProductId,
                Type = "RETURN",
                QuantityDelta = quantity,
                StockBefore = oldStock,
                StockAfter = newStock,
                SourceType = "ReturnReceipt",
                SourceId = returnId,
                SourceCode = returnCode,
                PerformedById = userId,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static InventoryTransaction CreateAdjustment(Product product, int difference, string countCode, int countId, int userId)
        {
            int oldStock = product.StockQuantity;
            int newStock = oldStock + difference;

            return new InventoryTransaction
            {
                ProductId = product.ProductId,
                Type = "ADJUSTMENT",
                QuantityDelta = difference,
                StockBefore = oldStock,
                StockAfter = newStock,
                SourceType = "StockCount",
                SourceId = countId,
                SourceCode = countCode,
                PerformedById = userId,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
