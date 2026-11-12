# Tìm kiếm nâng cao và sản phẩm cùng loại — 12/11/2026

## A. Phân tích trước khi sửa

Git sạch trước khi bắt đầu. Không có `AGENTS.md` trong repository. Phân tích thực hiện trên mã nguồn, không dùng mô tả README cũ làm căn cứ xác định tính năng đã có.

Mobile là .NET MAUI Android (`net10.0-android`), XAML và code-behind, ViewModel tự triển khai `INotifyPropertyChanged`/`AsyncCommand`. `CatalogViewModel` đăng ký singleton; trang và ViewModel chi tiết đăng ký transient. Điều hướng dùng `Navigation.PushAsync`/`PopAsync`. `CatalogApi` dùng HttpClient với DTO từ `SportShop.Contracts`; không kết nối DB trực tiếp.

Backend là ASP.NET Core .NET 10, `ProductsController` → `ICatalogReader` → `MySqlCatalogReader`, truy vấn Dapper/MySqlConnector. Sản phẩm đọc từ `SAN_PHAM`, danh mục từ `LOAI_SP`, biến thể từ `BIEN_THE_SP`: ID, tên, danh mục, đơn vị, mô tả, size, màu, giá, tồn kho, ảnh và trạng thái. Size/màu có thể thiếu; giá là `decimal(12,2)`. Sản phẩm/biến thể công khai có trạng thái `hien`, danh mục không phải `[Đã xóa]`. Tùy chọn mặc định chỉ lấy sản phẩm có biến thể công khai còn hàng.

| Nội dung | Hiện trạng trước sửa | Cần chỉnh sửa | Nguy cơ ảnh hưởng |
|---|---|---|---|
| Tìm kiếm sản phẩm | Từ khóa gửi backend, escape URL/LIKE; trạng thái lỗi/rỗng | Cho phép thay request bằng bộ lọc mới | Phản hồi cũ ghi đè |
| Lọc giá, size, màu | Web đã lọc cùng biến thể; Mobile/API chưa hỗ trợ | Tham số tùy chọn, metadata thật, UI chọn nhiều | Ghép nhầm điều kiện từ nhiều biến thể |
| Danh mục và sắp xếp | Đã có mới nhất/giá tăng/giá giảm/tên A–Z, chỉ còn hàng | Kết hợp với bộ lọc mới | Mất danh mục, đổi cách sắp xếp cũ |
| Phân trang/tải thêm | Backend COUNT/LIMIT/OFFSET trong transaction; client giữ bộ lọc cơ bản, retry và loại ID trùng | Lưu thêm giá/size/màu; chống request lỗi thời | Lặp trang, trộn kết quả, tải vô hạn khi trang chỉ có ID trùng |
| Chi tiết sản phẩm | Size/màu theo biến thể, giá/ảnh/tồn kho/số lượng, giỏ/mua ngay/đánh giá | Bổ sung gợi ý và bảo vệ vòng đời request | Lẫn sản phẩm, mất lựa chọn khi Back |
| Sản phẩm liên quan | Chưa có | Cùng danh mục, loại ID hiện tại trước LIMIT, trang chi tiết riêng | Sai ID, vòng lặp tải, lỗi gợi ý chặn chi tiết |
| API Backend | Có catalog/list/detail; chưa có lọc nâng cao/options/related | Mở rộng tương thích ngược, dùng reader hiện tại | Ảnh hưởng client cũ và quy tắc hiển thị |

Không phát hiện chức năng yêu thích đã triển khai trong Mobile; chỉ có tài nguyên hình trái tim. Không thêm chức năng này ngoài phạm vi.

## B. Những thay đổi đã thực hiện

### File Mobile

