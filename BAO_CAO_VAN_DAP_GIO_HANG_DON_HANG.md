# BÁO CÁO VẤN ĐÁP & HƯỚNG DẪN CHẠY ĐỒ ÁN CNPM
## Phân công: Phan Văn Thiện — Module Giỏ hàng, Thanh toán (Checkout) & Đơn hàng

Tài liệu này tổng hợp **chính xác từng file, class, hàm và số dòng code (line number)** cho phần phân công của **Phan Văn Thiện** (**Giỏ hàng và Đơn hàng** trên Mobile App đồng bộ 100% với bản Web khách hàng `SportShop.CustomerWeb`) cùng **7 điều kiện chấm điểm vấn đáp** của môn học.

---

## 1. Đối chiếu đồng bộ 100% giữa Web Khách hàng (`SportShop.CustomerWeb`) và Mobile App (`SportShop.Mobile`)

| Màn hình / Tính năng | Bản Web Khách hàng (`SportShop.CustomerWeb`) | Bản Mobile App (`SportShop.Mobile`) | API dùng chung (`SportShop.Api`) |
|---|---|---|---|
| **1. Giỏ hàng (`Cart`)** | `Views/Cart/Index.cshtml` & `Controllers/CartController.cs` | `CartPage.xaml`, `CartPage.xaml.cs` & `CartViewModel` (`ViewModels/CartAndCheckoutViewModel.cs`) | `GET/POST/PUT/DELETE /api/v1/cart` (`Controllers/CartController.cs`) |
| **2. Thanh toán (`Checkout`)** | `Views/Cart/Checkout.cshtml` | `CheckoutPage.xaml`, `CheckoutPage.xaml.cs` & `CartViewModel` | `GET /api/v1/checkout`, `POST /api/v1/checkout/quote` (`Controllers/CheckoutController.cs`) |
| **3. Đặt hàng thành công (`Done`)** | `Views/Cart/Done.cshtml` | Khối `HasPlacedOrder` trên `CheckoutPage.xaml` | `POST /api/v1/orders` (kèm `Idempotency-Key`) |
| **4. Đơn hàng của tôi (`Orders`)** | `Views/Customer/Orders.cshtml` & `Controllers/CustomerController.cs` | `OrdersPage.xaml`, `OrdersPage.xaml.cs` & `OrdersViewModel` (`ViewModels/OrdersAndStatsViewModel.cs`) | `GET /api/v1/orders`, `POST /api/v1/orders/{orderId}/cancel` (`Controllers/OrdersController.cs`) |

### Chi tiết các trạng thái đã đồng bộ y hệt Web:
1. **Khi chưa đăng nhập (chế độ Khách):** Hiển thị thông báo *"Vui lòng đăng nhập để xem giỏ hàng. Nếu chưa có tài khoản, hãy đăng ký."* kèm nút **Đăng nhập / Đăng ký** (chuyển về `AuthPage`), giống hệt `CartController.LoginRequired("xem giỏ hàng")` trên Web.
2. **Khi Giỏ hàng đang trống (`Items.Count == 0`):** Chỉ hiển thị thẻ *"Giỏ hàng đang trống — Hãy chọn vài món đồ bạn thích nhé."* và nút **Tiếp tục mua sắm** (ẩn hoàn toàn khung Tóm tắt đơn hàng ở dưới, giống hệt `Views/Cart/Index.cshtml` dòng 9–17).
3. **Khi Giỏ hàng có sản phẩm (`Items.Count > 0`):**
   - Hiển thị danh sách sản phẩm trong giỏ (ảnh, tên, phân loại `Size / Màu`, đơn giá, cảnh báo *"Chỉ còn X sản phẩm trong kho"* nếu vượt tồn, nút `−` / `+` chỉnh số lượng, thành tiền, nút `✕` bỏ khỏi giỏ).
   - Hiển thị khung **Tóm tắt đơn hàng** (`Tạm tính`, `Phí giao hàng`: miễn phí từ `500.000 ₫`, dưới mức là `30.000 ₫`, `Tổng cộng`, ghi chú *"Mã giảm giá được nhập ở bước thanh toán."*, nút **Tiến hành thanh toán** và **Tiếp tục mua sắm**).
