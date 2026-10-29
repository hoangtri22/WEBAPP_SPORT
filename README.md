# SportShop CRM

Đồ án môn Hệ thống thông tin doanh nghiệp (nhóm 10, Đại học Sài Gòn):
**Xây dựng hệ thống quản lý khách hàng (CRM) cho doanh nghiệp bán đồ thể thao.**

Hệ thống gồm một cơ sở dữ liệu MySQL dùng chung, một **web quản trị** cho quản lý và nhân viên, và một **web khách hàng**
để khách xem sản phẩm, mua hàng và gửi phản hồi. App điện thoại cho khách hàng (.NET MAUI) sẽ làm sau.
Phân tích và thiết kế cơ sở dữ liệu nằm trong file `Báo cáo đồ án cuối kỳ.pdf`.

## Cấu trúc thư mục

```
SportShop/
├── Backend/
│   ├── Database/sports_shop.sql       # Script MySQL: tạo CSDL sports_shop (35 bảng) + dữ liệu demo
│   └── Images/                        # Ảnh sản phẩm, hai web phục vụ tại đường dẫn /images
├── Frontend/
│   ├── SportShop.Admin/               # Web quản trị (ASP.NET Core MVC, .NET 10)
│   ├── SportShop.CustomerWeb/         # Web mua hàng cho khách hàng (ASP.NET Core Razor)
│   └── Uploads/                       # Ảnh tải lên từ web quản trị, hai web phục vụ tại /uploads (git bỏ qua)
├── MobileApp/                         # App điện thoại cho khách hàng (.NET MAUI), chưa làm
├── Admin Dashboard for SPORTSHOP/     # Mã nguồn thiết kế Figma (React), chỉ để tham khảo giao diện
├── SportShop.slnx                     # Solution mở bằng Visual Studio
└── Báo cáo đồ án cuối kỳ.pdf
```

## Chức năng

### Web khách hàng (`Frontend/SportShop.CustomerWeb`, http://localhost:5003)
- **Tài khoản:** đăng ký, đăng nhập, đăng xuất, chỉnh sửa thông tin cá nhân (kèm sở thích thể thao) và **tự đổi mật khẩu**
  (nhập mật khẩu hiện tại, mật khẩu mới, nhập lại). Mật khẩu mới được web quản trị thấy ngay.
- **Mua hàng:** trang chủ cửa hàng, danh sách sản phẩm (lọc theo loại, tìm kiếm, sắp xếp), chi tiết sản phẩm với chọn size / màu,
  giỏ hàng, thanh toán, trang khuyến mãi. Số sản phẩm ở từng danh mục trang chủ lấy theo dữ liệu thật.
  Chỉ hiện sản phẩm đang bán và còn tồn kho; sản phẩm hết hàng hoặc bị ẩn không hiện ở cửa hàng.
  - **Ảnh theo biến thể:** mỗi biến thể (màu) có ảnh riêng (`BIEN_THE_SP.hinh_anh`); chọn màu nào thì ảnh lớn ở trang chi tiết đổi theo màu đó.
  - **Giỏ hàng riêng cho từng tài khoản:** phải đăng nhập mới thêm vào giỏ được; giỏ được lưu lại giữa các lần đăng nhập
    (file `Frontend/SportShop.CustomerWeb/App_Data/carts.json`, git bỏ qua, tự tạo khi chạy).
  - **Thanh toán:** nhận hàng điền sẵn từ sổ địa chỉ, phương thức thanh toán giả lập, miễn phí giao hàng từ 500.000₫.
    Mục **Mã giảm giá** là danh sách chọn: mỗi mã một dòng kèm số tiền sẽ được giảm theo đơn hiện tại, chọn mã nào thì tổng tiền tính lại ngay.
    Danh sách chỉ hiện các mã khách này dùng được (xem mục *Quy tắc voucher* bên dưới).
- **Đơn hàng của tôi:** theo dõi trạng thái chờ xác nhận → đang chuẩn bị → đang giao → đã giao, tự hủy đơn khi còn chờ xác nhận.
  Khi đơn vừa chuyển "Đã giao" trong lúc khách đang xem, trang báo rồi quay về trang chủ. Đơn đã giao có mục đánh giá các sản phẩm trong đơn.
  Lịch sử mua hàng vẫn giữ nguyên kể cả khi sản phẩm bị ẩn hoặc loại sản phẩm bị xóa.