| File (tương đối với `MobileApp/`) | Mục đích |
|---|---|
| `SportShop.Mobile/Services/CatalogApi.cs` | Giữ overload cũ, thêm query giá/size/màu, đọc metadata và related; kiểm tra JSON/ID, lỗi và cancellation; xử lý WebException/IOException của transport Android |
| `SportShop.Mobile/ViewModels/CatalogViewModel.cs` | Bản nháp và snapshot đã áp dụng; validation giá; reset; metadata; generation/cancellation; phân trang không phụ thuộc số ID duy nhất |
| `SportShop.Mobile/MainPage.xaml` | Giữ tìm kiếm/danh mục/sort/stock; thêm mở bộ lọc, đặt lại tất cả và mô tả bộ lọc đang áp dụng; ẩn footer khi rỗng để tránh chồng chữ |
| `SportShop.Mobile/MainPage.xaml.cs` | Mở trang bộ lọc qua navigation hiện có |
| `SportShop.Mobile/CatalogFiltersPage.xaml` (mới) | Trang cuộn cho khoảng giá và nhiều checkbox size/màu; áp dụng, xóa, trạng thái tải/lỗi; giới hạn chiều rộng nhãn dài |
| `SportShop.Mobile/CatalogFiltersPage.xaml.cs` (mới) | Tải metadata theo vòng đời trang; chỉ lần áp dụng mới nhất được tự đóng trang |
| `SportShop.Mobile/ViewModels/ProductDetailViewModel.cs` | Related có tải/lỗi/rỗng/retry riêng; bỏ phản hồi cũ; giữ biến thể/số lượng khi tải lại cùng sản phẩm, kể cả callback từ Picker khi thay danh sách |
| `SportShop.Mobile/ProductDetailPage.xaml` | Danh sách ngang sản phẩm cùng loại, dùng `ProductCard` hiện có |
| `SportShop.Mobile/ProductDetailPage.xaml.cs` | Nhấn gợi ý mở trang transient với đúng ID; thử lại related |
| `SportShop.Mobile.Tests/AdvancedCatalogTests.cs` (mới) | Query/metadata, giá không hợp lệ, kết hợp/reset, paging/retry, race, hủy và phục hồi metadata |
| `SportShop.Mobile.Tests/ProductDetailTests.cs` | Mở rộng kiểm tra related, lỗi/race, giữ biến thể/số lượng và giỏ |
| `SportShop.Mobile.Tests/RelatedCatalogApiTests.cs` (mới) | Endpoint/ID/DTO, JSON sai, lỗi HTTP/mạng/timeout/cancellation |
| `ADVANCED_CATALOG_REPORT.md`, `README.md` | Báo cáo và liên kết kết quả |

### File Backend

| File (tương đối với `Backend/`) | Mục đích |
|---|---|
| `SportShop.Api/Catalog/ProductSearchQuery.cs` | Validate khoảng giá và mảng size/màu; chuẩn hóa; đọc giới hạn decimal độc lập locale |
| `SportShop.Api/Controllers/ProductsController.cs` | Hai endpoint đọc options/related; tái sử dụng reader |
| `SportShop.Application/Catalog/ICatalogReader.cs` | Thêm điều kiện tùy chọn cuối `ProductSearch`, loại ID nội bộ và phương thức đọc options |
| `SportShop.Contracts/Catalog/CatalogDtos.cs` | DTO mới `ProductFilterOptionsDto`; không đổi DTO sản phẩm cũ |
| `SportShop.Infrastructure/MySqlCatalogReader.cs` | EXISTS cùng biến thể, tham số hóa IN, cùng predicate cho COUNT/list, options thật và loại ID trước LIMIT |
| `SportShop.Api.Tests/CatalogApiFactory.cs` | Fake reader tương thích các thao tác đọc mới |
| `SportShop.Api.Tests/CatalogApiTests.cs` | HTTP binding/validation/locale, contract cũ/mới, related và options |
| `SportShop.Api.Tests/MySqlCatalogTests.cs` | So sánh SQL thật với dữ liệu gốc, lọc kết hợp, cùng biến thể, phân trang/loại ID/options |
| `SportShop.Api.Tests/MySqlCatalogHttpTests.cs` | Kiểm tra HTTP → MySQL cho lọc nâng cao, metadata và related |

### API và quy tắc

- Tái sử dụng `GET /api/v1/products` và `GET /api/v1/products/{id}`.
- List nhận thêm `minPrice`, `maxPrice`, `sizes`, `colors`. Mỗi mảng dùng tham số lặp, ví dụ `sizes=M&sizes=L&colors=Đỏ` (URL encode khi gửi). Giá từ 0 đến 9.999.999.999,99; min không lớn hơn max; tối đa 50 giá trị mỗi nhóm; size tối đa 20 ký tự, màu 30.
- `GET /api/v1/products/filter-options?inStockOnly=false` trả `{ "sizes": [...], "colors": [...] }` từ toàn bộ catalog công khai. Bao gồm lựa chọn ở biến thể hết hàng để dùng được khi tắt “Chỉ còn hàng”; không lấy từ trang sản phẩm hiện tại.
- `GET /api/v1/products/{id}/related?limit=8` trả mảng `ProductSummaryDto`; limit 1–20. Backend suy ra danh mục từ ID, dùng `ProductsAsync` với loại ID nội bộ trước LIMIT; chỉ công khai và còn hàng. Sản phẩm nguồn không công khai/không tồn tại trả 404.
- OR giữa các size, OR giữa các màu; AND giữa giá, size, màu, từ khóa, danh mục, tồn kho. Giá/size/màu phải thỏa trên cùng một biến thể công khai, còn hàng nếu bật stock filter.
- Giá hiển thị trên card và giá dùng sắp xếp vẫn là min/max của toàn bộ biến thể công khai như trước. Một card có thể hiện giá thấp nhất ngoài khoảng lọc nếu một biến thể khác khớp khoảng giá; đây là cách hiển thị sản phẩm sẵn có của hệ thống, không phải lọc theo giá card.

