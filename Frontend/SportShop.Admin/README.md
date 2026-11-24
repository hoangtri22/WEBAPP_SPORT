# Web Admin

Web quản trị cho Quản lý và Nhân viên (ASP.NET Core MVC, .NET 10). Chuẩn bị CSDL và các bước chạy chi tiết: [README tổng](../../README.md).

## Chức năng

| Menu | Làm được gì |
|---|---|
| Tổng quan | Doanh thu, số đơn hôm nay, phản hồi chưa trả lời, lịch hẹn, đơn gần nhất |
| Tài khoản & Phân quyền | Tạo, sửa, khóa, xóa mềm, khôi phục tài khoản |
| Quản lý nhân viên | Hồ sơ nhân viên, cho nghỉ việc, khôi phục |
| Quản lý khách hàng | Hồ sơ, nhóm khách theo tiêu chí, ghi chú chăm sóc, khóa, xóa mềm, khôi phục |
| Quản lý loại sản phẩm, sản phẩm | Loại, sản phẩm, biến thể, ảnh; nhập và xuất Excel |
| Quản lý nhà cung cấp, nhập hàng | Nhà cung cấp; phiếu nhập cập nhật tồn kho và giá vốn |
| Khuyến mãi & Marketing | Khuyến mãi theo sản phẩm, đơn hàng, voucher; chiến dịch thông báo |
| Quản lý đơn đặt hàng và giao hàng | Tạo đơn tại quầy, cập nhật trạng thái, thanh toán, vận chuyển, hủy đơn |
| Quản lý hóa đơn | Danh sách kèm khung chi tiết, xuất Excel |
| Phản hồi khách hàng, Khảo sát | Trả lời, ẩn hiện phản hồi; tạo, gửi, đóng và xem kết quả khảo sát |
| Thống kê doanh thu | Thu, chi, lợi nhuận theo kỳ, nhân viên, khách, sản phẩm; xuất Excel, PDF |
| Báo cáo thống kê | Chỉ số theo khoảng ngày; tạo, lưu và tải báo cáo Excel, PDF |

**Phân quyền:**
- Chỉ nhân viên và quản lý đăng nhập được, tài khoản khách bị chặn.
- Chỉ Quản lý được: vào trang Quản lý nhân viên; thêm, sửa, xóa tài khoản và nhân viên; khóa, khôi phục tài khoản; khôi phục nhân viên; xem mật khẩu khách.

**Quy ước khi sửa code:**
- Dữ liệu đọc qua `ShopData`, mọi thao tác ghi đi qua `ShopCommands.Run()` trong một transaction rồi xóa `DataCache`.
- Form thêm, sửa dựng từ khai báo trong `CrudDefs.cs`; thêm trường thì sửa ở đây và phần lưu tương ứng trong `ShopCommands.cs`.
- Xóa khách, tài khoản, nhân viên, nhà cung cấp, khuyến mãi là xóa mềm (đổi trạng thái).
- Phiên đăng nhập hết hạn sau 8 giờ. Mỗi tab trình duyệt giữ một tài khoản riêng.

## Cấu hình

Đọc lần lượt `appsettings.json`, `appsettings.Development.json`, `appsettings.Local.json` (git bỏ qua) rồi biến môi trường; nguồn sau ghi đè nguồn trước.

| Khóa | Ý nghĩa | Mặc định |
|---|---|---|
| `ConnectionStrings:Default` | Kết nối MySQL, bắt buộc | – |
| `Paths:Images` | Thư mục ảnh sản phẩm | `../../Backend/Images` |
| `Paths:Uploads` | Thư mục ảnh tải lên, báo cáo và file báo web khách nạp lại danh mục | `../Uploads` |

## Cách chạy

```powershell
dotnet run --project Frontend/SportShop.Admin --launch-profile http
```

Chạy tại thư mục `WEBAPP_SPORT`, rồi mở http://localhost:5188. Profile `https` dùng thêm cổng 7231.

## Cây thư mục

```text
SportShop.Admin/
├── Program.cs                  # Khởi tạo, đăng nhập cookie, trang báo lỗi CSDL, ảnh tĩnh
├── appsettings.json            # Cấu hình mặc định
├── Properties/launchSettings.json # Cổng chạy
├── Controllers/                # Một controller cho mỗi nhóm menu
│   ├── AccountController.cs    # Đăng nhập, đăng xuất, tài khoản
│   ├── CrudController.cs       # Mở form, lưu, xóa, thao tác riêng của mọi đối tượng
│   ├── ReportsController.cs    # Báo cáo thống kê, tạo và tải báo cáo
│   ├── StatsController.cs      # Thống kê doanh thu, xuất Excel và PDF
│   └── LayoutDataFilter.cs     # Dữ liệu chung của khung trang
├── Models/
│   ├── ShopData.cs             # Đọc dữ liệu từ MySQL
│   ├── ShopCommands.cs         # Ghi dữ liệu và quy tắc nghiệp vụ
│   ├── CrudDefs.cs             # Khai báo trường của các form
│   ├── DataCache.cs            # Bộ nhớ đệm 2 hoặc 10 phút
│   ├── Domain.cs, ViewModels.cs, TablePage.cs # Kiểu dữ liệu và bảng hiển thị
│   ├── ReportStore.cs          # Lưu các báo cáo đã tạo
│   ├── StatsPdf.cs             # Dựng PDF doanh thu
│   ├── Catalog.cs, MemberTier.cs # Quy tắc loại lưu trữ, voucher, hạng thành viên
│   └── TabCookies.cs           # Mỗi tab một phiên đăng nhập
├── Views/
│   ├── Shared/                 # Khung trang, bảng dữ liệu dùng chung, trang lỗi
│   ├── Crud/_Form.cshtml       # Form dùng chung cho thêm, sửa
│   └── …                       # Trang riêng của từng controller
└── wwwroot/
    ├── css/admin.css           # Giao diện
    └── js/
        ├── admin.js            # Form, bảng, khung xem nhanh, thông báo
        ├── search-kit.js       # Tìm kiếm và bộ lọc nâng cao
        └── tab-session.js      # Mã tab cho phiên đăng nhập
```