4. **Màn hình Thanh toán (`CheckoutPage.xaml`):**
   - **Thông tin nhận hàng:** Họ và tên, Số điện thoại (`10 chữ số`), Địa chỉ giao hàng (tự điền mặc định từ sổ địa chỉ/hồ sơ khách hàng).
   - **Phương thức thanh toán:** Chọn phương thức thanh toán mô phỏng giống Web.
   - **Mã giảm giá:** Chọn *"Không dùng mã"* hoặc chọn một mã giảm giá hợp lệ của tài khoản (`Code`, tên chương trình, `% giảm`, hạn sử dụng, số tiền giảm `-X ₫`), tự động gọi `/api/v1/checkout/quote` tính lại tiền ngay khi chạm chọn.
   - **Đơn hàng ($N$ sản phẩm):** Liệt kê từng dòng `Tên SP (Size/Màu) × SL`, `Tạm tính`, `Giảm giá`, `Phí giao hàng`, `Tổng thanh toán`, nút **Đặt hàng** và **Quay lại giỏ hàng**.
   - **Đặt hàng thành công:** Hiển thị mã đơn `DHxxxx`, người nhận, danh sách món đã đặt, tổng thanh toán và 2 nút **Xem đơn hàng của tôi** / **Tiếp tục mua sắm**.
5. **Màn hình Đơn hàng của tôi (`OrdersPage.xaml`):**
   - Hiển thị từng thẻ đơn hàng với: Mã đơn · Ngày đặt, badge Trạng thái đơn & badge Thanh toán (`Đã thanh toán` / `Chưa thanh toán`).
   - Thanh tiến trình **4 bước** (`Chờ xác nhận` → `Đang chuẩn bị` → `Đang giao` → `Đã giao`) hoặc hộp cảnh báo đỏ `Đơn đã hủy`.
   - Danh sách các dòng sản phẩm trong đơn (`CHI_TIET_DON_HANG`), địa chỉ giao nhận, đơn vị vận chuyển/mã vận đơn, số hóa đơn, tổng tiền và nút **Hủy đơn** (chỉ hiện khi `CanCancel == true`, tức đơn đang `cho_xac_nhan` và `chua_thanh_toan`, có hộp thoại xác nhận *"Hủy đơn hàng DHxxxx?"*).

---

## 2. Trả lời 7 Điều kiện Vấn đáp CNPM (kèm Dòng code chính xác trong đồ án)

### Điều kiện 1: Dòng code nào cấu hình kết nối Database (`appsettings.json`) và chạy câu truy vấn SQL?
- **Khai báo chuỗi kết nối:**
  - `Backend/SportShop.Api/appsettings.Local.json` (**dòng 2–4**) và `Backend/SportShop.Api/Program.cs` (**dòng 19–22**).
- **Khởi tạo `MySqlConnection`:**
  - `Backend/SportShop.Shared/StoreDatabase.cs` (**dòng 9–27**, hàm `StoreDatabase.Connection(string connectionString)`).
- **Mở kết nối & thực thi câu truy vấn SQL cho Giỏ hàng và Đơn hàng:**
  - **Giỏ hàng (`GIO_HANG`, `CHI_TIET_GIO_HANG`):** `Frontend/SportShop.CustomerWeb/Services/CartStore.cs` (được link vào `SportShop.Shared` dùng chung cho Web & Mobile API):
    - `Lines(...)` (**dòng 53–83**), `Add(...)` (**dòng 92–126**), `Update(...)` (**dòng 128–150**), `Remove(...)` (**dòng 152–161**).
  - **Đặt đơn (`PlaceCartOrder` trong Transaction), Lịch sử đơn (`Orders`), Hủy đơn (`CancelOrder`):** `Frontend/SportShop.CustomerWeb/Services/ShopStore.cs`:
    - `PlaceCartOrder(...)` (**dòng 225–338**): khóa tồn kho `FOR UPDATE`, `INSERT INTO DON_HANG`, `INSERT INTO CHI_TIET_DON_HANG`, trừ tồn kho `BIEN_THE_SAN_PHAM`, tạo `HOA_DON`, xóa giỏ hàng và `tx.Commit()`.
    - `Orders(...)` (**dòng 356–420**): truy vấn đơn hàng kèm `HOA_DON`, `VAN_DON` và `CHI_TIET_DON_HANG`.
    - `CancelOrder(...)` (**dòng 422–474**): kiểm tra `trang_thai_don_hang = 'cho_xac_nhan'` và `trang_thai_thanh_toan = 'chua_thanh_toan'`, hoàn tồn kho, hoàn lượt dùng voucher và cập nhật sang `'da_huy'`.

