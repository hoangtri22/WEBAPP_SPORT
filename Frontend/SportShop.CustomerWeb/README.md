# Web bán hàng

Web mua hàng cho khách (ASP.NET Core MVC, .NET 10). Chuẩn bị CSDL và các bước chạy chi tiết: [README tổng](../../README.md).

## Chức năng

| Nhóm | Làm được gì |
|---|---|
| Sản phẩm | Trang chủ, danh sách, tìm kiếm và lọc nâng cao (giá, kích cỡ, màu, loại trừ), chi tiết theo biến thể, trang khuyến mãi |
| Giỏ hàng | Thêm nhanh từ thẻ sản phẩm, sửa số lượng theo tồn kho, gợi ý mua thêm để đạt mức giảm giá |
| Thanh toán | Địa chỉ nhận, phương thức thanh toán (giả lập), voucher, tự áp mức giảm theo đơn tốt nhất |
| Đơn hàng | Theo dõi trạng thái, tự hủy đơn chờ xác nhận chưa thanh toán, đánh giá sản phẩm đã nhận |
| Tài khoản | Đăng ký, đăng nhập, hồ sơ, sở thích, đổi mật khẩu, phản hồi, khảo sát |
| Thông tin | Đổi trả, giao hàng, chọn size, liên hệ, về chúng tôi, hệ thống cửa hàng, tuyển dụng |

**Hai cách mua hàng:**
- **Có tài khoản:** dùng được voucher, xem đơn ở "Đơn hàng của tôi".
- **Không có tài khoản:** không cần đăng nhập hay đăng ký, đơn lưu theo số điện thoại, không dùng voucher, tra cứu đơn ở `/Track` (số đã thuộc tài khoản thì đăng nhập để xem). Đăng nhập thì giỏ của khách được gộp vào giỏ tài khoản.
- **Đồng bộ khi đăng ký (web và app):** `GuestHistory` gộp mọi đơn đã mua bằng số đó trên web, app, tại quầy cùng chi tiêu, điểm, hạng vào tài khoản; địa chỉ của đơn giao gần nhất thành địa chỉ mặc định. Đơn mua không đăng nhập sau này bằng số của tài khoản được gộp ở lần đăng nhập kế tiếp. Tài khoản Google đồng bộ khi nhập số lần đầu.

**Quy tắc chính:**
- Phí giao hàng 30.000đ, miễn phí từ 500.000đ.
- Nhập sai mật khẩu 5 lần thì khóa 5 phút. Phiên web hết hạn sau 2 giờ không thao tác. Mỗi tab trình duyệt giữ một tài khoản riêng.
- Giỏ hàng lưu ở `CUSTOMER_CART_ITEM`, dùng chung với app.
- Danh mục sản phẩm lưu đệm 10 phút; Web Admin lưu dữ liệu hoặc khách đặt, hủy đơn trên web này thì nạp lại ngay.
- Chưa kết nối CSDL thì trang chủ dùng dữ liệu mẫu, chức năng cần dữ liệu hiện thông báo.

## Cấu hình

Đọc lần lượt `appsettings.json`, `appsettings.Development.json`, `appsettings.Local.json` (git bỏ qua) rồi biến môi trường; nguồn sau ghi đè nguồn trước.

| Khóa | Ý nghĩa | Mặc định |
|---|---|---|
| `ConnectionStrings:Default` | Kết nối MySQL; để trống là chạy không có CSDL | – |
| `Links:AdminUrl` | Địa chỉ nút "Trang quản trị" | `http://localhost:5188` |
| `Paths:Images` | Thư mục ảnh sản phẩm | `../../Backend/Images` |
| `Paths:Uploads` | Thư mục ảnh tải lên | `../Uploads` |
| `Commerce:LegacyCartPath` | File giỏ hàng JSON cũ, nhập một lần theo khách | `App_Data/carts.json` |

## Cách chạy

```powershell
dotnet run --project Frontend/SportShop.CustomerWeb --launch-profile http
```

Chạy tại thư mục `WEBAPP_SPORT`, rồi mở http://localhost:5003. Profile `https` dùng thêm cổng 7258.

## Cây thư mục

`Services/` và `Models/` được biên dịch trong `SportShop.Shared` để API dùng chung, nên sửa ở đây sẽ ảnh hưởng cả API.

```text
SportShop.CustomerWeb/
├── Program.cs                  # Khởi tạo, session, ảnh tĩnh
├── appsettings.json            # Cấu hình mặc định
├── Properties/launchSettings.json # Cổng chạy
├── Controllers/
│   ├── HomeController.cs       # Trang chủ, trang lỗi
│   ├── ShopController.cs       # Danh sách, chi tiết sản phẩm, khuyến mãi
│   ├── CartController.cs       # Giỏ hàng, thanh toán, đặt hàng
│   ├── TrackController.cs      # Tra cứu đơn bằng số điện thoại
│   ├── CustomerController.cs   # Hồ sơ, đơn hàng, phản hồi, khảo sát
│   ├── AccountController.cs    # Đăng ký, đăng nhập, đăng xuất
│   ├── InfoController.cs       # Trang thông tin
│   ├── CustomerSessionFilter.cs # Kiểm tra tài khoản còn hoạt động
│   └── LayoutDataFilter.cs     # Dữ liệu chung của khung trang
├── Services/
│   ├── ShopStore.cs            # Sản phẩm, voucher, đặt và hủy đơn
│   ├── CustomerStore.cs        # Tài khoản, hồ sơ, phản hồi, khảo sát
│   ├── CartStore.cs            # Giỏ hàng trong MySQL
│   ├── GuestStore.cs           # Khách mua không cần tài khoản
│   ├── GuestHistory.cs         # Đồng bộ lịch sử mua theo số điện thoại vào tài khoản
│   ├── CatalogCache.cs         # Bộ nhớ đệm danh mục
│   ├── ProductReviewStore.cs   # Đánh giá công khai cho API
│   ├── LoginThrottle.cs        # Giới hạn đăng nhập sai
│   ├── PasswordStorage.cs      # Mã hóa và kiểm tra mật khẩu
│   ├── DbStatus.cs             # Kiểm tra kết nối CSDL
│   └── TabCookies.cs           # Mỗi tab một phiên đăng nhập
├── Models/                     # Kiểu dữ liệu, quy tắc voucher và hạng, nội dung trang thông tin
├── Views/
│   ├── Shop/, Cart/, Track/    # Mua hàng
│   ├── Customer/, Account/     # Tài khoản
│   ├── Home/, Info/            # Trang chủ, thông tin
│   └── Shared/                 # Khung trang, đầu trang, chân trang, tìm kiếm
└── wwwroot/
    ├── css/                    # shop.css, shop-store.css
    └── js/                     # shop.js, tab-session.js
```
