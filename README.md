# SPORTSHOP – Hệ thống bán hàng và quản lý khách hàng

Hệ thống cho một cửa hàng đồ thể thao, gồm:

- **Web quản trị** (`Frontend/SportShop.Admin`) cho Quản lý và Nhân viên: khách hàng, nhân viên, sản phẩm, nhập hàng, đơn hàng,
  hóa đơn, khuyến mãi, phản hồi, khảo sát, báo cáo thống kê.
- **Web khách hàng** (`Frontend/SportShop.CustomerWeb`) để khách xem sản phẩm, mua hàng, theo dõi đơn, gửi phản hồi và làm khảo sát.
- **App di động** (`MobileApp`, .NET MAUI) cho khách hàng — chức năng tương tự web khách hàng, sẽ làm sau.
- **Cơ sở dữ liệu MySQL** dùng chung cho cả hai web (`Backend/Database/sports_shop.sql`).

Công nghệ: ASP.NET Core MVC (.NET 10), Dapper + MySqlConnector, MySQL 8.

## Cấu trúc thư mục

```
WEBAPP_SPORT/
├── Backend/
│   ├── Database/sports_shop.sql       # Script MySQL: tạo CSDL sports_shop (35 bảng) + dữ liệu demo
│   ├── SportShop.Api/                 # REST API v1 cho toàn bộ chức năng User, cổng 5080
│   ├── SportShop.Application/         # Interface và use case backend
│   ├── SportShop.Contracts/           # DTO JSON thuần C#
│   ├── SportShop.Infrastructure/      # Truy vấn MySQL bằng Dapper
│   ├── SportShop.Shared/              # Service/model gốc dùng chung Web và API
│   ├── SportShop.Api.Tests/           # Kiểm thử HTTP và MySQL riêng
│   └── Images/                        # Ảnh sản phẩm, hai web phục vụ tại đường dẫn /images
├── Frontend/
│   ├── SportShop.Admin/               # Web quản trị
│   ├── SportShop.CustomerWeb/         # Web khách hàng
│   └── Uploads/                       # Ảnh tải lên từ web quản trị, hai web phục vụ tại /uploads (git bỏ qua)
├── MobileApp/                         # App điện thoại cho khách hàng (.NET MAUI), chưa làm
├── Admin Dashboard for SPORTSHOP/     # Mã nguồn thiết kế Figma (React), chỉ để tham khảo giao diện
└── SportShop.slnx                     # Solution mở bằng Visual Studio
```

## Cách chạy

Yêu cầu: Visual Studio có .NET 10 SDK và MySQL 8.

1. **Tạo CSDL:** mở MySQL Workbench, kết nối tới server, chọn File → Open SQL Script → `Backend/Database/sports_shop.sql`, bấm ⚡
   (Ctrl + Shift + Enter, chạy cả script). Kết quả đúng là schema `sports_shop` có **35 bảng**.
2. **Cấu hình kết nối:** tạo file `appsettings.Local.json` đặt cạnh `appsettings.json` của **cả hai** web:

```json
"ConnectionStrings": {
  "Default": "Server=localhost;Port=3306;Database=sports_shop;User=root;Password=MAT_KHAU_CUA_BAN;"
}
```

> **Lưu ý bảo mật:** Không đưa mật khẩu lên GitHub. Có thể tạo file `appsettings.Local.json` đặt cạnh `appsettings.json` với nội dung tương tự — file này đã được `.gitignore` bỏ qua tự động. Nội dung trong `appsettings.Local.json` sẽ tự ghi đè `appsettings.json` khi chạy.

## Bước 3 – Chạy web

### 🟣 Visual Studio (khuyến nghị)

Yêu cầu: **Visual Studio 2022** trở lên (có cài workload **ASP.NET and web development**) và **.NET 10 SDK**.

1. Mở file `SportShop.slnx` bằng Visual Studio.
2. Chuột phải vào **Solution** trong Solution Explorer → **Configure Startup Projects…**
3. Chọn **Multiple startup projects**, đặt cả hai dự án thành **Start**:
   - `SportShop.Admin` → Start
   - `SportShop.CustomerWeb` → Start
