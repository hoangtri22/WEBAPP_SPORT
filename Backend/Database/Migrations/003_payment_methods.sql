-- Provision the same simulated methods previously lazily inserted by the Web checkout GET.
-- INSERT IGNORE preserves existing IDs, names, status, and orders.
INSERT IGNORE INTO PHUONG_THUC_THANH_TOAN(ten_phuong_thuc_thanh_toan,trang_thai) VALUES
 ('Thanh toán khi nhận hàng (COD)','hoat_dong'),
 ('Chuyển khoản ngân hàng','hoat_dong'),
 ('Ví điện tử (MoMo, ZaloPay)','hoat_dong'),
 ('Thẻ ATM / Visa / Mastercard','hoat_dong');
