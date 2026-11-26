-- Chỉ giữ 5 phương thức thanh toán cho web khách, app và Web Admin; các phương thức khác đặt trang_thai = 'ngung' thay vì xóa vì DON_HANG, HOA_DON cũ còn tham chiếu. Chạy lại được.
SET SQL_SAFE_UPDATES = 0;

INSERT IGNORE INTO PHUONG_THUC_THANH_TOAN(ten_phuong_thuc_thanh_toan, trang_thai) VALUES
 ('Thanh toán khi nhận hàng (COD)', 'hoat_dong'),
 ('Trả góp qua thẻ', 'hoat_dong'),
 ('Chuyển khoản ngân hàng', 'hoat_dong'),
 ('Ví điện tử (MoMo, ZaloPay)', 'hoat_dong'),
 ('Thẻ ATM / Visa / Mastercard', 'hoat_dong');

UPDATE PHUONG_THUC_THANH_TOAN
SET trang_thai = CASE WHEN ten_phuong_thuc_thanh_toan IN ('Thanh toán khi nhận hàng (COD)', 'Trả góp qua thẻ', 'Chuyển khoản ngân hàng',
                                                         'Ví điện tử (MoMo, ZaloPay)', 'Thẻ ATM / Visa / Mastercard')
                      THEN 'hoat_dong' ELSE 'ngung' END;
