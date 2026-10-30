# SPORTSHOP – Hệ thống bán hàng và quản lý khách hàng

Hệ thống cho một cửa hàng đồ thể thao, gồm:

- **Web quản trị** (`Frontend/SportShop.Admin`) cho Quản lý và Nhân viên: khách hàng, nhân viên, sản phẩm, nhập hàng, đơn hàng,
  hóa đơn, khuyến mãi, phản hồi, khảo sát, báo cáo thống kê.
- **Web khách hàng** (`Frontend/SportShop.CustomerWeb`) để khách xem sản phẩm, mua hàng, theo dõi đơn, gửi phản hồi và làm khảo sát.
- **Cơ sở dữ liệu MySQL** dùng chung cho cả hai web (`Backend/Database/sports_shop.sql`).

Công nghệ: ASP.NET Core MVC (.NET 10), Dapper + MySqlConnector, MySQL 8.

## Cấu trúc thư mục

```
WEBAPP_SPORT/
├── Backend/
│   ├── Database/sports_shop.sql       # Script MySQL: tạo CSDL sports_shop (35 bảng) + dữ liệu demo
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
   {
     "ConnectionStrings": {
       "Default": "Server=localhost;Port=3306;Database=sports_shop;User=root;Password=MAT_KHAU_CUA_BAN;"
     }
   }
   ```

   File này được git bỏ qua nên mỗi máy tự điền cổng và mật khẩu MySQL của mình, không đưa mật khẩu lên GitHub.
3. **Chạy web:** mở `SportShop.slnx` bằng Visual Studio. Chuột phải vào Solution → **Configure Startup Projects** →
   **Multiple startup projects**, đặt `SportShop.Admin` và `SportShop.CustomerWeb` là **Start** (Debug Target chọn `http`), rồi bấm **F5**.

| Dự án | Địa chỉ (profile http) |
|---|---|
| Web quản trị | http://localhost:5188 |
| Web khách hàng | http://localhost:5003 |

Sau khi sửa code C# hoặc file `.cshtml` thì dừng và chạy lại web; sửa CSS / JS thì chỉ cần Ctrl + F5 trên trình duyệt.

### Tài khoản demo

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
    (`App_Data/carts.json`, git bỏ qua, tự tạo khi chạy).
  - Thanh toán: điền sẵn người nhận từ sổ địa chỉ, phương thức thanh toán giả lập, miễn phí giao hàng từ 500.000₫.
    Mục **Mã giảm giá** chỉ liệt kê các mã khách dùng được, kèm số tiền giảm theo đơn hiện tại (xem *Quy tắc voucher*).
- **Đơn hàng của tôi:** theo dõi trạng thái chờ xác nhận → đang chuẩn bị → đang giao → đã giao; tự hủy đơn khi còn chờ xác nhận;
  đánh giá sản phẩm trong đơn đã giao.
- **Phản hồi và khảo sát:** chỉ gửi phản hồi cho sản phẩm đã mua và đã nhận; xem câu trả lời của cửa hàng; làm khảo sát được gửi tới.
- **Trang thông tin:** đổi trả, giao hàng, chọn size, liên hệ, giới thiệu… (nội dung ở `Models/InfoPages.cs`).
- Header và footer dùng chung cho mọi trang (`Views/Shared/_SiteHeader.cshtml`, `_SiteFooter.cshtml`); nút "Trang quản trị"
  dẫn sang web quản trị (địa chỉ ở `appsettings.json` → `Links:AdminUrl`).

Mã nguồn chính: `Services/CustomerStore.cs` (tài khoản, phản hồi, khảo sát), `Services/ShopStore.cs` (sản phẩm, giỏ hàng, voucher, đặt hàng,
đơn hàng), `Services/CartStore.cs` (lưu giỏ hàng).

## Web quản trị

Đăng nhập bằng tài khoản Quản lý hoặc Nhân viên đang làm việc (tài khoản phải gắn với một hồ sơ nhân viên).
Tài khoản và nhân viên chỉ vai trò Quản lý mới thêm / sửa / xóa được.

### Cách dùng chung cho mọi danh sách

