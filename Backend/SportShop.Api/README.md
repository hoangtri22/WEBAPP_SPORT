# SportShop Mobile REST API v1

API ASP.NET Core .NET 10 chạy riêng với hai web MVC, mặc định cổng **5080**. Catalog, tài khoản/hồ sơ, giỏ/checkout/đơn,
feedback, khảo sát và trang thông tin dùng cùng database/service với Web User. Thư viện `SportShop.Shared` compile-link service/model gốc,
không sao chép business logic hoặc tham chiếu host MVC. Không tự chạy migration lúc startup. App MAUI chưa triển khai.

## Các endpoint đã có

| Method | Đường dẫn | Chức năng |
|---|---|---|
| GET | `/health/live` | Kiểm tra host, không cần DB |
| GET | `/health/ready` | Kiểm tra kết nối/schema cần thiết, không sửa dữ liệu |
| GET | `/api/v1/categories` | Loại sản phẩm và số sản phẩm đang bán còn hàng |
| GET | `/api/v1/products` | Tìm kiếm, lọc, sắp xếp, phân trang tại DB |
| GET | `/api/v1/products/{productId}` | Sản phẩm và các biến thể đang hiện, gồm biến thể hết hàng |
| GET | `/api/v1/promotions` | Voucher đang hiệu lực, còn lượt; chưa xác minh điều kiện riêng của khách |
| GET | `/api/v1/interests` | Danh mục sở thích thể thao |
| GET | `/api/v1/payment-methods` | Tất cả phương thức thanh toán đang hoạt động, vẫn mô phỏng như web |
| POST | `/api/v1/auth/register`, `/api/v1/auth/login`, `/api/v1/auth/refresh`, `/api/v1/auth/logout` | Tạo khách, đăng nhập, xoay token, thu hồi phiên; logout cần bearer |
| GET, PUT | `/api/v1/me` | Hồ sơ, hạng/điểm/số đơn và sở thích của khách đăng nhập |
| POST | `/api/v1/me/password` | Đổi mật khẩu, làm mất hiệu lực mọi phiên mobile |
| GET, DELETE | `/api/v1/cart` | Đọc/xóa giỏ dùng chung MySQL |
| POST | `/api/v1/cart/items` | Thêm biến thể, kiểm tra tồn và cộng số lượng |
| PUT, DELETE | `/api/v1/cart/items/{variantId}` | Thay số lượng/xóa một dòng |
| GET | `/api/v1/checkout`, `/api/v1/checkout/vouchers` | Người nhận mặc định, methods, giỏ, voucher đủ điều kiện |
| POST | `/api/v1/checkout/quote` | Tính lại giảm/ship/tổng; không giữ tồn/giá |
| GET, POST | `/api/v1/orders` | Lịch sử phân trang/lọc; đặt giỏ với `Idempotency-Key` |
| GET, POST | `/api/v1/orders/{orderId}`, `/api/v1/orders/{orderId}/cancel` | Chi tiết đơn riêng; tự hủy đơn chờ chưa thanh toán |
| GET, POST | `/api/v1/me/feedback` | Lịch sử/trả lời và gửi đánh giá sản phẩm đã nhận |
| GET | `/api/v1/me/feedback/eligible-products` | Sản phẩm khách đã mua và nhận |
| GET | `/api/v1/me/surveys`, `/api/v1/me/surveys/{id}` | Danh sách và chi tiết khảo sát được giao |
| POST | `/api/v1/me/surveys/{id}/responses` | Nộp đáp án một lần, validate option/required/ownership |
| GET | `/api/v1/pages`, `/api/v1/pages/{slug}` | Nội dung hỗ trợ/giới thiệu dùng chung `InfoPages` |
| GET | `/openapi/v1.json` | OpenAPI sinh từ mã, chỉ mở ở Development |
| GET | `/images/{filename}`, `/uploads/{filename}` | Ảnh có sẵn trong kho dùng chung của hai web |

Route cá nhân cần `Authorization: Bearer <accessToken>` của Customer; token opaque, không phải JWT. Không mở API Admin cho khách.
Contract đầy đủ, mẫu body/response/status/error: [MOBILE_API_DOCUMENTATION.md](../../MOBILE_API_DOCUMENTATION.md).
Đối chiếu toàn bộ Web User: [API_INVENTORY.md](../../API_INVENTORY.md).

## Cấu hình và chạy

Từ thư mục gốc `WEBAPP_SPORT`:

```powershell
Copy-Item Backend/SportShop.Api/appsettings.Local.example.json Backend/SportShop.Api/appsettings.Local.json
```

Áp dụng lần lượt migration `Backend/Database/Migrations/001_mobile_auth.sql`, `002_commerce.sql`, `003_payment_methods.sql` vào schema hiện có;
không chạy lại script tạo DB để nâng cấp. Migration tăng thêm bảng session/refresh/cart/idempotency và bổ sung phương thức thanh toán mô phỏng,
giữ dữ liệu cũ. Sửa `appsettings.Local.json`: điền đúng cổng, DB, user/mật khẩu; runtime cần quyền SELECT/INSERT/UPDATE/DELETE trên các bảng sử dụng,
migration chạy riêng bằng tài khoản có quyền DDL. File local được Git bỏ qua; API không đọc secret của hai web.

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Port=3306;Database=sports_shop;User=sportshop_app;Password=DIEN_MAT_KHAU;"
  },
  "Database": {
    "TimeZoneId": "Asia/Ho_Chi_Minh"
  },
  "Media": {
    "PublicBaseUrl": "http://localhost:5080"
  },
  "Commerce": {"LegacyCartPath": "../../Frontend/SportShop.CustomerWeb/App_Data/carts.json"}
}
```

`Database:TimeZoneId` là múi giờ các giá trị `DATETIME` của DB, mặc định giờ Việt Nam; timestamp response có offset.
`Commerce:LegacyCartPath` phải trỏ cùng file giỏ JSON cũ với web nếu deploy khác cấu trúc thư mục; import một lần theo khách, giữ file nguồn.
Chuyển cả web/API sang bản dùng DB cùng lúc, dừng writer JSON cũ trước khi nhận traffic mới.

```powershell
dotnet run --project Backend/SportShop.Api --launch-profile http
```

Mở `http://localhost:5080/openapi/v1.json` hoặc dùng [SportShop.Api.http](SportShop.Api.http) trong Visual Studio/REST Client. File HTTP phủ mọi operation bằng placeholder, không có token/password thực. Bản [OpenAPI export](../docs/openapi-v1.json) có 30 paths/35 operations, bearer opaque scheme, typed response, request examples và lỗi. Có thể chạy đồng thời Admin cổng 5188 và CustomerWeb cổng 5003 như trước. Chưa cấu hình DB vẫn khởi động được: live/OpenAPI/ảnh hoạt động, endpoint cần DB và ready trả **503**, không giả dữ liệu rỗng. Ready kiểm tra schema các module mobile và bảng migration.

Có thể dùng biến môi trường `ConnectionStrings__Default`, `Database__TimeZoneId`, `Media__PublicBaseUrl` thay cho file local. Biến môi trường được ưu tiên hơn file cấu hình; không đưa credential vào tham số URL hoặc file `.http`.

## Bộ lọc sản phẩm

```http
GET /api/v1/products?categoryId=1&q=giay&sort=price_asc&inStockOnly=true&page=1&pageSize=20
```

- `sort`: `new` (mặc định), `price_asc`, `price_desc`, `name`.
- `inStockOnly=true` mặc định; false để xem cả sản phẩm đang hiện nhưng hết hàng/chưa có biến thể.
- `page` từ 1; `pageSize` từ 1–100, mặc định 20. Trang vượt cuối trả `items: []`, vẫn có tổng kết quả.
- `q` tối đa 100 ký tự, tìm tên sản phẩm hoặc loại; `%`, `_`, `!` được xem như ký tự tìm kiếm, không làm wildcard tùy ý.
- Sản phẩm/biến thể ẩn và loại lưu trữ `[Đã xóa]` không xuất hiện. Giá/tồn/ảnh tổng hợp chỉ từ biến thể đang hiện.

Payload danh sách có `items, page, pageSize, totalItems, totalPages`. Tiền là số JSON decimal theo VND, không chứa chuỗi định dạng hoặc giá vốn.

Ảnh phải thuộc `/images` hoặc `/uploads`; ảnh thiếu/đường dẫn không hợp lệ trả `imageUrl: null`. API chỉ phục vụ định dạng ảnh, không tạo thư mục upload hoặc ghi file. Khi publish, cấu hình `Paths:Images` và `Paths:Uploads` tới kho ảnh thực tế; đường dẫn tương đối được tính từ content root của API, không phải thư mục shell.