### Giữ bộ lọc và phân trang

Người dùng sửa bản nháp; Tìm/Áp dụng chụp một snapshot đầy đủ và bắt đầu trang 1. Tải thêm/retry dùng snapshot thành công gần nhất và `_page + 1`, không đọc lại lựa chọn chưa áp dụng. Lỗi trang sau giữ kết quả và số trang cũ. Lỗi tìm mới giữ kết quả cũ với dòng mô tả bộ lọc cũ, tránh trộn hai tập dữ liệu.

Request cùng điều kiện đang chạy và tải thêm trùng được bỏ qua. Request bộ lọc mới hủy request cũ, tăng generation; trước khi ghi dữ liệu/lỗi/trạng thái đều kiểm tra thế hệ và cancellation. Vì vậy transport không tuân thủ hủy cũng không ghi đè kết quả mới. ID trùng bị loại; điều kiện còn trang dựa trên trang/pageSize/total của backend, tránh vòng lặp do đếm ID duy nhất.

Xóa từng ô giá/bỏ từng checkbox gỡ từng điều kiện. “Xóa giá, size và màu” giữ từ khóa/danh mục/sort; “Đặt lại tất cả” khôi phục mặc định, gồm danh mục tất cả, từ khóa rỗng, mới nhất, chỉ còn hàng. Metadata có gate riêng và tải lại khi lỗi; mở trang lọc lúc catalog vừa bị hủy vẫn lấy lại được lựa chọn.

### Sản phẩm cùng loại và điều hướng

Sau khi tải chi tiết, gợi ý tải riêng, không khóa thao tác mua. Client kiểm tra thêm đúng danh mục, khác ID nguồn, còn hàng và loại ID trùng. Không dùng cache chi tiết mới. Mỗi lần nhấn tạo `ProductDetailPage`/ViewModel transient, `PushAsync` rồi `LoadAsync(id)`. Back quay lại trang trước; tải lại cùng sản phẩm giữ ID biến thể và số lượng, có điều chỉnh theo tồn kho mới như hành vi cũ. Request detail/related cũ bị hủy và kiểm tra generation; không tự tải chuỗi gợi ý vô hạn.

## C. Kết quả kiểm thử

### Tự động

| Lượt | Kết quả thực tế | Bằng chứng |
|---|---|---|
| Mobile trước sửa | 98 PASS, 0 FAIL, 0 SKIP | `.test-artifacts/advanced-catalog/mobile-baseline.trx` |
| Backend trước sửa, chưa cấu hình DB test | 131 PASS, 0 FAIL, 35 SKIP | `.test-artifacts/advanced-catalog/backend-baseline.trx` |
| Mobile sau sửa | **149 PASS, 0 FAIL, 0 SKIP** | `.test-artifacts/advanced-catalog/mobile-final.trx` |
| Backend sau sửa, MySQL tạm riêng | **190 PASS, 0 FAIL, 0 SKIP** | `.test-artifacts/mobile-api-20261112-101028-ed7a75ea/mobile-api.trx`, `test.log` |

Backend chạy đầy đủ hồi quy catalog, authentication/session/customer, commerce, reviews, content, MVC compatibility và MySQL. DB test mới được tạo trong `.test-artifacts` bằng harness có kiểm tra datadir/loopback/port; không áp migration hoặc ghi dữ liệu vào DB cửa hàng. Fake chỉ dùng trong unit test; ứng dụng đọc API/DB thật.

Lượt tích hợp đầu phát hiện FAIL do `RangeAttribute` đọc giới hạn decimal theo locale Việt Nam; đã sửa `ParseLimitsInInvariantCulture=true` rồi chạy lại toàn bộ 190 test thành công. Lượt baseline trong sandbox bị DPAPI/profile key store cản trở; chạy lại bằng môi trường quyền phù hợp đã hoàn tất, không sửa mã authentication. Không còn test FAIL ở các lượt cuối.

