using Microsoft.EntityFrameworkCore;
using QuanLyKho.Models.Entities;

namespace QuanLyKho.Data
{
    public class QuanLyKhoDbContext : DbContext
    {
        public QuanLyKhoDbContext(DbContextOptions<QuanLyKhoDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Unit> Units => Set<Unit>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();
        public DbSet<PurchaseReceiptItem> PurchaseReceiptItems => Set<PurchaseReceiptItem>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
        public DbSet<ReturnReceipt> ReturnReceipts => Set<ReturnReceipt>();
        public DbSet<ReturnReceiptItem> ReturnReceiptItems => Set<ReturnReceiptItem>();
        public DbSet<StockCount> StockCounts => Set<StockCount>();
        public DbSet<StockCountItem> StockCountItems => Set<StockCountItem>();
        public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
        public DbSet<LowStockAlert> LowStockAlerts => Set<LowStockAlert>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.UserName).IsUnique();
            });

            // Product
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasIndex(p => p.ProductCode).IsUnique();
                entity.HasIndex(p => p.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");

                entity.HasOne(p => p.Category)
                      .WithMany(c => c.Products)
                      .HasForeignKey(p => p.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Unit)
                      .WithMany(u => u.Products)
                      .HasForeignKey(p => p.UnitId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // PurchaseReceipt
            modelBuilder.Entity<PurchaseReceipt>(entity =>
            {
                entity.HasIndex(r => r.ReceiptCode).IsUnique();

                entity.HasOne(r => r.Supplier)
                      .WithMany(s => s.PurchaseReceipts)
                      .HasForeignKey(r => r.SupplierId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.CreatedBy)
                      .WithMany(u => u.PurchaseReceipts)
                      .HasForeignKey(r => r.CreatedById)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PurchaseReceiptItem>(entity =>
            {
                entity.HasOne(i => i.PurchaseReceipt)
                      .WithMany(r => r.Items)
                      .HasForeignKey(i => i.ReceiptId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.Product)
                      .WithMany(p => p.PurchaseReceiptItems)
                      .HasForeignKey(i => i.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Invoice
            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasIndex(i => i.InvoiceCode).IsUnique();

                entity.HasOne(i => i.CreatedBy)
                      .WithMany(u => u.Invoices)
                      .HasForeignKey(i => i.CreatedById)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<InvoiceItem>(entity =>
            {
                entity.HasOne(i => i.Invoice)
                      .WithMany(inv => inv.Items)
                      .HasForeignKey(i => i.InvoiceId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.Product)
                      .WithMany(p => p.InvoiceItems)
                      .HasForeignKey(i => i.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ReturnReceipt
            modelBuilder.Entity<ReturnReceipt>(entity =>
            {
                entity.HasIndex(r => r.ReturnCode).IsUnique();

                entity.HasOne(r => r.Invoice)
                      .WithMany(i => i.ReturnReceipts)
                      .HasForeignKey(r => r.InvoiceId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.CreatedBy)
                      .WithMany(u => u.ReturnReceipts)
                      .HasForeignKey(r => r.CreatedById)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ReturnReceiptItem>(entity =>
            {
                entity.HasOne(i => i.ReturnReceipt)
                      .WithMany(r => r.Items)
                      .HasForeignKey(i => i.ReturnId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.InvoiceItem)
                      .WithMany(ii => ii.ReturnReceiptItems)
                      .HasForeignKey(i => i.InvoiceItemId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Product)
                      .WithMany(p => p.ReturnReceiptItems)
                      .HasForeignKey(i => i.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // StockCount
            modelBuilder.Entity<StockCount>(entity =>
            {
                entity.HasIndex(c => c.CountCode).IsUnique();

                entity.HasOne(c => c.CreatedBy)
                      .WithMany(u => u.CreatedStockCounts)
                      .HasForeignKey(c => c.CreatedById)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.ApprovedBy)
                      .WithMany(u => u.ApprovedStockCounts)
                      .HasForeignKey(c => c.ApprovedById)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<StockCountItem>(entity =>
            {
                entity.HasOne(i => i.StockCount)
                      .WithMany(c => c.Items)
                      .HasForeignKey(i => i.CountId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.Product)
                      .WithMany(p => p.StockCountItems)
                      .HasForeignKey(i => i.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // InventoryTransaction
            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.HasOne(t => t.Product)
                      .WithMany(p => p.InventoryTransactions)
                      .HasForeignKey(t => t.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.PerformedBy)
                      .WithMany(u => u.InventoryTransactions)
                      .HasForeignKey(t => t.PerformedById)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // LowStockAlert
            modelBuilder.Entity<LowStockAlert>(entity =>
            {
                entity.HasOne(a => a.Product)
                      .WithMany(p => p.LowStockAlerts)
                      .HasForeignKey(a => a.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // AuditLog
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasOne(a => a.User)
                      .WithMany(u => u.AuditLogs)
                      .HasForeignKey(a => a.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