4. Ở thanh toolbar, chọn Debug Target là **`http`** (tránh lỗi chứng chỉ HTTPS).
5. Bấm **F5** (chạy có debug) hoặc **Ctrl + F5** (chạy không debug).

Hai cửa sổ trình duyệt sẽ tự mở:

| Dự án | Địa chỉ |
|---|---|
| Web quản trị | http://localhost:5188 |
| Web khách hàng | http://localhost:5003 |

> Sau khi sửa file `.cs` hoặc `.cshtml`: dừng và bấm **F5** lại. Sau khi sửa `.css` / `.js`: chỉ cần **Ctrl + F5** trên trình duyệt.

---

### 🔵 Visual Studio Code

Yêu cầu: **Visual Studio Code**, extension **[C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)** và **.NET 10 SDK**.

Mở hai terminal riêng (Terminal → New Terminal) và chạy lần lượt:

**Terminal 1 – Web quản trị:**
```bash
dotnet run --project Frontend/SportShop.Admin --launch-profile http
```

**Terminal 2 – Web khách hàng:**
```bash
dotnet run --project Frontend/SportShop.CustomerWeb --launch-profile http
```

Hoặc dùng **`dotnet watch`** để tự reload khi sửa code:

```bash
# Terminal 1
dotnet watch --project Frontend/SportShop.Admin --launch-profile http

# Terminal 2
dotnet watch --project Frontend/SportShop.CustomerWeb --launch-profile http
```

Sau khi khởi động, mở trình duyệt và truy cập:

| Web | Địa chỉ |
|---|---|
| Web quản trị | http://localhost:5188 |
| Web khách hàng | http://localhost:5003 |
| API MobileApp (chạy riêng) | http://localhost:5080 |

