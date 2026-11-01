# Backend

Nơi đặt cơ sở dữ liệu, Web API và thiết kế MobileApp của hệ thống SportShop CRM.

- `Database/sports_shop.sql`: script MySQL tạo CSDL `sports_shop` (35 bảng, thiết kế theo chương 4 của báo cáo) và dữ liệu demo.
- Hai web đọc CSDL này qua chuỗi kết nối `ConnectionStrings:Default` trong `appsettings.json`.
- `Images/`: ảnh sản phẩm. Cả hai web phục vụ thư mục này tại đường dẫn `/images`, nên cột `BIEN_THE_SP.hinh_anh`
  ghi dạng `/images/ten-anh.jpg` (tên file phải trùng khớp, kể cả chữ hoa/thường). Thêm ảnh mới chỉ cần chép vào thư mục này.

## REST API cho MobileApp

Đã có tài liệu phân tích và thiết kế ngày 30/10/2026:

- [Phân tích dự án và các điểm cần xử lý](docs/project-analysis.md).
- [Thiết kế API v1: endpoint, DTO, auth, giỏ, checkout và thứ tự triển khai](docs/mobile-api-design.md).
- [Bản đồ 35 bảng và kế hoạch migration](docs/database-readiness.md).
- [Danh mục file được kiểm kê](docs/file-inventory.csv).

API tại [SportShop.Api](SportShop.Api/README.md), cổng dev 5080, có catalog/tìm kiếm/phân trang, tài khoản/hồ sơ, bearer access/refresh token,
giỏ MySQL chung với web, checkout/voucher, đặt đơn chống retry trùng, lịch sử/hủy đơn, feedback, khảo sát được giao, trang hỗ trợ, ảnh và OpenAPI.
`SportShop.Shared` biên dịch các service/model gốc bằng compile links để web/API dùng một implementation; hai MVC host vẫn giữ giao diện/route.

Trước khi chạy bản mới, áp dụng lần lượt `Database/Migrations/001_mobile_auth.sql`, `002_commerce.sql`, `003_payment_methods.sql` vào cùng schema.
Các migration tăng thêm bảng session/refresh-used/cart/idempotency và các phương thức thanh toán mô phỏng mặc định, giữ dữ liệu cũ.
File giỏ JSON được import một lần theo customer rồi web/API cùng ghi DB. Mật khẩu mới được hash; plaintext demo cũ nâng cấp khi login hợp lệ.
Khách chỉ tự hủy đơn chờ và chưa thanh toán; đơn đã trả tiền chuyển cửa hàng xử lý để bảo toàn hóa đơn/điểm. Thanh toán vẫn mô phỏng như web.

Xem [kiểm kê chức năng](../API_INVENTORY.md), [tài liệu API](../MOBILE_API_DOCUMENTATION.md), [hướng dẫn tích hợp](../MOBILE_INTEGRATION_GUIDE.md),
[test report](../MOBILE_API_TEST_REPORT.md) và [tiến độ thực tế](docs/api-implementation-status.md). App MAUI chưa triển khai.
Script `sports_shop.sql` dùng dựng DB demo mới, không dùng thay migration nâng cấp dữ liệu đang có; không đặt credential trong source.
