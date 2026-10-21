# SportShop CRM

Đồ án môn Hệ thống thông tin doanh nghiệp (nhóm 10, Đại học Sài Gòn):
**Xây dựng hệ thống quản lý khách hàng (CRM) cho doanh nghiệp bán đồ thể thao.**

Phân tích và thiết kế cơ sở dữ liệu nằm trong file `Báo cáo đồ án cuối kỳ.pdf`.

## Cấu trúc thư mục

```
SportShop/
├── Backend/
│   └── sports_shop.sql                # Script MySQL: tạo CSDL sports_shop (35 bảng) + dữ liệu demo
├── Frontend/
│   ├── SportShop.Admin/               # Web quản trị (ASP.NET Core MVC, .NET 10)
│   └── SportShop.CustomerWeb/         # Web mua hàng cho khách hàng (ASP.NET Core Razor)
├── MobileApp/                         # App điện thoại cho khách hàng (.NET MAUI), chưa làm
├── Admin Dashboard for SPORTSHOP/     # Mã nguồn thiết kế Figma (React), dùng để tham khảo giao diện
├── SportShop.slnx                     # Solution mở bằng Visual Studio
└── Báo cáo đồ án cuối kỳ.pdf
```

## Hiện trạng

### Web quản trị (`Frontend/SportShop.Admin`)
Giao diện theo thiết kế Figma, **đọc dữ liệu từ MySQL** (`Models/ShopData.cs`). Đăng nhập kiểm tra bảng `TAI_KHOAN`
(chỉ quản lý / nhân viên đang hoạt động). Gồm các trang:
tổng quan, tài khoản và phân quyền, khách hàng, nhân viên, loại sản phẩm, sản phẩm,
hóa đơn, khuyến mãi và marketing, phản hồi khách hàng, khảo sát,
nhập hàng, báo cáo thống kê. Tìm kiếm, bộ lọc và phân trang chạy được trên các bảng.

### Web khách hàng (`Frontend/SportShop.CustomerWeb`)
Giao diện theo thiết kế Figma. Gồm: trang chủ cửa hàng, đăng nhập,
đăng ký, chỉnh sửa thông tin, gửi phản hồi về sản phẩm, làm khảo sát.
Khi đã kết nối MySQL, đăng nhập / đăng ký / chỉnh sửa thông tin / gửi phản hồi / làm khảo sát **đọc và ghi dữ liệu thật**
(`Services/CustomerStore.cs`); trang chủ cửa hàng vẫn dùng dữ liệu mẫu.
Nút "Trang quản trị" chuyển sang web quản trị (địa chỉ cấu hình trong
`appsettings.json`, mục `Links:AdminUrl`).

### Cơ sở dữ liệu
Dùng MySQL. Khi chưa kết nối cơ sở dữ liệu, các nút cần dữ liệu (thêm, sửa, xóa, gửi phản hồi...)
hiện thông báo "Chức năng này sẽ hoạt động khi hệ thống được kết nối cơ sở dữ liệu".
Khi đã kết nối thì các chức năng chạy thật và không hiện thông báo này.

### Chưa làm
- Web quản trị: các nút thêm / sửa / xóa, cập nhật trạng thái chưa ghi vào CSDL.
- Mã hóa mật khẩu (hiện lưu dạng văn bản) và phân quyền theo vai trò trong web quản trị.
- Các trang giỏ hàng, chi tiết sản phẩm, thanh toán, lịch sử mua hàng của khách.
- App điện thoại (`MobileApp`).

## Cách chạy

Yêu cầu: Visual Studio có .NET 10 SDK.

1. Mở `SportShop.slnx` bằng Visual Studio.
2. Muốn chạy cả hai web cùng lúc: chuột phải vào Solution, chọn **Configure Startup Projects**,
   chọn **Multiple startup projects**, đặt `SportShop.Admin` và `SportShop.CustomerWeb` là **Start**
   (Debug Target chọn `http`).
3. Bấm **F5**.

| Dự án | Địa chỉ (profile http) |
|---|---|
| Web quản trị | http://localhost:5188 |
| Web khách hàng | http://localhost:5003 |

Tài khoản demo (mật khẩu đều là `123456`):

| Web | Tài khoản |
|---|---|
| Web quản trị | `admin` (quản lý), `nv_lan`, `nv_tuan` (nhân viên) |
| Web khách hàng | `kh_an`, `kh_binh`, `kh_chi` (`kh_dung` bị khóa) |

## Kết nối MySQL

1. Bật MySQL 8 (dịch vụ **MySQL80**), chạy `Backend/sports_shop.sql` (MySQL Workbench → File → Open SQL Script → ⚡)
   để tạo CSDL `sports_shop`.
2. Điền chuỗi kết nối trong `appsettings.json` của **cả hai** web, mục `ConnectionStrings:Default`
   (mỗi máy tự điền mật khẩu MySQL của mình, không đưa mật khẩu lên GitHub):

```
Server=localhost;Port=3306;Database=sports_shop;User=root;Password=...;
```

Web quản trị không kết nối được MySQL sẽ hiện trang hướng dẫn khắc phục. Web khách hàng không kết nối được
thì tự chuyển về chế độ xem giao diện (dữ liệu mẫu, các nút hiện thông báo).
