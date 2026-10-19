# SportShop CRM

Đồ án môn Hệ thống thông tin doanh nghiệp (nhóm 10, Đại học Sài Gòn):
**Xây dựng hệ thống quản lý khách hàng (CRM) cho doanh nghiệp bán đồ thể thao.**

Phân tích và thiết kế cơ sở dữ liệu nằm trong file `Báo cáo đồ án cuối kỳ.pdf`.

## Cấu trúc thư mục

```
SportShop/
├── Backend/
│   └── Database/                      # Cơ sở dữ liệu MySQL (nhóm thực hiện)
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
Giao diện theo thiết kế Figma, dùng dữ liệu mẫu (chưa nối cơ sở dữ liệu). Gồm các trang:
tổng quan, tài khoản và phân quyền, khách hàng, nhân viên, loại sản phẩm, sản phẩm,
hóa đơn, bán hàng tại quầy, khuyến mãi và marketing, phản hồi khách hàng, khảo sát,
nhập hàng, báo cáo thống kê. Tìm kiếm, bộ lọc và phân trang chạy được trên các bảng.

### Web khách hàng (`Frontend/SportShop.CustomerWeb`)
Giao diện theo thiết kế Figma, dùng dữ liệu mẫu. Gồm: trang chủ cửa hàng, đăng nhập,
đăng ký, chỉnh sửa thông tin, gửi phản hồi về sản phẩm, làm khảo sát.
Nút "Trang quản trị" chuyển sang web quản trị (địa chỉ cấu hình trong
`appsettings.json`, mục `Links:AdminUrl`).

### Cơ sở dữ liệu
Dùng MySQL. Khi chưa kết nối cơ sở dữ liệu, các nút cần dữ liệu (thêm, sửa, xóa, gửi phản hồi...)
hiện thông báo "Chức năng này sẽ hoạt động khi hệ thống được kết nối cơ sở dữ liệu".
Khi đã kết nối thì các chức năng chạy thật và không hiện thông báo này.

### Chưa làm
- Kết nối MySQL cho web quản trị và web khách hàng (đọc/ghi dữ liệu thật).
- Đăng nhập thật, mã hóa mật khẩu và phân quyền theo vai trò.
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

Trang đăng nhập của cả hai web hiện là chế độ xem giao diện: nhập thông tin hợp lệ là vào được.

## Kết nối MySQL

Chuỗi kết nối điền trong `appsettings.json`, mục `ConnectionStrings:Default`
(mỗi máy tự điền, không đưa mật khẩu lên GitHub). Ví dụ:

```
Server=localhost;Port=3306;Database=sportshop;User=root;Password=...;
```