- **Phản hồi và khảo sát:** chỉ gửi được phản hồi cho sản phẩm đã mua và đã nhận hàng; làm khảo sát được cửa hàng gửi.
  Phản hồi đã có câu trả lời của cửa hàng luôn hiện "Đã trả lời".
- **Trang thông tin:** chính sách đổi trả, giao hàng, hướng dẫn chọn size, liên hệ, về chúng tôi, hệ thống cửa hàng, tuyển dụng
  (nội dung ở `Models/InfoPages.cs`).
- **Bảo mật:** sai mật khẩu 5 lần thì khóa đăng nhập tạm 5 phút (`Services/LoginThrottle.cs`), so sánh mật khẩu theo thời gian không đổi,
  cookie gắn SameSite và các header chống nhúng trang / đoán kiểu file.
- Nút "Trang quản trị" chuyển sang web quản trị (địa chỉ cấu hình ở `appsettings.json`, mục `Links:AdminUrl`).

Mã nguồn chính: `Services/CustomerStore.cs` (tài khoản, phản hồi, khảo sát), `Services/ShopStore.cs` (sản phẩm, giỏ hàng, voucher, đặt hàng, đơn hàng)
và `Services/CartStore.cs` (giỏ hàng từng tài khoản).

### Web quản trị (`Frontend/SportShop.Admin`, http://localhost:5188)
Đăng nhập bằng tài khoản quản lý hoặc nhân viên đang hoạt động (tài khoản phải gắn với một hồ sơ nhân viên). Các trang: tổng quan,
tài khoản và phân quyền, khách hàng, nhân viên, loại sản phẩm, sản phẩm, nhà cung cấp, nhập hàng, khuyến mãi và marketing,
đơn đặt hàng và giao hàng, hóa đơn, phản hồi khách hàng, khảo sát, báo cáo thống kê.
Các bảng có tìm kiếm, bộ lọc và phân trang. Nút thêm / xem / sửa / xóa mở biểu mẫu ở ngăn bên phải và ghi thẳng vào MySQL
(`Models/ShopCommands.cs`, `Controllers/CrudController.cs`). Dữ liệu đọc từ `Models/ShopData.cs`.

- **Khách hàng:** thêm, xóa; sửa mọi thông tin (họ tên, giới tính, ngày sinh, điện thoại, email, địa chỉ giao hàng, sở thích) trừ mật khẩu;
  khóa / mở khóa tài khoản; phân nhóm khách; ghi chú chăm sóc. Mật khẩu do khách tự đổi, admin chỉ xem lại giá trị hiện có (chỉ vai trò Quản lý).
  Khách chưa chi tiêu đồng nào luôn là hạng Thường.
- **Đơn hàng:** lập đơn tại quầy (trừ tồn kho, kiểm tra voucher) và duyệt đơn khách đặt online.
  - Rê chuột vào một dòng trong danh sách **đơn hàng / hóa đơn** thì chi tiết hiện ở khung bên phải (màn hình rộng từ 1200px; màn hình hẹp
    dưới 1600px thì thanh menu tự thu gọn trong lúc xem). Bấm nút con mắt (đơn hàng) hoặc "Mở trang chi tiết" để mở trang đầy đủ.
  - Trong khung bên phải (và trang chi tiết) cập nhật được **trạng thái thanh toán** (Chưa / Đã thanh toán) và **trạng thái đơn** cùng lúc;
    chọn "Đã hủy" thì hiện ô nhập **lý do hủy** (bắt buộc).
  - Đơn **chưa thanh toán** chỉ ở "Chờ xác nhận" / "Đang chuẩn bị" / "Đang giao"; phải chuyển sang "Đã thanh toán" thì mới chọn được "Đã giao" hoặc "Đã hủy".
  - Khi chuyển **"Đã giao"** (và đã thanh toán): tự lập hóa đơn, cộng doanh thu và lợi nhuận vào báo cáo, cộng tổng chi tiêu, điểm tích lũy
    và nâng hạng thành viên cho khách.
  - **"Đã giao"** và **"Đã hủy"** là trạng thái cuối, không đổi được nữa.
  - Hủy đơn hoàn lại tồn kho; nếu khách đã thanh toán thì hoàn tiền, trừ khỏi doanh thu và tổng chi tiêu; **voucher đã dùng cho đơn được hoàn lại** cho khách.