- Bảng có tìm kiếm, bộ lọc theo trạng thái và phân trang ngay trên trình duyệt.
- **Bấm vào một dòng** để mở trang chi tiết (giữ Ctrl để mở tab mới; trang Sản phẩm thì ở dạng danh sách, dạng lưới bấm thẻ là mở biểu mẫu sửa).
  Mọi danh sách đều có trang chi tiết dạng thẻ:
  tài khoản, nhân viên, khách hàng, nhóm khách, loại sản phẩm, sản phẩm, nhà cung cấp, phiếu nhập, đơn hàng, hóa đơn,
  khuyến mãi, chiến dịch, khảo sát.
- **Rê chuột vào một dòng** (màn hình rộng từ 1200px) thì chi tiết hiện ở khung bên phải, kèm nút "Chỉnh sửa" và "Mở trang chi tiết".
- Nút thêm / sửa mở biểu mẫu ở ngăn bên phải và ghi thẳng vào MySQL.
- **Xóa mềm** cho tài khoản, khách hàng, nhân viên, nhà cung cấp, khuyến mãi: mục bị xóa chuyển xuống bảng riêng ở cuối trang
  ("Tài khoản đã xóa", "Khách hàng đã xóa", "Nhân viên đã nghỉ việc", "Nhà cung cấp ngừng hợp tác", "Chương trình đã ngừng")
  và có nút **Khôi phục**. Tài khoản và khách hàng có thêm nút **Khóa / Mở khóa**. Dữ liệu liên quan (đơn hàng, phiếu nhập…) luôn được giữ.

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
  - Phiếu nhập cộng tồn kho và cập nhật giá vốn; chọn được cả sản phẩm chưa có biến thể (tự tạo biến thể mặc định).
    Phiếu nhập đã lập không sửa / xóa được.
  - Xóa loại sản phẩm: sản phẩm chưa có lịch sử bị xóa, sản phẩm đã có lịch sử được ẩn sang loại "[Đã xóa]" để không mất báo cáo.
- **Khuyến mãi và marketing:** chương trình giảm theo sản phẩm / đơn hàng / voucher; chiến dịch thông báo theo nhóm khách.
- **Phản hồi và khảo sát:** hộp thư phản hồi (phản hồi chưa trả lời in đậm), trả lời hoặc ẩn phản hồi; tạo, gửi, đóng khảo sát
  và xem thống kê kết quả.
- **Báo cáo thống kê** (lọc theo khoảng ngày): doanh thu, số đơn, giá trị trung bình, lợi nhuận so với kỳ trước, biểu đồ doanh thu,
  sản phẩm bán chạy, lợi nhuận theo sản phẩm, hiệu quả khuyến mãi, khách theo độ tuổi, sở thích, hiệu suất nhân viên, tồn kho nhiều.
  - Nguồn dữ liệu chia đôi: nửa phụ thuộc khoảng ngày (doanh thu, sản phẩm, khuyến mãi) được MySQL cộng dồn và tải mới mỗi lần;
    nửa còn lại (khách, nhân viên, tồn kho) dùng lại dữ liệu các trang khác vừa nạp trong 2 phút (`Models/DataCache.cs`).
  - Bộ nhớ đệm tự xóa sau mỗi thao tác ghi ở web quản trị. Thay đổi từ web khách hàng (đặt đơn, phản hồi…) có thể chậm tối đa 2 phút
    ở nửa dùng lại.

## Quy tắc voucher

Áp dụng giống nhau ở web khách hàng và khi lập đơn tại quầy:

- **Mã cho khách mới** (`WELCOME5`, `WELCOME10`, `SPORT20`, `NEWBIE15`): chỉ dùng ở **lần mua đầu tiên** của tài khoản (đơn đã hủy không tính)
  và chỉ một mã trong nhóm. Nhận biết qua tên chương trình có chữ "bạn mới", "khách mới", "người mới" hoặc "thành viên mới".
- **Mã theo hạng thành viên** (Bạc: `BAC8`, `BAC15`, `SINHNHATBAC`; Vàng: `VANG5`, `VANG10`, `VANG15`): chỉ khách đúng hạng mới dùng được,
  mỗi khách 1 lần cho mỗi chương trình. Mốc hạng: 2 triệu → Bạc, 5 triệu → Vàng, 10 triệu → VIP (tính khi đơn đã giao và thanh toán).
- Mã phải còn hạn và còn lượt dùng chung. Khách hủy đơn thì lượt dùng voucher được trả lại.
- Lượt dùng tính theo chương trình (nhóm mã), vì CSDL chỉ lưu đơn đã dùng chương trình nào chứ không lưu từng mã.

## Tổ chức mã nguồn

