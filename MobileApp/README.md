# MobileApp

App điện thoại cho khách hàng (.NET MAUI), chức năng tương tự web mua hàng:
đăng ký, đăng nhập, đăng xuất, xem sản phẩm, giỏ hàng, mua hàng, sửa thông tin, lịch sử mua hàng,
gửi phản hồi, làm khảo sát.
Hiện chưa có project MAUI. REST API v1 được mở rộng ngày 01/11/2026 cho toàn bộ chức năng User đã phát hiện trong source:
tài khoản/hồ sơ, catalog, giỏ MySQL chung web, checkout/voucher, đặt đơn có idempotency, theo dõi/hủy đơn, feedback, khảo sát và trang thông tin.

- [Phân tích mã nguồn hiện tại](../Backend/docs/project-analysis.md).
- [Thiết kế API v1 và thứ tự triển khai](../Backend/docs/mobile-api-design.md).
- [Các thay đổi CSDL dự kiến](../Backend/docs/database-readiness.md).
- [API đã chạy được: cấu hình, endpoint và request mẫu](../Backend/SportShop.Api/README.md).
- [Contract REST API hiện tại](../MOBILE_API_DOCUMENTATION.md).
- [Hướng dẫn tích hợp theo từng màn hình](../MOBILE_INTEGRATION_GUIDE.md).
- [Đối chiếu tính năng Web → Mobile](../Backend/docs/mobile-feature-parity.md).
- [Bằng chứng kiểm thử và phần chưa xác minh](../MOBILE_API_TEST_REPORT.md).

Hướng xây dựng: MAUI dùng HttpClient gọi API riêng qua HTTPS, lưu refresh token trong SecureStorage; không kết nối MySQL trực tiếp.
Access token sống 15 phút; refresh tối đa 30 ngày, xoay token và phát hiện reuse. Chỉ chạy một refresh tại một thời điểm.
Checkout giữ các phương thức thanh toán mô phỏng như web, chưa có cổng thanh toán thật; giỏ đồng bộ DB với web.
Đặt đơn gửi cùng Idempotency-Key/body khi retry; khách chỉ tự hủy đơn chờ chưa thanh toán.

API chạy riêng cổng 5080. Backend cần áp dụng migration 001–003 và local connection string trước khi dùng;
Media:PublicBaseUrl phải truy cập được từ thiết bị. Tiếp theo là xây UI/mobile client theo contract và nghiệm thu trên emulator/thiết bị thật.
Không coi test API là bằng chứng app native đã chạy.
