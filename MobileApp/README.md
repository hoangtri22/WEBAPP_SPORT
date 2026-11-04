# MobileApp

App điện thoại cho khách hàng (.NET MAUI), chức năng tương tự web mua hàng:
đăng ký, đăng nhập, đăng xuất, xem sản phẩm, giỏ hàng, mua hàng, sửa thông tin, lịch sử mua hàng,
gửi phản hồi, làm khảo sát.
Đã có project MAUI Android `SportShop.Mobile` cho mốc đầu tiên: catalog thật từ API,
ảnh sản phẩm từ backend, giá VNĐ, tìm kiếm, kéo làm mới, tải thêm và thử lại khi lỗi.
REST API v1 được mở rộng ngày 01/11/2026 cho toàn bộ chức năng User đã phát hiện trong source:
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
Media:PublicBaseUrl phải truy cập được từ thiết bị. Màn hình ngoài cùng (`AuthPage`) đăng nhập, đăng ký qua API thật và cho dùng với tư cách khách
(gửi tên thiết bị lên `POST /api/v1/guest-devices`, cần migration 004; Web Admin hiện ở trang Quản lý khách hàng).
`HomePage` mới là giao diện theo trang chủ web khách hàng, dữ liệu mẫu trong `ViewModels/HomeSample.cs`, chưa nối API.
Các màn hình chi tiết sản phẩm, giỏ hàng, checkout và tài khoản chưa được triển khai trong app.
Không coi test API là bằng chứng app native đã chạy.

## Chạy Android thật từ VS Code qua USB

Yêu cầu .NET 10 SDK, workload MAUI/Android, Android SDK API 36 và **JDK 21**.
Android workload `36.1.43` hiện có trên máy không hỗ trợ JDK 25.
App nhắm Android 7.0/API 24 trở lên; mốc đầu tiên build Android ARM64.

1. Cài extension **.NET MAUI** và **C# Dev Kit** theo đề xuất trong VS Code.
2. Trên điện thoại: bật Tùy chọn nhà phát triển → Gỡ lỗi USB; cắm cáp truyền dữ liệu,
   mở khóa và chọn **Cho phép** khi điện thoại hỏi tin cậy máy tính.
3. Chuẩn bị DB và `Backend/SportShop.Api/appsettings.Local.json` theo README API.
   API cần `/health/ready` trả `200`, `Media:PublicBaseUrl` là `http://localhost:5080`.
4. Tại thư mục repo, chạy:

```powershell
powershell -NoProfile -File .\MobileApp\run-android.ps1
```

Hoặc bấm **Ctrl+Shift+B** → `Mobile: Chạy trên Android thật (USB)`.
Script tìm SDK/JDK, chọn điện thoại, build APK có sẵn assembly, chạy API nếu cần,
kiểm tra DB/ảnh, cài và mở SportShop. Chạy lại APK đã build bằng `-SkipBuild`.
Nếu có nhiều thiết bị, truyền `-DeviceSerial SERIAL`.

Script ưu tiên JDK 21 đã có trong `JAVA_HOME`, `C:/Program Files/Microsoft` hoặc
`.test-artifacts/tools/jdk21`. Có thể truyền `-JavaSdkDirectory` / `-AndroidSdkDirectory`.
JDK local trong `.test-artifacts` được Git bỏ qua; không thay đổi Java toàn hệ thống.

Kết nối USB dùng:

```powershell
& 'C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe' reverse tcp:5080 tcp:5080
```

Nhờ `adb reverse`, `http://localhost:5080` trên điện thoại đi qua USB tới API máy tính,
bao gồm URL `/images` và `/uploads` trả về trong JSON. Giữ cáp USB và API chạy khi dùng app.
Rút/cắm cáp thì chạy lại script. Không cần cùng Wi-Fi hoặc mở firewall cho LAN.

Cấu hình endpoint ở `SportShop.Mobile/Services/ApiSettings.cs`. Debug chỉ cho HTTP tới
loopback qua network security config; không tắt kiểm tra chứng chỉ HTTPS.
Trước khi phát hành, cấu hình API HTTPS công khai. Chưa nghiệm thu bản Release.
Không đưa thông tin MySQL vào app.

## Kiểm thử client

```powershell
dotnet test .\MobileApp\SportShop.Mobile.Tests\SportShop.Mobile.Tests.csproj
```

Kiểm thử độc lập Android kiểm tra JSON/URL ảnh, encode từ khóa, lỗi server/timeout,
cancellation, phân trang/retry, chống request trùng và thay kết quả khi tìm kiếm.
Nghiệm thu native cần mở app trên điện thoại, thấy tên/giá/ảnh, tìm kiếm và kéo làm mới.

[Báo cáo chạy trên Joy 4 / Android 11 và phạm vi đã xác minh](ANDROID_DEVICE_TEST_REPORT.md).

Tài liệu chính thức: [thiết lập Android thật](https://learn.microsoft.com/dotnet/maui/android/device/setup?view=net-maui-10.0),
[cài MAUI cho VS Code](https://learn.microsoft.com/dotnet/maui/get-started/installation?view=net-maui-10.0&tabs=visual-studio-code),
[Microsoft OpenJDK](https://learn.microsoft.com/java/openjdk/download).
