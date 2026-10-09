# SportShop CRM

Đồ án Hệ thống thông tin doanh nghiệp (nhóm 10, ĐH Sài Gòn): **Hệ thống quản lý khách hàng (CRM) cho doanh nghiệp bán đồ thể thao**.
Chi tiết phân tích và thiết kế CSDL nằm trong file `Báo cáo đồ án cuối kỳ.pdf`.

## Cấu trúc thư mục

```
SportShop/
├── Backend/
│   └── Database/                 # Dành cho cơ sở dữ liệu (chưa làm)
├── Frontend/
│   ├── SportShop.Admin/          # Web admin (ASP.NET Core MVC, .NET 10)
│   └── SportShop.CustomerApp/    # App khách hàng (.NET MAUI), làm sau
├── SportShop.slnx
└── Báo cáo đồ án cuối kỳ.pdf
```

## Đã làm

**Web admin** (`Frontend/SportShop.Admin`), giao diện theo thiết kế Figma, dùng dữ liệu mẫu:

- Đăng nhập (giao diện mẫu) và khung chung: sidebar, thanh trên, thu gọn menu.
- Tổng quan: 6 thẻ thống kê và bảng đơn hàng gần đây.
- Tài khoản & Phân quyền, Quản lý khách hàng (hồ sơ, hạng thành viên, lịch sử mua, chăm sóc), Quản lý nhân viên.
- Quản lý loại sản phẩm, sản phẩm (dạng lưới/danh sách, biến thể size, màu).
- Quản lý hóa đơn (chi tiết, vận chuyển) và Bán hàng tại quầy (hóa đơn tạm, tự tính tiền).
- Khuyến mãi & Marketing, Phản hồi khách hàng (hộp thư), Khảo sát, Quản lý nhập hàng.
- Báo cáo thống kê: lọc theo khoảng ngày, biểu đồ doanh thu, sản phẩm bán chạy, nhóm khách hàng, hiệu suất nhân viên.
- Tìm kiếm, bộ lọc và phân trang trên các bảng.

## Chưa làm

- Cơ sở dữ liệu và API (các nút Thêm/Sửa/Xóa hiện chỉ báo "sẽ hoạt động khi kết nối CSDL").
- Đăng nhập thật và phân quyền theo vai trò.
- Tài khoản riêng cho từng cửa hàng.
- App khách hàng (.NET MAUI).

## Cách chạy web admin

1. Mở `SportShop.slnx` bằng Visual Studio (cần .NET 10 SDK).
2. Chọn profile `http` hoặc `https`, bấm **F5**.
3. Trang đăng nhập mở ra, bấm **Đăng nhập** để vào Tổng quan.
