# Kiểm thử Mobile API

Kết quả lần verifier cuối ngày 01/11/2026: **142/142 PASS, 0 FAIL, 0 SKIP**, build solution **0 warning/0 error**, test khoảng 11 giây.
[TRX gốc](../docs/test-evidence/mobile-api.trx), [kết quả từng test và hash TRX](../docs/test-evidence/verification-results.json),
[log build](../docs/test-evidence/build.log.txt), [báo cáo và giới hạn](../../MOBILE_API_TEST_REPORT.md).
PASS áp dụng backend/API và các luồng MVC trong report; không khẳng định app/UI native đã xây hoặc chạy.

## Chạy bộ HTTP contract tests

```powershell
dotnet test Backend/SportShop.Api.Tests --nologo
```

HTTP contract tests khởi tạo pipeline API thật bằng WebApplicationFactory; catalog dùng fake reader hoặc DB rỗng, commerce validation có test principal.
Kiểm tra validation, 401/403, lỗi, catalog/media, OpenAPI, shared password/survey validator và shipping policy không chứng minh transaction MySQL.
Bộ MySQL integration tests tự bỏ qua với lý do rõ ràng nếu biến môi trường tương ứng chưa đặt. Không dùng test principal để kết luận bearer thật đã được kiểm chứng;
nhóm `MySqlCustomerTests` dùng login/refresh/access token thật, nhóm commerce có HTTP và shared-service test với DB thật.

## Kiểm tra truy vấn MySQL thật

Chỉ dùng DB dành riêng cho test, tên schema chứa `test`. Nạp script seed vào DB mới và áp dụng migration 001–003 trước khi chạy.
`MySqlCatalogTests` chỉ SELECT và tính kỳ vọng từ bảng thô; `MySqlCustomerTests`/`MySqlCommerceTests` **có ghi dữ liệu test** cho đăng ký,
token rotation/expiry/revocation, hồ sơ, feedback, survey, import/cart, đặt/replay/hủy đơn, tranh tồn/quota và FK cart/Admin delete.
Các fixture dùng ID/tên riêng; commerce dọn dữ liệu do nó tạo. Dùng account thử có quyền ghi, không account SELECT-only để chạy toàn bộ suite.

```powershell
$env:SPORTSHOP_TEST_MYSQL = 'Server=127.0.0.1;Port=13380;Database=sports_shop_mobile_test;User=root;Password=;SslMode=None;'
$env:SPORTSHOP_TEST_MYSQL_NO_COD = 'Server=127.0.0.1;Port=13380;Database=sports_shop_mobile_test_no_cod;User=root;Password=;SslMode=None;'
dotnet test Backend/SportShop.Api.Tests --nologo
```

Các connection string trên chỉ dành cho **instance local tạm được tạo riêng, không phải server cửa hàng**. Không đổi port thành cổng MySQL đang dùng cho web để làm bước chuẩn bị fixture.
Database test cần bản sao 35 bảng gốc + migration 001–003; tên chứa `test` là guard của customer integration test. Xóa biến môi trường khỏi terminal khi không tiếp tục dùng test DB.

Trong đợt kiểm tra ngày 30/10/2026, instance MySQL riêng dùng port **13380**, chỉ bind `127.0.0.1`, datadir trong `.test-artifacts/mysql-api-phase1`. Đã nạp bản sao [script seed gốc](../Database/sports_shop.sql) vào instance này; service MySQL của web không bị sửa hoặc restart.

Để tái lập cần tạo instance thử nghiệm tương tự bằng công cụ MySQL local/container của bạn. Không dùng lại datadir của service MySQL hiện có và không chạy lại script tạo DB trên server đang vận hành. Nếu dùng binaries MySQL trên Windows, `mysqld --no-defaults --initialize-insecure --datadir=<thư_mục_test_mới>` tạo dữ liệu riêng; tiến trình server sau đó phải chỉ rõ `--no-defaults`, datadir đó, `--bind-address=127.0.0.1`, `--port=13380` và tắt mysqlx hoặc dùng cổng mysqlx riêng. Khởi tạo chỉ thực hiện trên thư mục mới; không xóa dữ liệu cũ để ép chạy lại.

