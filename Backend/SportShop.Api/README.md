# API/Backend

REST API cho [App bán hàng](../../MobileApp/README.md) (ASP.NET Core, .NET 10, cổng 5080) và CSDL MySQL dùng chung cho cả hệ thống. Chuẩn bị chung: [README tổng](../../README.md).

## Kiến trúc

| Project | Vai trò |
|---|---|
| `SportShop.Api` | Controller, xác thực, lỗi, ảnh, OpenAPI |
| `SportShop.Application` | Interface đọc catalog, đồng hồ và kiểm tra CSDL |
| `SportShop.Infrastructure` | Cài đặt các interface trên bằng Dapper |
| `SportShop.Contracts` | DTO, app dùng chung |
| `SportShop.Shared` | Biên dịch `Services` và `Models` của [Web bán hàng](../../Frontend/SportShop.CustomerWeb/README.md): giỏ, đặt đơn, voucher, tài khoản |
| `SportShop.Api.Tests` | Test API |

Catalog đọc qua `ICatalogReader`; giỏ, đơn, tài khoản, phản hồi, khảo sát dùng `SportShop.Shared`. API không tự chạy migration.

## Database

Script gốc `Backend/Database/sports_shop.sql` tạo schema `sports_shop` gồm 35 bảng và dữ liệu demo. Chạy migration theo thứ tự thì thành 43 bảng.

| Migration | Nội dung |
|---|---|
| `001_mobile_auth.sql` | Phiên đăng nhập mobile, token làm mới đã dùng |
| `002_commerce.sql` | Giỏ hàng MySQL, chống đặt đơn trùng |
| `003_payment_methods.sql` | Thêm phương thức thanh toán mặc định |
| `004_guest_devices.sql` | Thiết bị của khách vãng lai |
| `005_password_reset.sql` | Mã OTP quên mật khẩu |
| `006_google_login.sql` | Liên kết tài khoản Google |
| `007_remove_vip_tier.sql` | Bỏ hạng VIP, hạng cao nhất là Vàng |
| `008_walkin_customer_orders.sql` | Đơn mua tại quầy cho khách chưa có tài khoản |

Cột `BIEN_THE_SP.hinh_anh` ghi dạng `/images/ten-anh.jpg`, file đặt trong `Backend/Images`.

## Xác thực

- **Khách có tài khoản:** gửi `Authorization: Bearer <accessToken>`. Access token sống 15 phút, phiên làm mới tối đa 30 ngày. Token là chuỗi ngẫu nhiên (không phải JWT), CSDL chỉ lưu hash. Mỗi lần làm mới đổi cả hai token; dùng lại token làm mới cũ thì phiên bị thu hồi.
- **Khách vãng lai:** gửi `X-Guest-Device-Key`, chỉ dùng được `/api/v1/cart`, `/api/v1/checkout`, `/api/v1/orders`. Đơn lưu theo số điện thoại; đăng ký bằng số đó thì đơn và địa chỉ được gộp vào tài khoản (`GuestHistory` của web khách).
- **Quên mật khẩu:** mã OTP sống 10 phút, sai 5 lần thì hủy, gửi lại sau 60 giây.
- **Google:** app mở `/api/v1/auth/google/start`, Google quay về `/api/v1/auth/google/callback`, app đổi vé (2 phút, dùng một lần) ở `/api/v1/auth/google/exchange`.
- Đăng nhập sai 5 lần khóa 5 phút. Tài khoản nhân viên không đăng nhập được API.

## API chính

| Nhóm | Đường dẫn | Cần đăng nhập |
|---|---|---|
| Kiểm tra | GET `/health/live`, `/health/ready` | Không |
| Sản phẩm | GET `/api/v1/products`, `/products/filter-options`, `/products/{id}`, `/products/{id}/related`, `/products/{id}/reviews` | Không |
| Danh mục | GET `/api/v1/categories`, `/promotions`, `/interests`, `/payment-methods` | Không |
| Nội dung | GET `/api/v1/pages`, `/pages/{slug}` | Không |
| Thiết bị khách | POST `/api/v1/guest-devices` | Không |
| Tài khoản | POST `/api/v1/auth/register`, `/login`, `/refresh`, `/logout`, `/password/forgot`, `/password/reset`, `/google/...` | Chỉ `/logout` |
| Hồ sơ | GET, PUT `/api/v1/me`; POST `/me/password` | Có |
| Giỏ hàng | GET, DELETE `/api/v1/cart`; POST `/cart/items`; PUT, DELETE `/cart/items/{variantId}` | Có hoặc khách vãng lai |
| Thanh toán | GET `/api/v1/checkout`, `/checkout/vouchers`; POST `/checkout/quote` | Có hoặc khách vãng lai |
| Đơn hàng | GET, POST `/api/v1/orders`; GET `/orders/{id}`; POST `/orders/{id}/cancel` | Có hoặc khách vãng lai |
| Phản hồi | GET, POST `/api/v1/me/feedback`; GET `/me/feedback/eligible-products` | Có |
| Khảo sát | GET `/api/v1/me/surveys`, `/me/surveys/{id}`; POST `/me/surveys/{id}/responses` | Có |