### Điều kiện 2: Tính tái sử dụng biến bộ nhớ (10 phút) cho tìm kiếm hoặc thống kê
- **Trên Mobile App:**
  - `MobileApp/SportShop.Mobile/Services/AppMemoryCache.cs` (**dòng 8–55**, `DefaultMaxAge = TimeSpan.FromMinutes(10)`).
  - `MobileApp/SportShop.Mobile/ViewModels/OrdersAndStatsViewModel.cs` (`OrdersViewModel.LoadOrdersAsync` **dòng 250–292** & `ApplyFilter` **dòng 350–365**): Khi người dùng gõ $n$ từ khóa tìm kiếm đơn hàng liên tiếp trong 1 phút, app lọc trực tiếp trên biến bộ nhớ `_allOrders` trong RAM mà không chạy lại câu truy vấn SQL; sau 10 phút (hoặc khi đặt/hủy đơn) mới tải lại từ CSDL.
- **Trên Web (`SportShop.CustomerWeb` & `SportShop.Admin`):**
  - `Frontend/SportShop.CustomerWeb/Services/CatalogCache.cs` (**dòng 10–35**, `MaxAge = TimeSpan.FromMinutes(10)`).
  - `Frontend/SportShop.Admin/Models/DataCache.cs` (**dòng 11–145**, `MaxAge = TimeSpan.FromMinutes(10)`).

### Điều kiện 3: Thiết kế Main-Sub Form cho những form thường xuyên xem chi tiết
- **Trên Mobile App:** `OrdersPage.xaml` & `OrdersViewModel` (`Orders` là danh sách đơn hàng Main, mỗi đơn chứa danh sách dòng chi tiết `Lines` / `SelectedOrderLines` từ bảng `CHI_TIET_DON_HANG`).
- **Trên Web:** `Frontend/SportShop.Admin/Views/Orders/Detail.cshtml` (Main Form: thông tin đơn hàng & hóa đơn `DON_HANG`/`HOA_DON`; Sub Form: bảng các dòng sản phẩm `CHI_TIET_DON_HANG` và lịch sử vận đơn `VAN_DON`).

### Điều kiện 4: Tìm kiếm nâng cao (sử dụng toán tử quan hệ và toán tử logic)
- **Trên Mobile App:** `MobileApp/SportShop.Mobile/Services/AdvancedOrderSearch.cs` (**dòng 21–218**): Hỗ trợ các toán tử quan hệ (`=`, `≠`, `>`, `≥`, `<`, `≤`, `chứa`, `không chứa`, `bắt đầu bằng`) và toán tử logic (`Và (AND)` ưu tiên trước `Hoặc (OR)`).
- **Trên Web:** `Frontend/SportShop.Admin/wwwroot/js/search-kit.js` (**dòng 1–215**) và `Frontend/SportShop.Admin/Views/Shared/Table.cshtml` (**dòng 35–85**).

### Điều kiện 5: Thiết kế 5 bảng thống kê tổng tiền hóa đơn
- Được cài đặt tại `Frontend/SportShop.Admin/Controllers/StatsController.cs` (**dòng 14–165**) và `Views/Stats/Index.cshtml`:
  1. Tính tổng tiền hóa đơn từ ngày A đến ngày B (`kind = "range"`, **dòng 40–62**).
  2. Thống kê tổng tiền hóa đơn theo tháng/quý/năm (`kind = "period"`, **dòng 64–86**).
  3. Thống kê tổng tiền hóa đơn theo nhân viên và năm (`kind = "employee"`, **dòng 88–110**).
  4. Thống kê tổng tiền hóa đơn theo khách hàng và năm/quý (`kind = "customer"`, **dòng 112–135**).
  5. Thống kê tổng tiền hóa đơn theo sản phẩm và quý (`kind = "product"`, **dòng 137–160**).

### Điều kiện 6: Đọc / Ghi file Excel (`.xlsx`), trích xuất bảng thống kê ra file Excel
- Được cài đặt tại `Frontend/SportShop.Admin/Models/ShopCommands.cs`:
  - Ghi (Xuất) các bảng thống kê và dữ liệu ra file `.xlsx` chuẩn OpenXML: hàm `ExportXlsx(...)` & `BuildXlsx(...)` and `StatsController.Export(...)` (`Controllers/StatsController.cs` **dòng 167–195**).
  - Đọc (Nhập) file `.xlsx` vào hệ thống: hàm `ReadXlsx(...)` (`Models/ShopCommands.cs`).

### Điều kiện 7: Lập báo cáo và cho phép in ấn (PDF)
- Được cài đặt tại `Frontend/SportShop.Admin/Controllers/ReportsController.cs` (**dòng 12–120**) và `Views/Reports/Index.cshtml` (xuất báo cáo PDF / mở hộp thoại In ấn `window.print()`).
