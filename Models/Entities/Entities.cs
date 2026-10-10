using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyKho.Models.Entities
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = "BanHang"; // Admin, Kho, BanHang

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<PurchaseReceipt> PurchaseReceipts { get; set; } = new List<PurchaseReceipt>();
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public ICollection<ReturnReceipt> ReturnReceipts { get; set; } = new List<ReturnReceipt>();
        public ICollection<StockCount> CreatedStockCounts { get; set; } = new List<StockCount>();
        public ICollection<StockCount> ApprovedStockCounts { get; set; } = new List<StockCount>();
        public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }

    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    public class Unit
    {
        [Key]
        public int UnitId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required]
        [MaxLength(50)]
        public string ProductCode { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Barcode { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public int UnitId { get; set; }
        public Unit? Unit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SalePrice { get; set; }

        public int StockQuantity { get; set; } = 0;

        public int MinimumStock { get; set; } = 5;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // Navigation
        public ICollection<PurchaseReceiptItem> PurchaseReceiptItems { get; set; } = new List<PurchaseReceiptItem>();
        public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
        public ICollection<ReturnReceiptItem> ReturnReceiptItems { get; set; } = new List<ReturnReceiptItem>();
        public ICollection<StockCountItem> StockCountItems { get; set; } = new List<StockCountItem>();
        public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
        public ICollection<LowStockAlert> LowStockAlerts { get; set; } = new List<LowStockAlert>();
    }

    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PurchaseReceipt> PurchaseReceipts { get; set; } = new List<PurchaseReceipt>();
    }

    public class PurchaseReceipt
    {
        [Key]
        public int ReceiptId { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReceiptCode { get; set; } = string.Empty;

        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Draft"; // Draft, Completed, Cancelled

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public int CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public ICollection<PurchaseReceiptItem> Items { get; set; } = new List<PurchaseReceiptItem>();
    }

    public class PurchaseReceiptItem
    {
        [Key]
        public int ItemId { get; set; }

        public int ReceiptId { get; set; }
        public PurchaseReceipt? PurchaseReceipt { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }
    }

    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }

        [Required]
        [MaxLength(50)]
        public string InvoiceCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Draft"; // Draft, Completed, Cancelled

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [MaxLength(20)]
        public string DiscountType { get; set; } = "None"; // None, Percentage, Fixed

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [MaxLength(20)]
        public string PaymentMethod { get; set; } = "TienMat"; // TienMat, ChuyenKhoan

        [MaxLength(100)]
        public string? CustomerName { get; set; }

        [MaxLength(20)]
        public string? CustomerPhone { get; set; }

        public int CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
        public ICollection<ReturnReceipt> ReturnReceipts { get; set; } = new List<ReturnReceipt>();
    }

    public class InvoiceItem
    {
        [Key]
        public int ItemId { get; set; }

        public int InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        public ICollection<ReturnReceiptItem> ReturnReceiptItems { get; set; } = new List<ReturnReceiptItem>();
    }

    public class ReturnReceipt
    {
        [Key]
        public int ReturnId { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReturnCode { get; set; } = string.Empty;

        public int InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Draft"; // Draft, Completed, Cancelled

        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; }

        [Required]
        [MaxLength(255)]
        public string Reason { get; set; } = string.Empty;

        public int CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public ICollection<ReturnReceiptItem> Items { get; set; } = new List<ReturnReceiptItem>();
    }

    public class ReturnReceiptItem
    {
        [Key]
        public int ItemId { get; set; }

        public int ReturnId { get; set; }
        public ReturnReceipt? ReturnReceipt { get; set; }

        public int InvoiceItemId { get; set; }
        public InvoiceItem? InvoiceItem { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; }
    }

    public class StockCount
    {
        [Key]
        public int CountId { get; set; }

        [Required]
        [MaxLength(50)]
        public string CountCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Draft"; // Draft, Completed, Cancelled

        [Required]
        [MaxLength(255)]
        public string Reason { get; set; } = string.Empty;

        public bool RequiresApproval { get; set; } = false;

        public int CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public int? ApprovedById { get; set; }
        public User? ApprovedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public ICollection<StockCountItem> Items { get; set; } = new List<StockCountItem>();
    }

    public class StockCountItem
    {
        [Key]
        public int ItemId { get; set; }

        public int CountId { get; set; }
        public StockCount? StockCount { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int SystemQuantity { get; set; }

        public int CountedQuantity { get; set; }

        public int Difference { get; set; } // CountedQuantity - SystemQuantity
    }

    public class InventoryTransaction
    {
        [Key]
        public int TransactionId { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = string.Empty; // IMPORT, SALE, RETURN, ADJUSTMENT

        public int QuantityDelta { get; set; }

        public int StockBefore { get; set; }

        public int StockAfter { get; set; }

        [Required]
        [MaxLength(50)]
        public string SourceType { get; set; } = string.Empty; // PurchaseReceipt, Invoice, ReturnReceipt, StockCount

        public int SourceId { get; set; }

        [MaxLength(50)]
        public string SourceCode { get; set; } = string.Empty;

        public int PerformedById { get; set; }
        public User? PerformedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class LowStockAlert
    {
        [Key]
        public int AlertId { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Open"; // Open, Closed

        public int CurrentStock { get; set; }

        public int MinimumStock { get; set; }

        public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ClosedAt { get; set; }
    }

    public class AuditLog
    {
        [Key]
        public int AuditLogId { get; set; }

        public int? UserId { get; set; }
        public User? User { get; set; }

        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string EntityType { get; set; } = string.Empty;

        [MaxLength(50)]
        public string EntityId { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Details { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
