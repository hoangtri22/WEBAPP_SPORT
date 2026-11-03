# Nghiệm thu màn hình catalog Android — 03/11/2026

Mốc đầu tiên đã chạy trên **điện thoại thật, Android 11, ARM64** qua USB.
API ASP.NET Core chạy tại `http://localhost:5080`; điện thoại truy cập bằng
`adb reverse tcp:5080 tcp:5080`. Không dùng sản phẩm giả trong mobile client.

## Dữ liệu và môi trường

- MySQL local cổng 3306 ban đầu chưa có schema `sports_shop`.
- Đã tạo mới bằng `Backend/Database/sports_shop.sql` và áp dụng migration 001–003.
  Dữ liệu mẫu từ repo gồm 65 sản phẩm, 138 biến thể; API trả 64 sản phẩm còn hàng.
- Cấu hình API local dùng kết nối database hiện có của web, lưu trong
  `Backend/SportShop.Api/appsettings.Local.json` được Git bỏ qua.
- .NET SDK 10.0.202, MAUI 10.0.20, Android workload 36.1.43, Android SDK API 36.
- JDK 25 trên máy không được workload này hỗ trợ; đã tải Microsoft OpenJDK 21
  vào `.test-artifacts/tools/jdk21`, không đổi Java hệ thống.

## Kết quả đã xác minh

| Kiểm tra | Kết quả |
| --- | --- |
| Build Debug `android-arm64` | Thành công, 0 lỗi, 1 cảnh báo XA0033 |
| Cài APK và mở Activity trên điện thoại thật | Thành công |
| `/health/ready` | HTTP 200, `ready` |
| Trang đầu API | 20/64 sản phẩm |
| Ảnh của 20 sản phẩm đầu | 20/20 URL trả HTTP 200 |
| Giao diện native | Thấy ảnh thật, tên, danh mục, giá VNĐ, tồn hàng |
| Đối chiếu sản phẩm đầu | Cước vợt Yonex BG65 — 170.000đ; Khăn thể thao microfiber — 150.000đ |
| Tìm kiếm native `Yonex` | 13/13 sản phẩm, khớp API |
| Ngắt reverse và mở lại app | Hiện thông báo lỗi và nút Thử lại |
| Khôi phục reverse và bấm Thử lại | Tải lại 20/64 sản phẩm |
| Kiểm thử API client/view model | 9/9 đạt, 0 bỏ qua |
| Cú pháp script PowerShell và `git diff --check` | Đạt |

Ảnh và UI XML kiểm tra lưu local trong `.test-artifacts` (Git bỏ qua):
`sportshop-products.png`, `sportshop-search.xml`, `sportshop-offline.xml`,
`sportshop-retry.xml`. Ảnh sản phẩm xác nhận trực quan bằng screenshot từ điện thoại.

Cảnh báo XA0033 xuất hiện vì Android tooling chưa phân tích được chuỗi phiên bản
JDK `21.0.13.0.2` có năm phần; build, cài APK và chạy native vẫn thành công.

Các bài test client kiểm tra encode từ khóa, giữ URL ảnh backend, JSON không hợp lệ,
lỗi server/timeout, cancellation, retry đúng trang/từ khóa, chống request trùng và
thay kết quả tìm kiếm. Phân trang và kéo làm mới có trong app; thao tác kéo làm mới
chưa được nghiệm thu riêng bằng một ca native tại đầu danh sách.

Chưa nghiệm thu Release, emulator, iOS hoặc các màn hình tiếp theo (đăng nhập,
chi tiết sản phẩm, giỏ hàng, checkout, tài khoản). Dữ liệu là MySQL local khởi tạo
từ SQL repo, không phải database cửa hàng đang vận hành.

## Chạy lại

```powershell
powershell -NoProfile -File .\MobileApp\run-android.ps1
```

Hoặc dùng task VS Code `Mobile: Chạy trên Android thật (USB)` (Ctrl+Shift+B).
Giữ cáp USB và API chạy để điện thoại tiếp tục tải dữ liệu/ảnh.