[Fixtures/mobile-edge-cases.sql](Fixtures/mobile-edge-cases.sql) được verifier hiện tại nạp vào instance mới, gồm sản phẩm không biến thể/hết tồn và từ khóa literal `%_!`; guard yêu cầu schema `sports_shop_mobile_test`, datadir thuộc `.test-artifacts/mobile-api-*` và bind loopback. `MySqlCatalogHttpTests` cần fixture này để xác nhận nhánh tìm kiếm literal có dữ liệu thật. Chạy verifier để dựng đúng fixture cho toàn suite.

[Fixtures/catalog-edge-cases.sql](Fixtures/catalog-edge-cases.sql) là fixture catalog giai đoạn một, có guard theo môi trường cũ: sản phẩm ẩn/lưu trữ, stock lớn, ký tự `%_!`, mốc thời gian/quota. Hướng dẫn import bên dưới là tham khảo môi trường cũ, không thay thế fixture/verifier hiện tại; không dùng `--force` bỏ qua guard.

Ví dụ nạp fixture sau khi đã dựng instance riêng và import seed:

```powershell
& 'C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe' --no-defaults --protocol=TCP --host=127.0.0.1 --port=13380 --user=root --default-character-set=utf8mb4 --execute='source D:/WEBAPP_SPORT/Backend/SportShop.Api.Tests/Fixtures/catalog-edge-cases.sql'
```

Biến `SPORTSHOP_TEST_MYSQL_NO_COD` là tùy chọn: trỏ tới DB test khác chỉ có bảng `PHUONG_THUC_THANH_TOAN` với một vài phương thức không phải COD đang hoạt động và COD ngừng.
Contract hiện trả tất cả phương thức active: test này kiểm tra vẫn trả alternative active, không seed/fallback COD và readiness báo schema thiếu.
Không áp migration 003 vào fixture tối giản này vì nó chủ ý không có COD active. Khi chưa chuẩn bị fixture, bỏ biến để test đó skip.

Fixture no-COD tối thiểu phải giữ đúng các cột `ma_phuong_thuc_thanh_toan INT PRIMARY KEY`, `ten_phuong_thuc_thanh_toan VARCHAR(50)`, `trang_thai VARCHAR(10)`; dữ liệu ví dụ: một chuyển khoản active, một tiền mặt active, tên COD chính xác `Thanh toán khi nhận hàng (COD)` có trạng thái `ngung`. Chỉ tạo trong schema test riêng ở instance tạm.

Tắt đúng tiến trình test hoặc dùng `mysqladmin --no-defaults --protocol=TCP --host=127.0.0.1 --port=13380 --user=root shutdown` sau khi hoàn tất; không dừng service MySQL của web. `.test-artifacts` được Git bỏ qua và có thể giữ lại để kiểm tra lại; không nằm trong bản publish API.

## Bằng chứng thực thi

Có thể chạy verifier từ instance sạch: `powershell -ExecutionPolicy Bypass -File Backend/scripts/Test-MobileApi.ps1 -Port 13382` từ repository root, với MySQL 8 binaries cài sẵn.
Script tạo datadir mới dưới `.test-artifacts`, start hidden MySQL chỉ bind loopback, áp seed+migrations/fixture, restore/build/test và giữ TRX/log;
đồng thời export runtime schema vào `Backend/docs/openapi-v1.json`. Có thể truyền `-MySqlBin` nếu đường dẫn cài đặt khác.
Script không dùng lại/xóa datadir cũ và chỉ dừng process do nó tạo. Migrations 001–003 được áp hai lần để kiểm tra tính lặp; lần verifier đã chạy thành công lưu dưới `.test-artifacts/mobile-api-20261101-140744-b91a4b36`, kết quả 142/142 được dẫn ở đầu tài liệu.

Kết quả hiện tại, lệnh đã chạy, TRX/log, loại test và phần chưa xác minh được ghi tại [MOBILE_API_TEST_REPORT.md](../../MOBILE_API_TEST_REPORT.md).
Không dùng kết quả đợt catalog ngày 30/10/2026 để kết luận các endpoint ghi mới đã pass; không coi test bị skip là pass.
Build hai MVC host và test HTTP/service không thay thế kiểm thử UI/browser toàn bộ. Tài liệu API/flows ở [MOBILE_API_DOCUMENTATION.md](../../MOBILE_API_DOCUMENTATION.md)
và [MOBILE_INTEGRATION_GUIDE.md](../../MOBILE_INTEGRATION_GUIDE.md).
