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
│   └── SportShop.CustomerWeb/         # Web mua hàng cho khách hàng (ASP.NET Core Razor)
├── MobileApp/                         # App điện thoại cho khách hàng (.NET MAUI), chưa làm
├── Admin Dashboard for SPORTSHOP/     # Mã nguồn thiết kế Figma (React), chỉ để tham khảo giao diện
├── SportShop.slnx                     # Solution mở bằng Visual Studio
└── Báo cáo đồ án cuối kỳ.pdf
```

## Chức năng

### Web khách hàng (`Frontend/SportShop.CustomerWeb`, http://localhost:5003)
- **Tài khoản:** đăng ký, đăng nhập, đăng xuất, chỉnh sửa thông tin cá nhân (kèm sở thích thể thao).
- **Mua hàng:** trang chủ cửa hàng, danh sách sản phẩm (lọc theo loại, tìm kiếm, sắp xếp), chi tiết sản phẩm với chọn size / màu,
  giỏ hàng, thanh toán với mã giảm giá (thanh toán giả lập, miễn phí giao hàng từ 500.000₫), trang khuyến mãi.
- **Đơn hàng của tôi:** theo dõi trạng thái chờ xác nhận → đang chuẩn bị → đang giao → đã giao, tự hủy đơn khi còn chờ xác nhận.
  Khi đơn vừa chuyển "Đã giao" trong lúc khách đang xem, trang báo rồi quay về trang chủ.
- **Phản hồi và khảo sát:** gửi phản hồi về sản phẩm, làm khảo sát được cửa hàng gửi.
- Nút "Trang quản trị" chuyển sang web quản trị (địa chỉ cấu hình ở `appsettings.json`, mục `Links:AdminUrl`).

Mã nguồn chính: `Services/CustomerStore.cs` (tài khoản, phản hồi, khảo sát) và `Services/ShopStore.cs` (sản phẩm, giỏ hàng, đặt hàng, đơn hàng).
Giỏ hàng lưu trong session, không cần thêm bảng trong CSDL.

### Web quản trị (`Frontend/SportShop.Admin`, http://localhost:5188)
Đăng nhập bằng tài khoản quản lý hoặc nhân viên đang hoạt động. Các trang: tổng quan, tài khoản và phân quyền, khách hàng,
nhân viên, loại sản phẩm, sản phẩm, hóa đơn, khuyến mãi và marketing, phản hồi khách hàng, khảo sát, nhập hàng, báo cáo thống kê.
Các bảng có tìm kiếm, bộ lọc và phân trang. Nút thêm / xem / sửa / xóa mở biểu mẫu ở ngăn bên phải và ghi thẳng vào MySQL
(`Models/ShopCommands.cs`, `Controllers/CrudController.cs`).

- **Khách hàng:** thêm, sửa, xóa; khóa / mở khóa tài khoản; phân nhóm khách; ghi chú chăm sóc; xem mật khẩu (chỉ vai trò Quản lý).
- **Đơn hàng:** lập đơn tại quầy (trừ tồn kho, kiểm tra voucher) và duyệt đơn khách đặt online.
  - Đơn **chưa thanh toán** chỉ ở "Chờ xác nhận"; phải chuyển sang "Đã thanh toán" thì mới chọn được đang chuẩn bị / đang giao / đã giao / đã hủy.
  - Khi chuyển **"Đã giao"**: tự lập hóa đơn, cộng doanh thu và lợi nhuận vào báo cáo, cộng tổng chi tiêu, điểm tích lũy và nâng hạng thành viên cho khách.
  - **"Đã giao"** và **"Đã hủy"** là trạng thái cuối, không đổi được nữa.
  - Hủy đơn hoàn lại tồn kho; nếu khách đã thanh toán thì hoàn tiền, trừ khỏi doanh thu và tổng chi tiêu.
- **Sản phẩm và nhập hàng:** quản lý loại, sản phẩm, biến thể (size, màu, giá bán, giá vốn, tồn), nhà cung cấp; phiếu nhập cộng tồn kho và cập nhật giá vốn.
- **Khuyến mãi và marketing:** chương trình giảm giá theo sản phẩm / đơn hàng / voucher; chiến dịch thông báo gửi cho nhóm khách.
- **Phản hồi và khảo sát:** trả lời hoặc ẩn phản hồi; tạo, gửi, đóng khảo sát và xem thống kê kết quả.
- **Tài khoản và phân quyền:** thêm, sửa, khóa, xóa tài khoản; tài khoản và nhân viên chỉ vai trò Quản lý được thao tác.
- **Báo cáo:** doanh thu, lợi nhuận, sản phẩm bán chạy, khách hàng theo độ tuổi, sở thích khách hàng, hiệu suất nhân viên, tồn kho, hiệu quả khuyến mãi.

### Cơ sở dữ liệu (`Backend/Database/sports_shop.sql`)
MySQL 8, 35 bảng theo chương 4 của báo cáo, kèm dữ liệu demo. Script xóa và tạo lại toàn bộ CSDL `sports_shop` mỗi lần chạy,
nên có thể chạy lại bất kỳ lúc nào để đưa dữ liệu về như ban đầu.

### Chưa làm
- App điện thoại cho khách hàng (`MobileApp`, .NET MAUI) và Web API để app gọi vào.
- Mã hóa mật khẩu (hiện lưu dạng văn bản theo thiết kế của nhóm).
- Khuyến mãi theo sản phẩm và theo mốc đơn hàng chưa tự áp dụng ở bước thanh toán (hiện chỉ áp dụng được mã voucher).

## Cách chạy

Yêu cầu: Visual Studio có .NET 10 SDK và MySQL 8.

1. **Tạo CSDL:** mở MySQL Workbench, kết nối tới server, chọn File → Open SQL Script → `Backend/Database/sports_shop.sql`, bấm ⚡.
   Chỉ cần làm một lần (kết quả đúng là thấy schema `sports_shop` với 35 bảng).
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

Tài khoản demo (mật khẩu đều là `123456`):

| Web | Tài khoản |
|---|---|
| Web quản trị | `admin` (quản lý), `nv_lan`, `nv_tuan` (nhân viên) |
| Web khách hàng | `kh_an`, `kh_binh`, `kh_chi` (`kh_dung` bị khóa) |

Web quản trị không kết nối được MySQL sẽ hiện trang hướng dẫn khắc phục. Web khách hàng không kết nối được thì các chức năng cần dữ liệu
hiện thông báo cần kết nối cơ sở dữ liệu. Nếu MySQL đã cài nhưng web vẫn không nối được, kiểm tra dịch vụ MySQL đang chạy
(`services.msc`) và đúng cổng, mật khẩu trong `appsettings.Local.json`.