| # | Ca yêu cầu | Bằng chứng tự động/kết luận |
|---|---|---|
| 1 | Từ khóa | PASS — URL encode, LIKE literal, HTTP/MySQL |
| 2 | Khoảng giá | PASS — giá 0/decimal/kết hợp, invalid và min > max |
| 3 | Size | PASS — mảng lặp, chuẩn hóa, lựa chọn thật, SQL |
| 4 | Màu | PASS — tiếng Việt, encoding, SQL |
| 5 | Giá + size + màu + danh mục | PASS — cùng biến thể, stock/visibility, HTTP → MySQL |
| 6 | Sort khi lọc | PASS — giữ snapshot, đổi sort quay trang 1 |
| 7 | Tải thêm khi lọc | PASS — snapshot, đúng trang/retry, chống ID trùng |
| 8 | Thay lọc khi request cũ chạy | PASS — kết quả/lỗi/append cũ không ghi đè |
| 9 | Reset | PASS — xóa nâng cao giữ danh mục; reset toàn bộ về mặc định |
| 10 | Không có kết quả | PASS — ShowEmpty và dừng tải thêm |
| 11 | Chi tiết đúng sản phẩm | PASS — ID/JSON, thay sản phẩm, không áp response cũ |
| 12 | Cùng loại không chứa chính nó | PASS — loại trước LIMIT, visibility, dedup/category/stock |
| 13 | Nhấn cùng loại | Unit test xác minh dữ liệu theo ID; native xác minh ở phần dưới |
| 14 | Back | Unit test giữ biến thể/số lượng và thay request đã hủy; stack native xác minh ở phần dưới |
| 15 | Size/màu/số lượng/thêm giỏ | PASS tự động — 5 test chi tiết cũ và bộ commerce regression giữ nguyên |
| 16 | API lỗi/mất mạng | PASS — HTTP lỗi, timeout, cancellation, retry; related không chặn mua |
| 17 | Yêu thích | **Không áp dụng/chưa kiểm thử** — Mobile chưa có chức năng này trước sửa |
| 18 | Danh mục/tìm kiếm cơ bản | PASS — test cũ và MVC/API compatibility |

### Build, typecheck và kiểm tra diff

- `dotnet test MobileApp/SportShop.Mobile.Tests/SportShop.Mobile.Tests.csproj --no-restore`: compiler + 149 test thành công.
- Build `Backend/SportShop.Api.Tests/SportShop.Api.Tests.csproj --no-restore`: 0 lỗi, 0 cảnh báo ở lượt cuối; bao gồm các project tham chiếu Web/API.
- Android Debug ARM64, SDK API 36 và JDK 21: build C#/XAML và đóng gói APK thành công; 0 lỗi. Log `.test-artifacts/advanced-catalog/android-build.log`.
- Còn cảnh báo `CS8602` tại `Platforms/Android/MainActivity.cs:15` (file không sửa) và `XA0033` do workload không phân tích được chuỗi phiên bản JDK `21.0.13.0.2`; không phải lỗi code mới. Cảnh báo `TabCookies.cs` xuất hiện ở baseline, không sửa ngoài phạm vi.
- Không có lệnh lint riêng được cấu hình cho các project này. `git diff --check` kiểm tra whitespace; compiler thực hiện kiểm tra kiểu và XAML. Không gọi đây là kết quả lint toàn dự án.
- Chưa build Release/AAB; không suy ra kết quả Release từ Debug.

### Kiểm thử native

Đã chạy trên **Joy 4, Android 11, ARM64, màn hình 1080×2340** bằng APK Debug, UIAutomator/ADB, API thật nối tới DB test riêng. Package test `vn.sportshop.mobile` được cài mới/cập nhật; không đụng package `vn.sportshop.mobile.discoverytest` có sẵn. Không đổi display settings. Ảnh/XML/API JSON ở `.test-artifacts/advanced-catalog/native`.

