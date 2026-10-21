# SportShop.CustomerWeb

Web mua hàng dành cho khách hàng (ASP.NET Core Razor), giao diện theo thiết kế Figma.
Dùng khi khách không muốn tải app; chức năng tương tự app (`MobileApp`).

## Đã có
- Trang chủ cửa hàng: khuyến mãi, danh mục, sản phẩm nổi bật, banner, đăng ký nhận ưu đãi.
- Đăng nhập, đăng ký (kiểm tra dữ liệu nhập giống Figma).
- Khu vực khách hàng: chỉnh sửa thông tin, gửi phản hồi về sản phẩm (xem lại phản hồi đã gửi), làm khảo sát.
- Nút "Trang quản trị" chuyển sang web admin (địa chỉ cấu hình ở `appsettings.json` → `Links:AdminUrl`).

## Cơ sở dữ liệu (MySQL)
- Chưa có CSDL: bấm các chức năng cần dữ liệu sẽ hiện thông báo "Chức năng này sẽ hoạt động khi hệ thống được kết nối cơ sở dữ liệu".
- Có CSDL: đăng nhập, đăng ký, chỉnh sửa thông tin, gửi phản hồi, làm khảo sát đọc / ghi dữ liệu thật
  (`Services/CustomerStore.cs`); các nút của chức năng chưa làm (giỏ hàng, sản phẩm...) hiện "đang phát triển".
  Cờ kết nối nằm ở `Services/DbStatus.cs` (mở thử kết nối MySQL, nhớ kết quả 15 giây).
- Chuỗi kết nối điền ở `appsettings.json` → `ConnectionStrings:Default` (mỗi máy tự điền, không đưa mật khẩu lên GitHub).

## Chạy
Mở `SportShop.slnx` bằng Visual Studio, chọn dự án `SportShop.CustomerWeb`, bấm F5 (cổng http 5003).
Muốn nút "Trang quản trị" hoạt động thì chạy thêm `SportShop.Admin` (cổng 5188).