- **Hóa đơn:** danh sách hóa đơn (không sửa được để giữ số liệu đối soát), xem chi tiết và in hóa đơn.
- **Sản phẩm và nhập hàng:** quản lý loại, sản phẩm, biến thể (size, màu, giá bán, giá vốn, tồn, ảnh), nhà cung cấp; phiếu nhập cộng tồn kho
  và cập nhật giá vốn.
  - Ảnh gắn với **từng biến thể**: chọn ảnh có sẵn hoặc **tải ảnh từ thiết bị** (JPG, PNG, WEBP, GIF, tối đa 5 MB, lưu ở `Frontend/Uploads`).
  - Phiếu nhập hàng chọn được cả sản phẩm chưa có biến thể (nhập thêm giá bán, hệ thống tự tạo biến thể mặc định); danh sách phiếu hiện
    gọn các mặt hàng đã nhập (3 mặt hàng đầu, còn lại "+ N mặt hàng khác").
  - **Xóa loại sản phẩm:** sản phẩm trong loại bị xóa; sản phẩm đã có lịch sử (đơn hàng, phiếu nhập, khuyến mãi, phản hồi) được ẩn thay vì xóa hẳn
    (chuyển sang loại ẩn "[Đã xóa]") để lịch sử mua hàng và báo cáo không mất.
- **Khuyến mãi và marketing:** chương trình giảm giá theo sản phẩm / đơn hàng / voucher; chiến dịch thông báo gửi cho nhóm khách.
- **Phản hồi và khảo sát:** trả lời hoặc ẩn phản hồi; tạo, gửi, đóng khảo sát và xem thống kê kết quả.
- **Tài khoản và phân quyền:** thêm, sửa, khóa, xóa tài khoản; tài khoản và nhân viên chỉ vai trò Quản lý được thao tác.
- **Báo cáo:** doanh thu, lợi nhuận, sản phẩm bán chạy, khách hàng theo độ tuổi, sở thích khách hàng, hiệu suất nhân viên, tồn kho, hiệu quả khuyến mãi.

### Quy tắc voucher
Áp dụng giống nhau ở web khách hàng và khi lập đơn tại quầy:

- **Mã cho khách mới** (chương trình "Voucher chào bạn mới": `WELCOME5`, `WELCOME10`, `SPORT20`, `NEWBIE15`): chỉ dùng được ở **lần mua đầu tiên**
  của tài khoản (chưa có đơn nào, đơn đã hủy không tính) và chỉ **một mã** trong nhóm. Từ lần mua thứ 2 không dùng được nữa.
  Nhận biết qua tên chương trình có chữ "bạn mới", "khách mới", "người mới" hoặc "thành viên mới".
- **Mã theo hạng thành viên** (Bạc: `BAC8`, `BAC15`, `SINHNHATBAC`; Vàng: `VANG5`, `VANG10`, `VANG15`): chỉ khách **đúng hạng** mới dùng được,
  mỗi khách dùng 1 lần cho mỗi nhóm mã. Khách lên hạng mới thì dùng được mã của hạng mới (hạng được nâng khi đơn đã giao và thanh toán,
  với mốc chi tiêu 2 triệu → Bạc, 5 triệu → Vàng, 10 triệu → VIP).
- Mã còn hạn, còn lượt dùng chung của chương trình. Khách **hủy đơn** thì voucher được hoàn lại (trả lượt dùng của khách và lượt dùng chung)
  để dùng ở lần mua sau.
- Lượt dùng được tính theo chương trình (nhóm mã), vì CSDL chỉ lưu đơn đã dùng chương trình nào chứ không lưu từng mã.

