-- Thêm các phương thức thanh toán mặc định vào PHUONG_THUC_THANH_TOAN; chạy lại được vì bỏ qua tên đã có.
INSERT IGNORE INTO PHUONG_THUC_THANH_TOAN(ten_phuong_thuc_thanh_toan,trang_thai) VALUES
 ('Thanh toán khi nhận hàng (COD)','hoat_dong'),
 ('Chuyển khoản ngân hàng','hoat_dong'),
 ('Ví điện tử (MoMo, ZaloPay)','hoat_dong'),
 ('Thẻ ATM / Visa / Mastercard','hoat_dong');