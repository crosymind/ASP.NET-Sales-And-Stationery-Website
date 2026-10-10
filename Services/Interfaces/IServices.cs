using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyKho.Models.Entities;

namespace QuanLyKho.Services.Interfaces
{
    public interface IUserService
    {
        Task<User?> AuthenticateAsync(string username, string password);
        Task<List<User>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(int id);
        Task<(bool Success, string Message)> SaveUserAsync(int? userId, string username, string? password, string fullName, string role, bool isActive, int performerId);
        Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
    }

    public interface IProductService
    {
        Task<List<Product>> GetAllAsync(string? search = null, int? categoryId = null, bool activeOnly = true);
        Task<Product?> GetByIdAsync(int id);
        Task<Product?> GetByCodeOrBarcodeAsync(string code);
        Task<(bool Success, string Message)> CreateAsync(Product product, int userId);
        Task<(bool Success, string Message)> UpdateAsync(Product product, int userId);
        Task<(bool Success, string Message)> ToggleStatusAsync(int id, int userId);
        Task<List<Category>> GetCategoriesAsync(bool activeOnly = false);
        Task<(bool Success, string Message)> SaveCategoryAsync(Category category, int userId);
        Task<List<Unit>> GetUnitsAsync(bool activeOnly = false);
        Task<(bool Success, string Message)> SaveUnitAsync(Unit unit, int userId);
    }

    public interface ISupplierService
    {
        Task<List<Supplier>> GetAllAsync(string? search = null, bool activeOnly = false);
        Task<Supplier?> GetByIdAsync(int id);
        Task<(bool Success, string Message)> SaveAsync(Supplier supplier, int userId);
        Task<(bool Success, string Message)> ToggleStatusAsync(int id, int userId);
    }

    public interface IPurchaseReceiptService
    {
        Task<List<PurchaseReceipt>> GetAllAsync(string? status = null, DateTime? from = null, DateTime? to = null);
        Task<PurchaseReceipt?> GetByIdAsync(int id);
        Task<(bool Success, string Message, int ReceiptId)> CreateReceiptAsync(int supplierId, string? notes, List<(int ProductId, int Quantity, decimal UnitCost)> items, int userId);
        Task<(bool Success, string Message)> CompleteReceiptAsync(int receiptId, int userId);
        Task<(bool Success, string Message)> CancelReceiptAsync(int receiptId, int userId);
    }

    public interface IInvoiceService
    {
        Task<List<Invoice>> GetAllAsync(string? search = null, string? status = null, DateTime? from = null, DateTime? to = null);
        Task<Invoice?> GetByIdAsync(int id);
        Task<(bool Success, string Message, int InvoiceId)> CreateAndCompleteInvoiceAsync(string? customerName, string? customerPhone, string discountType, decimal discountValue, string paymentMethod, List<(int ProductId, int Quantity)> items, int userId);
        Task<(bool Success, string Message)> CancelInvoiceAsync(int invoiceId, int userId);
    }

    public interface IReturnReceiptService
    {
        Task<List<ReturnReceipt>> GetAllAsync(DateTime? from = null, DateTime? to = null);
        Task<ReturnReceipt?> GetByIdAsync(int id);
        Task<Dictionary<int, int>> GetReturnedQuantitiesByInvoiceAsync(int invoiceId);
        Task<(bool Success, string Message, int ReturnId)> CreateAndCompleteReturnAsync(int invoiceId, string reason, List<(int InvoiceItemId, int ProductId, int Quantity, decimal RefundAmount)> items, int userId);
    }

    public interface IInventoryService
    {
        Task<List<InventoryTransaction>> GetTransactionsAsync(int? productId = null, string? type = null, DateTime? from = null, DateTime? to = null);
        Task<List<LowStockAlert>> GetAlertsAsync(string? status = "Open");
        Task<List<StockCount>> GetStockCountsAsync();
        Task<StockCount?> GetStockCountByIdAsync(int id);
        Task<(bool Success, string Message, int CountId)> CreateStockCountAsync(string reason, List<(int ProductId, int CountedQuantity)> items, int userId);
        Task<(bool Success, string Message)> CompleteStockCountAsync(int countId, int userId, bool isAdmin);
    }

    public class RevenueReportDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalInvoiceCount { get; set; }
        public decimal GrossRevenue { get; set; }
        public int TotalReturnCount { get; set; }
        public decimal TotalRefundAmount { get; set; }
        public decimal NetRevenue => GrossRevenue - TotalRefundAmount;
        public decimal CashAmount { get; set; }
        public decimal BankTransferAmount { get; set; }
        public List<Invoice> Invoices { get; set; } = new();
        public List<ReturnReceipt> Returns { get; set; } = new();
    }

    public class StockReportDto
    {
        public int TotalProductCount { get; set; }
        public int TotalStockQuantity { get; set; }
        public decimal EstimatedTotalValue { get; set; } // BR.19
        public int LowStockProductCount { get; set; }
        public List<Product> Products { get; set; } = new();
    }

    public class BestSellerDto
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public int QuantityReturned { get; set; }
        public int NetQuantitySold => QuantitySold - QuantityReturned;
        public decimal TotalRevenue { get; set; }
    }

    public interface IReportService
    {
        Task<RevenueReportDto> GetRevenueReportAsync(DateTime from, DateTime to);
        Task<StockReportDto> GetStockReportAsync();
        Task<List<BestSellerDto>> GetBestSellersAsync(DateTime from, DateTime to, int top = 10);
        Task<List<AuditLog>> GetAuditLogsAsync(int? userId = null, string? entityType = null, DateTime? from = null, DateTime? to = null);
    }
}
