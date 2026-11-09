# Hệ thống quản lý bán hàng và kho văn phòng phẩm Minh Anh

Website nội bộ hỗ trợ cửa hàng văn phòng phẩm quản lý sản phẩm, nhập kho, bán hàng, trả hàng, kiểm kê, tồn kho và báo cáo. Đây là đồ án môn **Thiết kế phần mềm** của Nhóm 02.

> README này hướng dẫn chạy dự án trên máy cá nhân. Tài khoản và dữ liệu khởi tạo chỉ dành cho demo/học tập; không sử dụng nguyên trạng trên môi trường thật.

## Công nghệ

- C# / ASP.NET Core MVC trên **.NET 10**
- Entity Framework Core 10
- SQL Server
- Razor Views, Bootstrap 5 và Bootstrap Icons

## Chức năng chính

- Đăng nhập và phân quyền: Admin, nhân viên kho, nhân viên bán hàng
- Quản lý sản phẩm, danh mục, đơn vị tính và nhà cung cấp
- Lập phiếu nhập, hóa đơn bán hàng và phiếu trả hàng
- Kiểm kê, điều chỉnh tồn, xem sổ kho và cảnh báo tồn thấp
- Báo cáo doanh thu, tồn kho, sản phẩm bán chạy và nhật ký thao tác

## Yêu cầu trước khi chạy

1. Windows 10/11.
2. Cài **.NET 10 SDK**.
3. Cài và khởi động **SQL Server**. Cấu hình mặc định dùng SQL Server cục bộ với Windows Authentication.
4. Kết nối Internet trong lần đầu khôi phục các gói NuGet.

## Tải mã nguồn

Sau khi dự án được đưa lên GitHub, clone repository về máy:

```bash
git clone <URL-repository>
cd <ten-repository>
```

Thay `<URL-repository>` bằng URL HTTPS hoặc SSH trên trang GitHub của repository.

## Cấu hình cơ sở dữ liệu

Chuỗi kết nối mặc định trong `Source Code/QuanLyKho/appsettings.json` là:

```text
Server=localhost;Database=QuanLyKhoDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

Ứng dụng gọi `DbInitializer` lúc khởi động để tạo cơ sở dữ liệu nếu cần và nạp dữ liệu demo khi database chưa có tài khoản. Nếu SQL Server dùng instance khác, hãy sửa `Server=localhost`, ví dụ:

```text
Server=localhost\SQLEXPRESS;Database=QuanLyKhoDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

Nếu dùng SQL Server Authentication, thay `Trusted_Connection=True` bằng thông tin đăng nhập phù hợp. Không commit mật khẩu hoặc thông tin bí mật lên GitHub; ưu tiên dùng User Secrets hoặc biến môi trường khi cần lưu thông tin nhạy cảm.

## Chạy trên Windows

### Cách 1: Dùng script

Tại thư mục gốc repository, nhấp đúp `CHAY_WEB.bat`. Script chạy website ở `http://localhost:5000` và mở trình duyệt.

### Cách 2: Dùng .NET CLI

Mở terminal tại thư mục gốc repository:

```powershell
cd "Source Code\QuanLyKho"
dotnet restore
dotnet build
dotnet run --urls "http://localhost:5000"
```

Mở trình duyệt tại <http://localhost:5000>. Dừng server bằng `Ctrl+C` trong cửa sổ terminal.

> Các package cần được tải từ NuGet trong lần restore đầu tiên. Nếu gặp `NU1301`, kiểm tra kết nối Internet, proxy hoặc quyền truy cập `https://api.nuget.org/v3/index.json`, sau đó chạy lại `dotnet restore`.

## Tài khoản demo

Các tài khoản được khởi tạo trong mã nguồn (`DbInitializer`) khi database còn trống:

| Vai trò | Tên đăng nhập | Mật khẩu |
|---|---|---|
| Admin | `admin` | `admin123` |
| Nhân viên kho | `kho01` | `kho123` |
| Nhân viên bán hàng | `banhang01` | `banhang123` |

**Lưu ý:** Mật khẩu này là thông tin công khai trong mã nguồn, chỉ để chạy thử. Hãy đổi hoặc loại bỏ tài khoản/mật khẩu demo trước khi triển khai hay sử dụng dữ liệu thật.

## Cấu trúc thư mục

```text
.
├── 1. PROJECT MANAGEMENT/              # Kế hoạch, backlog, sprint, rủi ro, họp
├── 2. CONTEXT AND SYSTEM REQUIREMENT/  # Chức năng, nghiệp vụ, truy vết
├── 3. SOFTWARE ARCHITECTURE & DESIGN/ # Kiến trúc, cơ sở dữ liệu, UML
├── 4. TESTING/                         # Test plan, test case, kết quả và báo cáo
├── 5. PROTOTYPE/                       # Prototype giao diện
├── Source Code/QuanLyKho/              # Mã nguồn ASP.NET Core MVC
├── 00_GIOI_THIEU.md                    # Thông tin tổng quan nhóm/dự án
├── 00_HUONG_DAN_SU_DUNG.md             # Thứ tự đọc bộ tài liệu
├── CHAY_WEB.bat                        # Script chạy nhanh trên Windows
└── README.md
```

Một số thư mục con của mã nguồn như `bin/`, `obj/` và `wwwroot/lib/` là tệp build/thư viện phụ thuộc; không cần chỉnh sửa để chạy các chức năng thông thường.

## Đọc bộ tài liệu dự án

Nếu muốn tìm hiểu yêu cầu và thiết kế trước khi sử dụng mã nguồn, nên đọc theo thứ tự:

1. [Giới thiệu dự án](00_GIOI_THIEU.md) và [Hướng dẫn sử dụng tài liệu](00_HUONG_DAN_SU_DUNG.md)
2. Project Plan, Milestone và Product Backlog trong `1. PROJECT MANAGEMENT`
3. Concept of Operation, Function List và Business Rule trong `2. CONTEXT AND SYSTEM REQUIREMENT`
4. Architecture Driver, Architecture Design, Database Analysis and Design và tài liệu UML trong `3. SOFTWARE ARCHITECTURE & DESIGN`
5. Test Plan, Test Case, Test Execute và các báo cáo trong `4. TESTING`

## Phạm vi và lưu ý

- Phiên bản đồ án tập trung vào một cửa hàng; không tích hợp ngân hàng/cổng thanh toán thật.
- Phương thức “Chuyển khoản” chỉ được ghi nhận trên hóa đơn, không xác minh giao dịch ngân hàng.
- Giá trị tồn trong báo cáo là số ước tính theo tồn hiện tại nhân giá nhập hiện hành.
- Website cần SQL Server đang chạy và cấu hình kết nối đúng; chỉ cài .NET SDK là chưa đủ.

## Bản quyền và mục đích sử dụng

Dự án được xây dựng cho mục đích học tập. Nếu sử dụng lại mã nguồn hoặc tài liệu, vui lòng ghi nhận nguồn và kiểm tra quyền sử dụng của các thư viện/phụ thuộc bên thứ ba.
