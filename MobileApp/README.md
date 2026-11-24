# App bán hàng (Android)

App mua hàng cho khách, viết bằng .NET MAUI (`net10.0-android`, Android 7.0/API 24 trở lên). App chỉ gọi [REST API](../Backend/SportShop.Api/README.md), không kết nối MySQL. Các bước chạy chi tiết: [README tổng](../README.md).

## Chức năng

| Màn hình | File | Làm được gì |
|---|---|---|
| Đăng nhập, đăng ký | `AuthPage` | Đăng nhập, đăng ký, Google, dùng với tư cách khách |
| Quên mật khẩu | `ForgotPasswordPage` | Nhận mã OTP qua email, đặt mật khẩu mới |
| Trang chủ | `HomePage` | Danh mục, sản phẩm mới, khuyến mãi, thông tin |
| Sản phẩm | `MainPage`, `CatalogFiltersPage` | Tìm, lọc, sắp xếp, tải thêm, thêm giỏ và mua nhanh |
| Mua nhanh | `QuickPurchasePage` | Chọn biến thể và số lượng ngay từ danh sách |
| Chi tiết sản phẩm | `ProductDetailPage` | Ảnh, biến thể, tồn kho, sản phẩm cùng loại |
| Đánh giá | `ReviewsPage` | Xem đánh giá, gửi đánh giá khi đã nhận hàng |
| Giỏ hàng, thanh toán | `CartPage`, `CheckoutPage` | Gợi ý mua thêm, voucher, đặt đơn |
| Đơn hàng | `OrdersPage` | Danh sách, tìm, lọc, hủy đơn chờ chưa thanh toán |
| Tài khoản | `ProfilePage`, `ChangePasswordPage` | Sửa hồ sơ, đổi mật khẩu, đăng xuất |
| Khuyến mãi, thông tin | `PromotionsPage`, `InfoPagesPage` | Mã giảm giá, chính sách và hướng dẫn |

**Cách hoạt động:**
- **Phiên đăng nhập:** token giữ trong `SecureStorage`. `SessionManager` làm mới token trước khi hết hạn 30 giây; API trả 401 thì `AuthorizedHandler` làm mới rồi gửi lại đúng một lần.
- **Khách vãng lai:** app gửi mã thiết bị (`DeviceIdentity`) để dùng giỏ hàng, thanh toán và đơn hàng.
- **Bộ nhớ đệm:** danh sách đơn hàng giữ 10 phút trong RAM, tải lại khi giỏ hoặc đơn thay đổi, xóa khi đăng xuất.

## Cấu hình

| Nội dung | Vị trí |
|---|---|
| Địa chỉ API, hiện là `http://localhost:5080/` | `Services/ApiSettings.cs` |
| HTTP không mã hóa chỉ cho `localhost` và `127.0.0.1` | `Platforms/Android/Resources/xml/network_security_config.xml` |
| Link quay về sau đăng nhập Google: `sportshop://auth` | `Platforms/Android/WebAuthenticationCallbackActivity.cs` |
| Mã app `vn.sportshop.mobile`, phiên bản, font, ảnh | `SportShop.Mobile/SportShop.Mobile.csproj` |

Vì app gọi `localhost:5080`, máy ảo và điện thoại cần lệnh `adb reverse tcp:5080 tcp:5080` (script bên dưới tự chạy). Dùng trên máy khác thì đổi `ApiSettings.cs` sang địa chỉ HTTPS của API.

## Cách chạy và build

Cần .NET 10 SDK, workload MAUI Android, Android SDK API 36, JDK 21 và API đã có `appsettings.Local.json`. Chạy tại thư mục `WEBAPP_SPORT`, khi đã mở máy ảo hoặc cắm điện thoại:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\MobileApp\run-android.ps1 -AndroidSdkDirectory "C:\Program Files (x86)\Android\android-sdk"
```

| Tham số | Công dụng |
|---|---|
| `-SkipBuild` | Cài lại bản đã build |
| `-DeviceSerial` | Chọn thiết bị khi có nhiều máy |
| `-AndroidSdkDirectory`, `-JavaSdkDirectory` | Chỉ rõ Android SDK, JDK 21 |

Chỉ build file APK:

```powershell
dotnet build MobileApp/SportShop.Mobile/SportShop.Mobile.csproj -f net10.0-android
```

Chạy test (không cần máy ảo hay API):

```powershell
dotnet test MobileApp/SportShop.Mobile.Tests/SportShop.Mobile.Tests.csproj
```

## Cây thư mục

```text
MobileApp/
├── run-android.ps1             # Build, chạy API nếu cần, nối cổng, cài và mở app
├── PRODUCT_DISCOVERY_REPORT.md # Báo cáo luồng sản phẩm và nội dung
├── SportShop.Mobile/
│   ├── App.xaml(.cs)           # Chuyển giữa màn hình đăng nhập và Trang chủ
│   ├── MauiProgram.cs          # Đăng ký service, ViewModel, trang
│   ├── AppNavigation.cs        # Điều hướng danh sách sản phẩm, tránh mở chồng
│   ├── *Page.xaml(.cs)         # Các màn hình ở bảng trên
│   ├── Services/
│   │   ├── AuthApi.cs, GoogleSignIn.cs, PasswordResetApi.cs # Đăng nhập, Google, quên mật khẩu
│   │   ├── AccountApi.cs       # Hồ sơ, đổi mật khẩu
│   │   ├── CatalogApi.cs, ContentApi.cs # Sản phẩm, nội dung, đánh giá
│   │   ├── CommerceApi.cs      # Giỏ hàng, thanh toán, đơn hàng
│   │   ├── SessionManager.cs, SessionStore.cs, AuthorizedHandler.cs # Phiên và token
│   │   ├── DeviceIdentity.cs   # Mã thiết bị của khách vãng lai
│   │   ├── AppMemoryCache.cs   # Bộ nhớ đệm
│   │   ├── AdvancedOrderSearch.cs # Tìm đơn nâng cao
│   │   └── ApiSettings.cs      # Địa chỉ API
│   ├── ViewModels/             # Trạng thái và thao tác của từng màn hình
│   ├── Platforms/Android/      # Manifest, MainActivity, cấu hình mạng
│   └── Resources/              # Icon, ảnh, font, màu và style
└── SportShop.Mobile.Tests/     # Test Services và ViewModels
```