| Thao tác native thực sự đã chạy | Kết quả | Bằng chứng trong thư mục native |
|---|---|---|
| Danh mục Giày + từ khóa Pegasus + giá giảm dần + giá 2–3 triệu + size 42 + Đen | PASS — 1/1 Pegasus; giữ toàn bộ điều kiện | `04-catalog-category`, `05-sort-selected`, `08-combined-selected`, `09-combined-results` |
| Sản phẩm cùng loại và nhấn mở | PASS — API trả 8 ID khác nhau, cùng danh mục/còn hàng/không chứa ID 2; Pegasus ID 2 → Brooks ID 46 | `product-2-related.json`, `12-related-list`, `13-related-detail`; lặp lại trên APK cuối ở `33-final-related`, `34-final-brooks` |
| Back về Pegasus, giữ size 42/Đen và số lượng 2 | **PASS trên APK cuối** | `32-final-quantity-two`, `35-final-back` |
| Thêm giỏ ở phiên khách, biến thể đã chọn | PASS — hiện thông báo thêm thành công | `15-add-cart` |
| Đặt lại tất cả | **PASS trên APK cuối** — danh mục tất cả/từ khóa rỗng/mới nhất/chỉ còn hàng, trang đầu 20/65 | `36-final-reset` |
| Không có kết quả | **PASS trên APK cuối** — 0/0, thông báo rõ, footer không chồng chữ | `37-final-empty` |
| Ngắt API test khi gọi catalog, rồi khởi động lại và bấm Thử lại | **PASS trên APK cuối** — lỗi thân thiện, giữ sản phẩm cũ, retry về 20/65 trong cùng PID app, không crash | `40-final-offline`, `41-final-recovered`, `final-device.pid`, `crash-log-after-final.log` |

Phiên native đầu phát hiện ba lỗi: Picker làm số lượng 2 về 1 khi Back; Android transport ném `WebException` gây crash; footer chồng thông báo rỗng. Đã sửa cả ba trong phạm vi catalog/chi tiết, thêm test mô phỏng callback Picker và lỗi transport, rồi cài lại APK cuối để kiểm tra lại thành công. Giữ bằng chứng lỗi ban đầu (`14-back-pegasus`, `catalog-offline-crash-before-fix.log`, `22-empty`) để đối chiếu; không tính các lần thất bại ban đầu là PASS. Ảnh `39-final-offline` chụp trước khi lỗi xuất hiện, bằng chứng lỗi chính xác là `40-final-offline`.

APK cuối: `MobileApp/SportShop.Mobile/bin/Debug/net10.0-android/android-arm64/vn.sportshop.mobile-Signed.apk`.
SHA-256: `0BEBD6730165EBBE59AF978688B6DB380997D33AD512F08821A4CB6CEA335C34`.

Kết thúc kiểm thử đã dừng đúng API và MySQL test tự khởi chạy, khôi phục `adb reverse` về trạng thái trống ban đầu và dừng app test. Package có sẵn và display settings giữ nguyên. Nhật ký thao tác chi tiết: `.test-artifacts/advanced-catalog/native/native-results.md`.

Giới hạn native: chưa chạy tải thêm qua nhiều trang trong lúc đang chọn nhiều size/màu, request đổi lọc liên tiếp có độ trễ có kiểm soát, hết gợi ý, mọi loại lỗi API hoặc mọi phương án thanh toán trên thiết bị; các ca được đánh dấu PASS tự động chỉ có bằng chứng ở tầng test tương ứng. Chưa kiểm thử tablet/màn hình nhỏ khác, landscape/font scale và Release. Yêu thích chưa tồn tại nên không thể nghiệm thu. Không suy kết quả ViewModel/API thành PASS UI cho các ca chưa thao tác.

## D. Tác động và giới hạn

- Không đổi schema DB, migration, dữ liệu cửa hàng hoặc cấu hình môi trường. Không nâng dependency.
- Không đổi endpoint/DTO sản phẩm đang có; chỉ thêm tham số tùy chọn và endpoint đọc. Request cũ giữ mặc định và thuật toán sắp xếp cũ. Cần triển khai backend mới trước hoặc cùng bản Mobile mới để có endpoint/tham số mới.
- Không sửa authentication, authorization, session, cart service, checkout, order, profile, Web hoặc hệ thống navigation chung. Chi tiết vẫn gọi commerce service cũ; chỉ bảo vệ request và thêm vùng gợi ý. Bộ hồi quy đã chạy không phát hiện ảnh hưởng, không khẳng định mọi luồng sản xuất đã được kiểm thử.
- Chỉ sửa các file catalog/chi tiết, test liên quan và tài liệu báo cáo. Không ghi đè thay đổi của developer khác; không commit tự động.
- Phân trang OFFSET giữ hành vi sẵn có: nếu dữ liệu/giá/tồn kho thay đổi giữa hai request, thứ tự tập kết quả có thể dịch chuyển; client loại ID trùng nhưng không tạo snapshot xuyên suốt nhiều trang. Backend giữ COUNT và trang nhất quán trong từng transaction.
- Chưa đo hiệu năng trên catalog lớn; không thêm index/schema chỉ dựa trên suy đoán. Chưa nghiệm thu mọi kích thước màn hình, landscape, accessibility/font scale hoặc bản Release.