> **Mẹo VS Code:** Có thể tạo file `.vscode/tasks.json` để chạy cả hai lệnh bằng một lần bấm **Ctrl + Shift + B**. Xem thêm tài liệu [VS Code Tasks](https://code.visualstudio.com/docs/editor/tasks).

---

## Tài khoản demo

Mật khẩu đều là `123456`.

| Web | Tài khoản |
|---|---|
| Web quản trị | `admin` (Quản lý), `nv_lan`, `nv_tuan`, `nv_hoa`… (Nhân viên) |
| Web khách hàng | `kh_an` (Vàng), `kh_khanh` (Bạc), `kh_binh`, `kh_mai`, `kh_linh`… (Thường); `kh_dung` bị khóa |

Muốn thử mã cho khách mới thì đăng ký một tài khoản mới ở web khách hàng rồi đặt đơn đầu tiên.

## Web khách hàng

- **Tài khoản:** đăng ký, đăng nhập, đăng xuất, sửa thông tin cá nhân (kèm sở thích thể thao), tự đổi mật khẩu.
  Sai mật khẩu 5 lần thì tạm khóa đăng nhập 5 phút (`Services/LoginThrottle.cs`).
- **Mua hàng:** trang chủ, danh sách sản phẩm (lọc theo loại, tìm kiếm, sắp xếp), chi tiết sản phẩm chọn size / màu
  (mỗi màu có ảnh riêng), giỏ hàng, thanh toán, trang khuyến mãi. Chỉ hiện sản phẩm đang bán và còn tồn kho.
  - Phải đăng nhập mới thêm vào giỏ được; giỏ của từng tài khoản được lưu lại giữa các lần đăng nhập
    trong MySQL dùng chung mobile. File `App_Data/carts.json` cũ được import một lần theo khách và giữ nguyên làm bản sao;
    cấu hình `Commerce:LegacyCartPath` của hai host trỏ cùng file nếu deploy khác thư mục.
  - Thanh toán: điền sẵn người nhận từ sổ địa chỉ, phương thức thanh toán giả lập, miễn phí giao hàng từ 500.000₫.
    Mục **Mã giảm giá** chỉ liệt kê các mã khách dùng được, kèm số tiền giảm theo đơn hiện tại (xem *Quy tắc voucher*).
- **Đơn hàng của tôi:** theo dõi trạng thái chờ xác nhận → đang chuẩn bị → đang giao → đã giao; tự hủy đơn khi còn chờ xác nhận;
  đánh giá sản phẩm trong đơn đã giao.
- **Phản hồi và khảo sát:** chỉ gửi phản hồi cho sản phẩm đã mua và đã nhận; xem câu trả lời của cửa hàng; làm khảo sát được gửi tới.
- **Trang thông tin:** đổi trả, giao hàng, chọn size, liên hệ, giới thiệu… (nội dung ở `Models/InfoPages.cs`).

Mã nguồn chính: `Services/CustomerStore.cs` (tài khoản, phản hồi, khảo sát), `Services/ShopStore.cs` (sản phẩm, giỏ hàng, voucher, đặt hàng, đơn hàng), `Services/CartStore.cs` (lưu giỏ hàng), `Services/TabCookies.cs` (đa phiên theo tab).

## Web quản trị

Đăng nhập bằng tài khoản Quản lý hoặc Nhân viên đang làm việc (tài khoản phải gắn với một hồ sơ nhân viên).
Tài khoản và nhân viên chỉ vai trò Quản lý mới thêm / sửa / xóa được. Tài khoản Nhân viên không truy cập được trang Quản lý nhân viên dù gõ thẳng đường dẫn.

- **Đa phiên theo tab:** tương tự web khách hàng, mỗi tab trình duyệt giữ phiên đăng nhập riêng.

### Cách dùng chung cho mọi danh sách

- Bảng có tìm kiếm nhanh, tìm kiếm nâng cao theo từng cột và phân trang ngay trên trình duyệt.
- **Bấm vào một dòng** để mở trang chi tiết (giữ Ctrl để mở tab mới).
- **Rê chuột vào một dòng** (màn hình rộng từ 1200px) thì chi tiết hiện ở khung bên phải, kèm nút "Chỉnh sửa" và "Mở trang chi tiết".
- Nút thêm / sửa mở biểu mẫu ở ngăn bên phải và ghi thẳng vào MySQL.
- **Xóa mềm** cho tài khoản, khách hàng, nhân viên, nhà cung cấp, khuyến mãi: mục bị xóa chuyển xuống bảng riêng ở cuối trang và có nút **Khôi phục**. Dữ liệu liên quan (đơn hàng, phiếu nhập…) luôn được giữ.

### Từng chức năng

- **Khách hàng:** sửa mọi thông tin trừ mật khẩu (mật khẩu do khách tự đổi, chỉ Quản lý xem được), phân nhóm khách, ghi chú chăm sóc
  và lịch hẹn liên hệ lại. Khách đã xóa không còn hiện khi lập đơn, tính nhóm khách, gửi khảo sát và trong thống kê.
- **Đơn hàng:** lập đơn tại quầy (trừ tồn kho, kiểm tra voucher) và duyệt đơn khách đặt online.
  - Đơn chưa thanh toán chỉ ở "Chờ xác nhận" / "Đang chuẩn bị" / "Đang giao"; phải chuyển "Đã thanh toán" mới chọn được "Đã giao" hoặc "Đã hủy".
  - Chuyển "Đã giao": tự lập hóa đơn, cộng doanh thu, tổng chi tiêu, điểm tích lũy (1 điểm / 10.000₫) và nâng hạng thành viên.
  - Hủy đơn: bắt buộc nhập lý do, hoàn tồn kho, hoàn tiền nếu đã thanh toán và trả lại lượt dùng voucher.
  - "Đã giao" và "Đã hủy" là trạng thái cuối.
- **Hóa đơn:** xem và in; không sửa được để giữ số liệu đối soát.
- **Sản phẩm và nhập hàng:** loại sản phẩm, sản phẩm, biến thể (size, màu, giá bán, giá vốn, tồn, ảnh), nhà cung cấp.
  - Ảnh gắn với từng biến thể: chọn ảnh có sẵn hoặc tải từ máy (JPG, PNG, WEBP, GIF, tối đa 5 MB, lưu ở `Frontend/Uploads`).
  - Phiếu nhập cộng tồn kho và cập nhật giá vốn; chọn được cả sản phẩm chưa có biến thể (tự tạo biến thể mặc định). Phiếu nhập đã lập không sửa / xóa được.
- **Khuyến mãi và marketing:** chương trình giảm theo sản phẩm / đơn hàng / voucher; chiến dịch thông báo theo nhóm khách.
- **Phản hồi và khảo sát:** hộp thư phản hồi (phản hồi chưa trả lời in đậm), trả lời hoặc ẩn phản hồi; tạo, gửi, đóng khảo sát và xem thống kê kết quả.
- **Báo cáo thống kê** (lọc theo khoảng ngày): doanh thu, số đơn, giá trị trung bình, lợi nhuận so với kỳ trước, biểu đồ doanh thu, sản phẩm bán chạy, lợi nhuận theo sản phẩm, hiệu quả khuyến mãi, khách theo độ tuổi, sở thích, hiệu suất nhân viên, tồn kho nhiều.

Mã nguồn chính: `Models/ShopData.cs` (đọc dữ liệu), `Models/ShopCommands.cs` (ghi dữ liệu), `Models/CrudDefs.cs` (định nghĩa biểu mẫu tự động), `Models/TabCookies.cs` (đa phiên theo tab).

## Quy tắc voucher

Áp dụng giống nhau ở web khách hàng và khi lập đơn tại quầy:

- **Mã cho khách mới** (`WELCOME5`, `WELCOME10`, `SPORT20`, `NEWBIE15`): chỉ dùng ở **lần mua đầu tiên** của tài khoản (đơn đã hủy không tính) và chỉ một mã trong nhóm. Nhận biết qua tên chương trình có chữ "bạn mới", "khách mới", "người mới" hoặc "thành viên mới". Giới hạn: **1 lần mỗi khách**.
- **Mã theo hạng thành viên** (Bạc: `BAC8`, `BAC15`, `SINHNHATBAC`; Vàng: `VANG5`, `VANG10`, `VANG15`): chỉ khách đúng hạng mới dùng được. Giới hạn: **2 lần mỗi khách** (Bạc) và **3 lần mỗi khách** (Vàng). Mốc hạng: 2 triệu → Bạc, 5 triệu → Vàng, 10 triệu → VIP (tính khi đơn đã giao và thanh toán).
- Mã phải còn hạn và còn lượt dùng chung. Khách hủy đơn thì lượt dùng voucher được trả lại.
- Lượt dùng tính theo chương trình (nhóm mã), vì CSDL chỉ lưu đơn đã dùng chương trình nào chứ không lưu từng mã.

## Tổ chức mã nguồn

Luồng chung của cả hai web: đường dẫn `/{Controller}/{Hàm}/{id}` → Controller (thư mục `Controllers`) → lớp dữ liệu đọc / ghi MySQL → View (`.cshtml` trong thư mục `Views`).

| Web | Đọc dữ liệu | Ghi dữ liệu |
|---|---|---|
| Quản trị | `Models/ShopData.cs` (nạp từng danh sách khi cần, mỗi request một bản) | `Models/ShopCommands.cs` qua `Controllers/CrudController.cs` (Form, Save, Delete, Act) |
| Khách hàng | `Services/ShopStore.cs`, `Services/CustomerStore.cs` | Cùng hai lớp trên |

- Biểu mẫu thêm / sửa của web quản trị được dựng tự động từ `Models/CrudDefs.cs`; JavaScript giao diện ở `wwwroot/js/admin.js` và `wwwroot/js/search-kit.js`.
- Đa phiên theo tab: `Models/TabCookies.cs` (Admin) và `Services/TabCookies.cs` (CustomerWeb) cùng với `wwwroot/js/tab-session.js` ở mỗi web.
- Mỗi hàm chính có một dòng ghi chú ngay phía trên: hàm làm gì, lấy / ghi dữ liệu ở đâu (bảng MySQL, file, session) và gọi hàm nào, file nào. Không ghi chú từng dòng lệnh.

## Cơ sở dữ liệu

MySQL 8, 35 bảng, kèm dữ liệu demo. Bảng `KHACH_HANG` có cột `da_xoa` để xóa mềm khách hàng.
CSDL tạo trước khi có cột này thì thêm bằng lệnh (không mất dữ liệu):

```sql
ALTER TABLE KHACH_HANG ADD COLUMN da_xoa TINYINT(1) NOT NULL DEFAULT 0 AFTER tong_chi_tieu;
```

## Gặp lỗi

- **Web quản trị báo "Lỗi kết nối CSDL":** kiểm tra dịch vụ MySQL đang chạy (`services.msc`), đúng cổng và mật khẩu trong `appsettings.json`.
  Máy có nhiều server MySQL (ví dụ cổng 3306 và 3307) thì cổng trong file phải đúng server đã chạy script.
  Web khách hàng không kết nối được thì các chức năng cần dữ liệu hiện thông báo cần kết nối cơ sở dữ liệu.
- **`Table '...' doesn't exist` hoặc `Unknown column 'da_xoa'`:** kiểm tra schema cũ và các migration 001–003; thêm `da_xoa` bằng lệnh `ALTER TABLE` ở trên nếu thiếu. Không chạy lại script tạo DB để nâng cấp dữ liệu đang có.
- **Mã nhân viên ở góc phải (ví dụ NV011) không đúng sau khi tạo lại CSDL:** mã này lưu trong cookie đăng nhập cũ; đăng xuất rồi đăng nhập lại.
  Nếu không, các thao tác ghi người thực hiện (lập đơn, phiếu nhập, trả lời phản hồi) sẽ báo lỗi.
- **Không đăng nhập được web quản trị, báo "Tài khoản khách hàng không được đăng nhập trang quản trị":** tài khoản chưa gắn với hồ sơ nhân viên trong bảng `NHAN_VIEN`.
- **Build báo không chép được file `.exe` / `.dll`:** web đang chạy giữ file; dừng web rồi build lại.

## Chưa làm

- App điện thoại cho khách hàng (`MobileApp`, .NET MAUI) chưa có project; REST API đã có tài khoản/hồ sơ, catalog, giỏ/checkout/đơn, phản hồi/khảo sát và trang thông tin.
- Tự áp dụng khuyến mãi theo sản phẩm và theo mốc đơn hàng ở bước thanh toán (hiện chỉ áp dụng mã voucher).
- Gửi chiến dịch thông báo thật qua email / SMS (hiện chỉ ghi nhận chiến dịch đã gửi).
- Tách lượt dùng voucher theo từng mã trong cùng một chương trình (cần thêm cột lưu mã voucher vào CSDL).

## Phân tích và thiết kế API cho MobileApp

REST API được mở rộng ngày 01/11/2026, dùng chung service/model gốc qua `SportShop.Shared`. Mật khẩu mới dùng hash có salt;
đăng nhập hợp lệ nâng cấp dữ liệu plaintext demo cũ. Mobile dùng opaque bearer access/refresh token, refresh rotation và thu hồi phiên;
đặt đơn có `Idempotency-Key`. Đơn đã thanh toán cần cửa hàng xử lý hoàn tiền, khách không tự hủy để tránh lệch hóa đơn/điểm.
Không đưa token, mật khẩu hoặc DB credential vào app/Git/log. App MAUI chưa được triển khai.

- [Kiểm kê 64 chức năng Web User và API](API_INVENTORY.md).
- [Contract, request/response và lỗi Mobile API](MOBILE_API_DOCUMENTATION.md).
- [Hướng dẫn chạy backend, migration và tích hợp mobile](MOBILE_INTEGRATION_GUIDE.md).
- [Báo cáo kiểm thử và giới hạn xác minh](MOBILE_API_TEST_REPORT.md).
- [Đối chiếu từng màn hình Web → Mobile](Backend/docs/mobile-feature-parity.md).
- [Danh mục file mới/sửa và mã phase-one có sẵn](Backend/docs/mobile-api-change-manifest.md).

Verifier cuối trên instance MySQL sạch: **142/142 PASS, 0 FAIL, 0 SKIP**, build solution **0 lỗi/0 cảnh báo**.
[TRX và hash đối chiếu](Backend/docs/test-evidence/verification-results.json); app/UI native chưa triển khai và chưa kiểm thử.

- [Phân tích hiện trạng mã nguồn, khả năng tái sử dụng và các điểm cần xử lý](Backend/docs/project-analysis.md).
- [Thiết kế API v1: endpoint, DTO, xác thực, giỏ hàng, checkout và kế hoạch triển khai](Backend/docs/mobile-api-design.md).
- [Bản đồ CSDL và migration dự kiến](Backend/docs/database-readiness.md).
- [Chạy REST API, cấu hình và ví dụ request](Backend/SportShop.Api/README.md).
- [Tiến độ API và phần web được giữ nguyên](Backend/docs/api-implementation-status.md).