### Cơ sở dữ liệu (`Backend/Database/sports_shop.sql`)
MySQL 8, 35 bảng theo chương 4 của báo cáo, kèm dữ liệu demo. Script tạo CSDL `sports_shop` và nạp dữ liệu; để chạy lại từ đầu thì phải xóa CSDL cũ
(xem mục *Chạy lại script CSDL* bên dưới). Phần Backend do thành viên phụ trách CSDL quản lý, hai web chỉ đọc / ghi dữ liệu, không sửa script.

### Chưa làm
- App điện thoại cho khách hàng (`MobileApp`, .NET MAUI) và Web API để app gọi vào.
- Mã hóa mật khẩu (hiện lưu dạng văn bản theo thiết kế của nhóm, nên admin xem được mật khẩu khách).
- Khuyến mãi theo sản phẩm và theo mốc đơn hàng chưa tự áp dụng ở bước thanh toán (hiện chỉ áp dụng được mã voucher).
- Gửi chiến dịch thông báo tới khách: hiện chỉ ghi nhận chiến dịch đã gửi, chưa gửi email / SMS thật hay hiện thông báo ở web khách.
- Lượt dùng voucher chưa tách theo từng mã trong cùng một chương trình (cần thêm cột lưu mã voucher vào CSDL).

## Cách chạy

Yêu cầu: Visual Studio có .NET 10 SDK và MySQL 8.

1. **Tạo CSDL:** mở MySQL Workbench, kết nối tới server, chọn File → Open SQL Script → `Backend/Database/sports_shop.sql`, bấm ⚡
   (Ctrl + Shift + Enter, chạy cả script). Kết quả đúng là thấy schema `sports_shop` với 35 bảng.
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
   Sau khi sửa code C# hoặc file `.cshtml` thì dừng và chạy lại; sửa CSS / JS thì chỉ cần Ctrl + F5 trên trình duyệt.

| Dự án | Địa chỉ (profile http) |
|---|---|
| Web quản trị | http://localhost:5188 |
| Web khách hàng | http://localhost:5003 |

Tài khoản demo (mật khẩu đều là `123456`):

| Web | Tài khoản |
|---|---|
| Web quản trị | `admin` (quản lý), `nv_lan`, `nv_tuan` (nhân viên) |
| Web khách hàng | `kh_an` (Vàng), `kh_khanh` (Bạc), `kh_binh`, `kh_mai` (Thường), `kh_dung` bị khóa |

Muốn thử mã cho khách mới thì đăng ký một tài khoản mới ở web khách hàng rồi đặt đơn đầu tiên.

### Chạy lại script CSDL
Script không tự xóa CSDL cũ, nên chạy lần 2 sẽ báo lỗi 1007 (database exists). Trong cửa sổ Workbench (không cần lưu vào file), thêm hai dòng này
lên **đầu** script rồi chạy lại toàn bộ:

```sql
SET SQL_SAFE_UPDATES = 0;
DROP DATABASE IF EXISTS sports_shop;
```

Dòng `SET SQL_SAFE_UPDATES = 0;` cần vì Workbench mặc định bật Safe Updates (lỗi 1175) làm các lệnh tính lại tổng tiền và tồn kho cuối script không chạy,
dẫn đến tổng tiền hoặc tồn kho bằng 0. Việc xóa CSDL sẽ mất mọi dữ liệu đã nhập thêm.

### Gặp lỗi kết nối
- Web quản trị không kết nối được MySQL sẽ hiện trang hướng dẫn khắc phục. Web khách hàng không kết nối được thì các chức năng cần dữ liệu
  hiện thông báo cần kết nối cơ sở dữ liệu.
- Kiểm tra dịch vụ MySQL đang chạy (`services.msc`), đúng cổng và mật khẩu trong `appsettings.Local.json`.
  Máy có nhiều server MySQL (ví dụ cổng 3306 và 3307) thì cổng trong file phải đúng server đã chạy script.
- Thông báo kiểu `Table '...' doesn't exist` nghĩa là CSDL trên máy chưa đủ 35 bảng: chạy lại script theo mục trên.
- Không đăng nhập được web quản trị dù đúng mật khẩu, báo "Tài khoản khách hàng không được đăng nhập trang quản trị": tài khoản chưa gắn với hồ sơ
  nhân viên trong bảng `NHAN_VIEN`, thường do CSDL được tạo từ một bản script khác với bản trong repo.
