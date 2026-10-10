using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyKho.Common;
using QuanLyKho.Models.Entities;

namespace QuanLyKho.Data
{
    public static class DbInitializer
    {
        public static void Initialize(QuanLyKhoDbContext context)
        {
            context.Database.EnsureCreated();

            if (context.Users.Any())
            {
                return; // Database has been seeded
            }

            // 1. Seed Users
            var adminUser = new User
            {
                UserName = "admin",
                PasswordHash = SecurityHelper.HashPassword("admin123"),
                FullName = "Chủ cửa hàng (Admin)",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var warehouseUser = new User
            {
                UserName = "kho01",
                PasswordHash = SecurityHelper.HashPassword("kho123"),
                FullName = "Nguyễn Văn Kho",
                Role = "Kho",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var salesUser = new User
            {
                UserName = "banhang01",
                PasswordHash = SecurityHelper.HashPassword("banhang123"),
                FullName = "Trần Thị Bán Hàng",
                Role = "BanHang",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.AddRange(adminUser, warehouseUser, salesUser);
            context.SaveChanges();

            // 2. Seed Categories
            var catPen = new Category { Name = "Bút viết & Mực", Description = "Các loại bút bi, gel, chì, dạ quang", IsActive = true };
            var catNotebook = new Category { Name = "Vở & Sổ tay", Description = "Vở học sinh, sổ lò xo, sổ da", IsActive = true };
            var catPaper = new Category { Name = "Giấy in & File hồ sơ", Description = "Giấy photocopy A4/A3, bìa còng, túi clear bag", IsActive = true };
            var catTools = new Category { Name = "Dụng cụ văn phòng", Description = "Kéo, thước kẻ, bấm kim, băng keo", IsActive = true };
            var catTech = new Category { Name = "Thiết bị & Máy tính", Description = "Máy tính bỏ túi khoa học Casio, máy hủy tài liệu", IsActive = true };

            context.Categories.AddRange(catPen, catNotebook, catPaper, catTools, catTech);
            context.SaveChanges();

            // 3. Seed Units
            var unitCay = new Unit { Name = "Cây", IsActive = true };
            var unitQuyen = new Unit { Name = "Quyển", IsActive = true };
            var unitRam = new Unit { Name = "Ram", IsActive = true };
            var unitCai = new Unit { Name = "Cái", IsActive = true };
            var unitHop = new Unit { Name = "Hộp", IsActive = true };
            var unitCuon = new Unit { Name = "Cuộn", IsActive = true };

            context.Units.AddRange(unitCay, unitQuyen, unitRam, unitCai, unitHop, unitCuon);
            context.SaveChanges();

            // 4. Seed Suppliers
            var supThienLong = new Supplier
            {
                Name = "Công ty CP Tập đoàn Thiên Long",
                Phone = "02837505555",
                Email = "sales@thienlong.vn",
                Address = "Tầng 10, Sofic Tower, Số 10 Mai Chí Thọ, TP. Thủ Đức, TP.HCM",
                IsActive = true
            };

            var supDoubleA = new Supplier
            {
                Name = "Công ty TNHH Giấy Double A Việt Nam",
                Phone = "02839100000",
                Email = "contact@doublea.com.vn",
                Address = "Tòa nhà Sailing Tower, 111A Pasteur, Quận 1, TP.HCM",
                IsActive = true
            };

            var supDeli = new Supplier
            {
                Name = "Công ty CP Dụng cụ Văn phòng Deli Việt Nam",
                Phone = "02438221122",
                Email = "cskh@delivietnam.com",
                Address = "Số 15 Liễu Giai, Ba Đình, Hà Nội",
                IsActive = true
            };

            context.Suppliers.AddRange(supThienLong, supDoubleA, supDeli);
            context.SaveChanges();

            // 5. Seed Products
            var products = new[]
            {
                new Product
                {
                    ProductCode = "SP001",
                    Barcode = "893500100101",
                    Name = "Bút bi Thiên Long TL-027 0.5mm",
                    CategoryId = catPen.CategoryId,
                    UnitId = unitCay.UnitId,
                    PurchasePrice = 3500m,
                    SalePrice = 5000m,
                    StockQuantity = 150,
                    MinimumStock = 20,
                    IsActive = true
                },
                new Product
                {
                    ProductCode = "SP002",
                    Barcode = "893500100102",
                    Name = "Bút gel Pilot G2 0.5mm Đen",
                    CategoryId = catPen.CategoryId,
                    UnitId = unitCay.UnitId,
                    PurchasePrice = 22000m,
                    SalePrice = 32000m,
                    StockQuantity = 45,
                    MinimumStock = 10,
                    IsActive = true
                },
                new Product
                {
                    ProductCode = "SP003",
                    Barcode = "893500100201",
                    Name = "Vở kẻ ngang Campus Adventure 200 trang",
                    CategoryId = catNotebook.CategoryId,
                    UnitId = unitQuyen.UnitId,
                    PurchasePrice = 14000m,
                    SalePrice = 20000m,
                    StockQuantity = 80,
                    MinimumStock = 15,
                    IsActive = true
                },
                new Product
                {
                    ProductCode = "SP004",
                    Barcode = "893500100301",
                    Name = "Giấy in A4 Double A 70gsm (Ram 500 tờ)",
                    CategoryId = catPaper.CategoryId,
                    UnitId = unitRam.UnitId,
                    PurchasePrice = 68000m,
                    SalePrice = 85000m,
                    StockQuantity = 60,
                    MinimumStock = 10,
                    IsActive = true
                },
                new Product
                {
                    ProductCode = "SP005",
                    Barcode = "893500100302",
                    Name = "Bìa còng KingJim 7cm khổ A4",
                    CategoryId = catPaper.CategoryId,
                    UnitId = unitCai.UnitId,
                    PurchasePrice = 45000m,
                    SalePrice = 62000m,
                    StockQuantity = 25,
                    MinimumStock = 5,
                    IsActive = true
                },
                new Product
                {
                    ProductCode = "SP006",
                    Barcode = "893500100401",
                    Name = "Bấm kim Plus No.10 PS-10E",
                    CategoryId = catTools.CategoryId,
                    UnitId = unitCai.UnitId,
                    PurchasePrice = 28000m,
                    SalePrice = 40000m,
                    StockQuantity = 30,
                    MinimumStock = 5,
                    IsActive = true
                },
                new Product
                {
                    ProductCode = "SP007",
                    Barcode = "893500100402",
                    Name = "Kim bấm số 10 Plus (Hộp 1000 kim)",
                    CategoryId = catTools.CategoryId,
                    UnitId = unitHop.UnitId,
                    PurchasePrice = 3000m,
                    SalePrice = 6000m,
                    StockQuantity = 100,
                    MinimumStock = 20,
                    IsActive = true
                },
                new Product
                {
                    ProductCode = "SP008",
                    Barcode = "893500100403",
                    Name = "Băng keo trong văn phòng 5cm x 100y",
                    CategoryId = catTools.CategoryId,
                    UnitId = unitCuon.UnitId,
                    PurchasePrice = 12000m,
                    SalePrice = 18000m,
                    StockQuantity = 50,
                    MinimumStock = 10,
                    IsActive = true
                },
                new Product
                {
                    ProductCode = "SP009",
                    Barcode = "893500100501",
                    Name = "Máy tính khoa học Casio FX-580VN X",
                    CategoryId = catTech.CategoryId,
                    UnitId = unitCai.UnitId,
                    PurchasePrice = 550000m,
                    SalePrice = 680000m,
                    StockQuantity = 12,
                    MinimumStock = 3,
                    IsActive = true
                },
                new Product
                {
                    ProductCode = "SP010",
                    Barcode = "893500100404",
                    Name = "Kéo văn phòng Deli 175mm thép không gỉ",
                    CategoryId = catTools.CategoryId,
                    UnitId = unitCai.UnitId,
                    PurchasePrice = 15000m,
                    SalePrice = 25000m,
                    StockQuantity = 3, // Dưới ngưỡng tối thiểu (5) để kích hoạt cảnh báo tồn thấp!
                    MinimumStock = 5,
                    IsActive = true
                }
            };

            context.Products.AddRange(products);
            context.SaveChanges();

            // 6. Record Initial Inventory Transactions & Low Stock Alerts
            foreach (var p in products)
            {
                context.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = p.ProductId,
                    Type = "IMPORT",
                    QuantityDelta = p.StockQuantity,
                    StockBefore = 0,
                    StockAfter = p.StockQuantity,
                    SourceType = "InitStock",
                    SourceId = 1,
                    SourceCode = "INIT-KHO-01",
                    PerformedById = warehouseUser.UserId,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                });

                if (p.StockQuantity <= p.MinimumStock)
                {
                    context.LowStockAlerts.Add(new LowStockAlert
                    {
                        ProductId = p.ProductId,
                        Status = "Open",
                        CurrentStock = p.StockQuantity,
                        MinimumStock = p.MinimumStock,
                        OpenedAt = DateTime.UtcNow
                    });
                }
            }

            // 7. Seed Initial Audit Log
            context.AuditLogs.Add(new AuditLog
            {
                UserId = adminUser.UserId,
                Action = "SYSTEM_INITIALIZED",
                EntityType = "System",
                EntityId = "1",
                Details = "Khởi tạo hệ thống Quản lý Bán hàng & Văn phòng phẩm Minh Anh thành công.",
                CreatedAt = DateTime.UtcNow
            });

            context.SaveChanges();
        }
    }
}