- Đặt đơn bắt buộc có header `Idempotency-Key`; gửi lại cùng khóa và nội dung trả đơn cũ (200), đơn mới trả 201.
- Lỗi trả `application/problem+json` gồm `status`, `title`, `code`, `traceId`; dữ liệu sai có thêm `errors`.
- Danh sách đầy đủ: `/openapi/v1.json` (chỉ Development) hoặc gọi thử bằng [SportShop.Api.http](SportShop.Api.http).

## Cấu hình

Tạo `appsettings.Local.json` (git bỏ qua) từ [appsettings.Local.example.json](appsettings.Local.example.json). Biến môi trường ghi đè file. API không đọc cấu hình của hai web.

| Khóa | Ý nghĩa | Mặc định |
|---|---|---|
| `ConnectionStrings:Default` | Kết nối MySQL, bắt buộc | Trống |
| `Database:TimeZoneId` | Múi giờ của cột ngày giờ | `Asia/Ho_Chi_Minh` |
| `Media:PublicBaseUrl` | Địa chỉ gốc của ảnh trả cho app | `http://localhost:5080` |
| `Paths:Images`, `Paths:Uploads` | Thư mục ảnh | `../Images`, `../../Frontend/Uploads` |
| `Commerce:LegacyCartPath` | File giỏ hàng JSON cũ của web | `../../Frontend/SportShop.CustomerWeb/App_Data/carts.json` |
| `Google:ClientId`, `Google:ClientSecret` | Đăng nhập Google | – |
| `Smtp:Host`, `Port`, `EnableSsl`, `Username`, `Password`, `From`, `FromName` | Gửi mã OTP. Chưa có `Host` thì ở Development mã chỉ hiện trong log | – |

## Cách chạy

Chạy tại thư mục `WEBAPP_SPORT`:

```powershell
Copy-Item Backend/SportShop.Api/appsettings.Local.example.json Backend/SportShop.Api/appsettings.Local.json
```

Sửa chuỗi kết nối trong file vừa tạo; không dùng Google hoặc gửi email thì xóa mục `Google`, `Smtp`. Sau đó chạy API và mở http://localhost:5080/health/ready để kiểm tra:

```powershell
dotnet run --project Backend/SportShop.Api --launch-profile http
```

**Test:** lệnh dưới chạy các test không cần MySQL; test MySQL tự bỏ qua khi chưa đặt `SPORTSHOP_TEST_MYSQL`.

```powershell
dotnet test Backend/SportShop.Api.Tests
```

Chạy đủ mọi test trên MySQL tạm, không đụng CSDL thật, đồng thời xuất lại `Backend/docs/openapi-v1.json`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Backend/scripts/Test-MobileApi.ps1
```

Kết quả và CSDL tạm nằm trong `.test-artifacts` (git bỏ qua). Không đặt biến test trỏ vào CSDL đang dùng.

## Cây thư mục

```text
Backend/
├── Database/
│   ├── sports_shop.sql         # Tạo CSDL và dữ liệu demo
│   └── Migrations/             # 001 đến 008
├── Images/                     # Ảnh sản phẩm
├── SportShop.Api/
│   ├── Program.cs              # Khởi tạo và đăng ký dịch vụ
│   ├── appsettings.json, appsettings.Local.example.json # Cấu hình và mẫu cấu hình
│   ├── SportShop.Api.http      # Request mẫu
│   ├── Controllers/            # Các endpoint, CommerceMapping.cs đổi dữ liệu sang DTO
│   ├── Auth/
│   │   ├── BearerAuthenticationHandler.cs # Đọc token hoặc mã thiết bị khách
│   │   ├── MobileTokenStore.cs # Cấp, làm mới, thu hồi token
│   │   ├── GuestDeviceStore.cs # Thiết bị và hồ sơ khách vãng lai
│   │   ├── PasswordResetStore.cs, PasswordResetMailer.cs # Mã OTP và email
│   │   └── GoogleLogin.cs      # Đăng nhập Google
│   ├── Catalog/                # Tham số tìm sản phẩm, đổi sang DTO
│   ├── Errors/                 # Lỗi dạng ProblemDetails
│   ├── Media/MediaUrlResolver.cs # URL và phục vụ ảnh
│   └── OpenApi/MobileApiDocument.cs # Tùy chỉnh tài liệu OpenAPI
├── SportShop.Application/      # Interface Catalog, Health
├── SportShop.Contracts/        # DTO Catalog, Commerce, Common, Customer
├── SportShop.Infrastructure/   # Đọc catalog, kết nối, kiểm tra CSDL
├── SportShop.Shared/
│   ├── SportShop.Shared.csproj # Liên kết Services, Models của Web bán hàng
│   └── StoreDatabase.cs        # Mở kết nối dùng chung
├── SportShop.Api.Tests/        # Test HTTP và MySQL, Fixtures chứa dữ liệu test
├── docs/openapi-v1.json        # OpenAPI đã xuất
└── scripts/Test-MobileApi.ps1  # Chạy test trên MySQL tạm
```
