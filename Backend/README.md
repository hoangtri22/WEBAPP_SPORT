# Backend

Nơi đặt cơ sở dữ liệu (và API sau này) của hệ thống SportShop CRM.

- `Database/sports_shop.sql`: script MySQL tạo CSDL `sports_shop` (35 bảng, thiết kế theo chương 4 của báo cáo) và dữ liệu demo.
- Hai web đọc CSDL này qua chuỗi kết nối `ConnectionStrings:Default` trong `appsettings.json`.
- `Images/`: ảnh sản phẩm. Cả hai web phục vụ thư mục này tại đường dẫn `/images`, nên cột `SAN_PHAM.hinh_anh`
  ghi dạng `/images/ten-anh.jpg` (tên file phải trùng khớp, kể cả chữ hoa/thường). Thêm ảnh mới chỉ cần chép vào thư mục này.