Payment-methods trả mọi phương thức active, code `cod` cho tên COD chuẩn và `method_{id}` cho các phương thức khác; GET không seed.
Migration 003 thêm các phương thức mô phỏng mặc định trước đây do web tạo lười. Không gửi thông tin thẻ; đơn mới vẫn chưa thanh toán.

## Phiên và giao dịch dùng chung

Access token 15 phút, refresh/phiên tối đa 30 ngày; DB chỉ lưu hash token, refresh xoay cả hai token và reuse thu hồi phiên.
Mật khẩu mới dùng PBKDF2 có salt; đăng nhập hợp lệ nâng cấp plaintext demo cũ. Khóa/xóa tài khoản/khách hoặc đổi mật khẩu vô hiệu phiên mobile.
Không log token/body auth, không đưa DB credential vào app. Đổi password thành công phải login lại.
CustomerWeb kiểm tra lại trạng thái tài khoản/khách để xóa Session cũ khi khóa/xóa/đổi vai trò; shared StoreDatabase có timeout và trả lỗi DB rõ khi chưa cấu hình.

Giỏ lưu MySQL đồng bộ web/app. Đặt hàng khóa/tái kiểm tra giá/tồn/voucher, ghi đơn và xóa giỏ trong một transaction; key/body giữ nguyên khi retry,
201 đơn mới, 200 replay. Quote chỉ xem trước. Tự hủy chỉ với đơn chờ chưa thanh toán; đơn đã trả tiền cần cửa hàng xử lý hoàn hóa đơn/điểm.

## Kết nối MobileApp và môi trường thật

Android emulator trên cùng máy: dùng API base URL `http://10.0.2.2:5080/api/v1` và cấu hình `Media:PublicBaseUrl=http://10.0.2.2:5080` để URL ảnh cũng truy cập được từ emulator.

Máy thật dùng địa chỉ LAN/domain của máy chạy API và bind host phù hợp, ví dụ cấu hình URL lắng nghe của profile dev; không dùng localhost của điện thoại. Native MAUI HttpClient không cần mở CORS. Bản phát hành dùng HTTPS; Production tự chuyển HTTP sang HTTPS và không mở OpenAPI. Cấu hình chứng chỉ/hostname/media URL trên máy chủ trước khi triển khai; không tắt kiểm tra chứng chỉ cho bản phát hành.

## Lỗi và kiểm thử

Lỗi trả `application/problem+json`, gồm `status`, `title`, `code`, `traceId`; validation thêm `errors`. Các trường hợp chính: 400 dữ liệu sai, 404 không thấy/đang ẩn, 503 DB chưa cấu hình/kết nối/schema không phù hợp, 500 lỗi không dự kiến. Chi tiết exception chỉ ghi log phía server, không đưa ra response. API ghi log console để chạy bằng tài khoản không có quyền Windows Event Log.

```powershell
dotnet build SportShop.slnx --nologo
dotnet test Backend/SportShop.Api.Tests --nologo
```

Bộ HTTP contract tests dùng host thật và dependency test; nhóm MySQL dùng `SPORTSHOP_TEST_MYSQL`, bỏ qua rõ ràng nếu thiếu.
Catalog đọc DB; customer/commerce integration tạo và dọn dữ liệu thử riêng để kiểm tra auth/rotation/ownership/giỏ/order/voucher/survey.
Không đặt biến test tới DB đang vận hành. [Hướng dẫn kiểm thử DB riêng](../SportShop.Api.Tests/README.md),
[báo cáo thực thi](../../MOBILE_API_TEST_REPORT.md), [tích hợp từng màn hình](../../MOBILE_INTEGRATION_GUIDE.md).

Lần verifier sạch cuối: **142/142 PASS, 0 FAIL, 0 SKIP**, build toàn solution **0 warning/0 error**, test khoảng 11 giây.
[TRX](../docs/test-evidence/mobile-api.trx), [kết quả/hash](../docs/test-evidence/verification-results.json), [log build](../docs/test-evidence/build.log.txt).
Kết quả xác nhận API/backend và MVC được test; chưa có app/UI native.

Thư viện OpenAPI có pin trực tiếp `Microsoft.OpenApi 2.7.5` để không kéo bản 2.0.0 có cảnh báo bảo mật từ dependency của framework; [thông báo bản vá](https://github.com/microsoft/OpenAPI.NET/security/advisories/GHSA-v5pm-xwqc-g5wc).