Luồng chung của cả hai web: đường dẫn `/{Controller}/{Hàm}/{id}` → Controller (thư mục `Controllers`) → lớp dữ liệu đọc / ghi MySQL →
View (`.cshtml` trong thư mục `Views`).

| Web | Đọc dữ liệu | Ghi dữ liệu |
|---|---|---|
| Quản trị | `Models/ShopData.cs` (nạp từng danh sách khi cần, mỗi request một bản) | `Models/ShopCommands.cs` qua `Controllers/CrudController.cs` (Form, Save, Delete, Act) |
| Khách hàng | `Services/ShopStore.cs`, `Services/CustomerStore.cs` | Cùng hai lớp trên |

- Biểu mẫu thêm / sửa của web quản trị được dựng tự động từ `Models/CrudDefs.cs`; JavaScript giao diện ở `wwwroot/js/admin.js`.
- Mỗi hàm chính có một dòng ghi chú ngay phía trên: hàm làm gì, lấy / ghi dữ liệu ở đâu (bảng MySQL, file, session) và gọi hàm nào, file nào.
  Không ghi chú từng dòng lệnh.

## Cơ sở dữ liệu

MySQL 8, 35 bảng, kèm dữ liệu demo. Bảng `KHACH_HANG` có cột `da_xoa` để xóa mềm khách hàng.
CSDL tạo trước khi có cột này thì thêm bằng lệnh (không mất dữ liệu):

```sql
ALTER TABLE KHACH_HANG ADD COLUMN da_xoa TINYINT(1) NOT NULL DEFAULT 0 AFTER tong_chi_tieu;
```

### Chạy lại script CSDL

Script không tự xóa CSDL cũ, nên chạy lần 2 sẽ báo lỗi 1007 (database exists). Trong cửa sổ Workbench (không cần lưu vào file),
thêm hai dòng này lên **đầu** script rồi chạy lại toàn bộ:

```sql
SET SQL_SAFE_UPDATES = 0;
DROP DATABASE IF EXISTS sports_shop;
```

`SET SQL_SAFE_UPDATES = 0;` cần vì Workbench mặc định bật Safe Updates (lỗi 1175) làm các lệnh tính lại tổng tiền và tồn kho cuối script
không chạy. Xóa CSDL sẽ mất mọi dữ liệu đã nhập thêm.

## Gặp lỗi

- **Web quản trị báo "Lỗi kết nối CSDL":** kiểm tra dịch vụ MySQL đang chạy (`services.msc`), đúng cổng và mật khẩu trong `appsettings.Local.json`.
  Máy có nhiều server MySQL (ví dụ cổng 3306 và 3307) thì cổng trong file phải đúng server đã chạy script.
  Web khách hàng không kết nối được thì các chức năng cần dữ liệu hiện thông báo cần kết nối cơ sở dữ liệu.
- **`Table '...' doesn't exist` hoặc `Unknown column 'da_xoa'`:** CSDL trên máy cũ hơn script, chạy lại script hoặc lệnh `ALTER TABLE` ở trên.
- **Mã nhân viên ở góc phải (ví dụ NV011) không đúng sau khi tạo lại CSDL:** mã này lưu trong cookie đăng nhập cũ; đăng xuất rồi đăng nhập lại.
  Nếu không, các thao tác ghi người thực hiện (lập đơn, phiếu nhập, trả lời phản hồi) sẽ báo lỗi.
- **Không đăng nhập được web quản trị, báo "Tài khoản khách hàng không được đăng nhập trang quản trị":** tài khoản chưa gắn với hồ sơ
  nhân viên trong bảng `NHAN_VIEN`.
- **Build báo không chép được file `.exe` / `.dll`:** web đang chạy giữ file; dừng web rồi build lại.

## Chưa làm

- App điện thoại cho khách hàng (`MobileApp`, .NET MAUI) và Web API cho app.
- Mã hóa mật khẩu (hiện lưu dạng văn bản theo thiết kế CSDL của nhóm).
- Tự áp dụng khuyến mãi theo sản phẩm và theo mốc đơn hàng ở bước thanh toán (hiện chỉ áp dụng mã voucher).
- Gửi chiến dịch thông báo thật qua email / SMS (hiện chỉ ghi nhận chiến dịch đã gửi).
- Tách lượt dùng voucher theo từng mã trong cùng một chương trình (cần thêm cột lưu mã voucher vào CSDL).
