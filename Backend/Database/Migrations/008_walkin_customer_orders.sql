-- Khách chưa có tài khoản phải có đơn hàng: lập đơn mua tại cửa hàng (đã giao, đã thanh toán, có hóa đơn) cho 4 khách đang thiếu đơn, trừ tồn kho và cộng tổng chi tiêu, điểm. Khách đã có đơn thì bỏ qua nên chạy lại vẫn an toàn.

SET @kh = (SELECT k.ma_khach_hang FROM KHACH_HANG k WHERE k.so_dien_thoai = '0912000005' AND k.ma_tai_khoan IS NULL AND k.da_xoa = 0
           AND NOT EXISTS (SELECT 1 FROM DON_HANG d WHERE d.ma_khach_hang = k.ma_khach_hang) LIMIT 1);
SET @bt = 32, @ngay = '2026-09-19 10:24:00', @nv = 2, @pt = 1, @dh = NULL, @hd = NULL;
INSERT INTO DON_HANG (ma_khach_hang, ma_nhan_vien, ngay_dat, ten_nguoi_nhan, sdt_nguoi_nhan, dia_chi_giao_hang, tam_tinh, tong_tien_giam_gia, tong_thanh_toan,
                      ma_phuong_thuc_thanh_toan, trang_thai_thanh_toan, trang_thai_don_hang, ngay_cap_nhat)
SELECT k.ma_khach_hang, @nv, @ngay, CONCAT(k.ho_lot, ' ', k.ten), k.so_dien_thoai, 'Mua tại cửa hàng', b.gia_ban, 0, b.gia_ban, @pt, 'da_thanh_toan', 'da_giao', @ngay
FROM KHACH_HANG k JOIN BIEN_THE_SP b ON b.ma_bien_the = @bt WHERE k.ma_khach_hang = @kh;
SET @dh = IF(ROW_COUNT() > 0, LAST_INSERT_ID(), NULL);
INSERT INTO CHI_TIET_DON_HANG (ma_don_hang, ma_bien_the, so_luong, don_gia, giam_gia, thanh_tien)
SELECT @dh, ma_bien_the, 1, gia_ban, 0, gia_ban FROM BIEN_THE_SP WHERE ma_bien_the = @bt AND @dh IS NOT NULL;
UPDATE BIEN_THE_SP SET so_luong_ton = so_luong_ton - 1 WHERE ma_bien_the = @bt AND @dh IS NOT NULL;
SET @so = CONCAT('HD', LPAD((SELECT IFNULL(MAX(ma_hoa_don), 0) + 1 FROM HOA_DON), 5, '0'));
INSERT INTO HOA_DON (so_hoa_don, ma_don_hang, ma_phuong_thuc_thanh_toan, ngay_thanh_toan, ngay_xuat, tong_tien, tong_tien_giam_gia, phi_van_chuyen, tong_thanh_toan)
SELECT @so, ma_don_hang, @pt, @ngay, @ngay, tam_tinh, 0, 0, tong_thanh_toan FROM DON_HANG WHERE ma_don_hang = @dh;
SET @hd = IF(ROW_COUNT() > 0, LAST_INSERT_ID(), NULL);
INSERT INTO CHI_TIET_HOA_DON (ma_hoa_don, ma_bien_the, so_luong, don_gia, gia_von, giam_gia, thanh_tien)
SELECT @hd, ma_bien_the, 1, gia_ban, gia_von, 0, gia_ban FROM BIEN_THE_SP WHERE ma_bien_the = @bt AND @hd IS NOT NULL;
UPDATE KHACH_HANG k JOIN DON_HANG d ON d.ma_don_hang = @dh
SET k.tong_chi_tieu = k.tong_chi_tieu + d.tong_thanh_toan, k.diem_tich_luy = k.diem_tich_luy + FLOOR(d.tong_thanh_toan / 10000)
WHERE k.ma_khach_hang = @kh;

SET @kh = (SELECT k.ma_khach_hang FROM KHACH_HANG k WHERE k.so_dien_thoai = '0703334938' AND k.ma_tai_khoan IS NULL AND k.da_xoa = 0
           AND NOT EXISTS (SELECT 1 FROM DON_HANG d WHERE d.ma_khach_hang = k.ma_khach_hang) LIMIT 1);
SET @bt = 36, @ngay = '2026-10-03 16:12:00', @nv = 8, @pt = 2, @dh = NULL, @hd = NULL;
INSERT INTO DON_HANG (ma_khach_hang, ma_nhan_vien, ngay_dat, ten_nguoi_nhan, sdt_nguoi_nhan, dia_chi_giao_hang, tam_tinh, tong_tien_giam_gia, tong_thanh_toan,
                      ma_phuong_thuc_thanh_toan, trang_thai_thanh_toan, trang_thai_don_hang, ngay_cap_nhat)
SELECT k.ma_khach_hang, @nv, @ngay, CONCAT(k.ho_lot, ' ', k.ten), k.so_dien_thoai, 'Mua tại cửa hàng', b.gia_ban, 0, b.gia_ban, @pt, 'da_thanh_toan', 'da_giao', @ngay
FROM KHACH_HANG k JOIN BIEN_THE_SP b ON b.ma_bien_the = @bt WHERE k.ma_khach_hang = @kh;
SET @dh = IF(ROW_COUNT() > 0, LAST_INSERT_ID(), NULL);
INSERT INTO CHI_TIET_DON_HANG (ma_don_hang, ma_bien_the, so_luong, don_gia, giam_gia, thanh_tien)
SELECT @dh, ma_bien_the, 1, gia_ban, 0, gia_ban FROM BIEN_THE_SP WHERE ma_bien_the = @bt AND @dh IS NOT NULL;
UPDATE BIEN_THE_SP SET so_luong_ton = so_luong_ton - 1 WHERE ma_bien_the = @bt AND @dh IS NOT NULL;
SET @so = CONCAT('HD', LPAD((SELECT IFNULL(MAX(ma_hoa_don), 0) + 1 FROM HOA_DON), 5, '0'));
INSERT INTO HOA_DON (so_hoa_don, ma_don_hang, ma_phuong_thuc_thanh_toan, ngay_thanh_toan, ngay_xuat, tong_tien, tong_tien_giam_gia, phi_van_chuyen, tong_thanh_toan)
SELECT @so, ma_don_hang, @pt, @ngay, @ngay, tam_tinh, 0, 0, tong_thanh_toan FROM DON_HANG WHERE ma_don_hang = @dh;
SET @hd = IF(ROW_COUNT() > 0, LAST_INSERT_ID(), NULL);
INSERT INTO CHI_TIET_HOA_DON (ma_hoa_don, ma_bien_the, so_luong, don_gia, gia_von, giam_gia, thanh_tien)
SELECT @hd, ma_bien_the, 1, gia_ban, gia_von, 0, gia_ban FROM BIEN_THE_SP WHERE ma_bien_the = @bt AND @hd IS NOT NULL;
UPDATE KHACH_HANG k JOIN DON_HANG d ON d.ma_don_hang = @dh
SET k.tong_chi_tieu = k.tong_chi_tieu + d.tong_thanh_toan, k.diem_tich_luy = k.diem_tich_luy + FLOOR(d.tong_thanh_toan / 10000)
WHERE k.ma_khach_hang = @kh;

SET @kh = (SELECT k.ma_khach_hang FROM KHACH_HANG k WHERE k.so_dien_thoai = '0989659519' AND k.ma_tai_khoan IS NULL AND k.da_xoa = 0
           AND NOT EXISTS (SELECT 1 FROM DON_HANG d WHERE d.ma_khach_hang = k.ma_khach_hang) LIMIT 1);
SET @bt = 24, @ngay = '2026-10-17 09:40:00', @nv = 2, @pt = 1, @dh = NULL, @hd = NULL;
INSERT INTO DON_HANG (ma_khach_hang, ma_nhan_vien, ngay_dat, ten_nguoi_nhan, sdt_nguoi_nhan, dia_chi_giao_hang, tam_tinh, tong_tien_giam_gia, tong_thanh_toan,
                      ma_phuong_thuc_thanh_toan, trang_thai_thanh_toan, trang_thai_don_hang, ngay_cap_nhat)
SELECT k.ma_khach_hang, @nv, @ngay, CONCAT(k.ho_lot, ' ', k.ten), k.so_dien_thoai, 'Mua tại cửa hàng', b.gia_ban, 0, b.gia_ban, @pt, 'da_thanh_toan', 'da_giao', @ngay
FROM KHACH_HANG k JOIN BIEN_THE_SP b ON b.ma_bien_the = @bt WHERE k.ma_khach_hang = @kh;
SET @dh = IF(ROW_COUNT() > 0, LAST_INSERT_ID(), NULL);
INSERT INTO CHI_TIET_DON_HANG (ma_don_hang, ma_bien_the, so_luong, don_gia, giam_gia, thanh_tien)
SELECT @dh, ma_bien_the, 1, gia_ban, 0, gia_ban FROM BIEN_THE_SP WHERE ma_bien_the = @bt AND @dh IS NOT NULL;
UPDATE BIEN_THE_SP SET so_luong_ton = so_luong_ton - 1 WHERE ma_bien_the = @bt AND @dh IS NOT NULL;
SET @so = CONCAT('HD', LPAD((SELECT IFNULL(MAX(ma_hoa_don), 0) + 1 FROM HOA_DON), 5, '0'));
INSERT INTO HOA_DON (so_hoa_don, ma_don_hang, ma_phuong_thuc_thanh_toan, ngay_thanh_toan, ngay_xuat, tong_tien, tong_tien_giam_gia, phi_van_chuyen, tong_thanh_toan)
SELECT @so, ma_don_hang, @pt, @ngay, @ngay, tam_tinh, 0, 0, tong_thanh_toan FROM DON_HANG WHERE ma_don_hang = @dh;
SET @hd = IF(ROW_COUNT() > 0, LAST_INSERT_ID(), NULL);
INSERT INTO CHI_TIET_HOA_DON (ma_hoa_don, ma_bien_the, so_luong, don_gia, gia_von, giam_gia, thanh_tien)
SELECT @hd, ma_bien_the, 1, gia_ban, gia_von, 0, gia_ban FROM BIEN_THE_SP WHERE ma_bien_the = @bt AND @hd IS NOT NULL;
UPDATE KHACH_HANG k JOIN DON_HANG d ON d.ma_don_hang = @dh
SET k.tong_chi_tieu = k.tong_chi_tieu + d.tong_thanh_toan, k.diem_tich_luy = k.diem_tich_luy + FLOOR(d.tong_thanh_toan / 10000)
WHERE k.ma_khach_hang = @kh;

SET @kh = (SELECT k.ma_khach_hang FROM KHACH_HANG k WHERE k.so_dien_thoai = '0904028100' AND k.ma_tai_khoan IS NULL AND k.da_xoa = 0
           AND NOT EXISTS (SELECT 1 FROM DON_HANG d WHERE d.ma_khach_hang = k.ma_khach_hang) LIMIT 1);
SET @bt = 25, @ngay = '2026-10-25 18:05:00', @nv = 8, @pt = 2, @dh = NULL, @hd = NULL;
INSERT INTO DON_HANG (ma_khach_hang, ma_nhan_vien, ngay_dat, ten_nguoi_nhan, sdt_nguoi_nhan, dia_chi_giao_hang, tam_tinh, tong_tien_giam_gia, tong_thanh_toan,
                      ma_phuong_thuc_thanh_toan, trang_thai_thanh_toan, trang_thai_don_hang, ngay_cap_nhat)
SELECT k.ma_khach_hang, @nv, @ngay, CONCAT(k.ho_lot, ' ', k.ten), k.so_dien_thoai, 'Mua tại cửa hàng', b.gia_ban, 0, b.gia_ban, @pt, 'da_thanh_toan', 'da_giao', @ngay
FROM KHACH_HANG k JOIN BIEN_THE_SP b ON b.ma_bien_the = @bt WHERE k.ma_khach_hang = @kh;
SET @dh = IF(ROW_COUNT() > 0, LAST_INSERT_ID(), NULL);
INSERT INTO CHI_TIET_DON_HANG (ma_don_hang, ma_bien_the, so_luong, don_gia, giam_gia, thanh_tien)
SELECT @dh, ma_bien_the, 1, gia_ban, 0, gia_ban FROM BIEN_THE_SP WHERE ma_bien_the = @bt AND @dh IS NOT NULL;
UPDATE BIEN_THE_SP SET so_luong_ton = so_luong_ton - 1 WHERE ma_bien_the = @bt AND @dh IS NOT NULL;
SET @so = CONCAT('HD', LPAD((SELECT IFNULL(MAX(ma_hoa_don), 0) + 1 FROM HOA_DON), 5, '0'));
INSERT INTO HOA_DON (so_hoa_don, ma_don_hang, ma_phuong_thuc_thanh_toan, ngay_thanh_toan, ngay_xuat, tong_tien, tong_tien_giam_gia, phi_van_chuyen, tong_thanh_toan)
SELECT @so, ma_don_hang, @pt, @ngay, @ngay, tam_tinh, 0, 0, tong_thanh_toan FROM DON_HANG WHERE ma_don_hang = @dh;
SET @hd = IF(ROW_COUNT() > 0, LAST_INSERT_ID(), NULL);
INSERT INTO CHI_TIET_HOA_DON (ma_hoa_don, ma_bien_the, so_luong, don_gia, gia_von, giam_gia, thanh_tien)
SELECT @hd, ma_bien_the, 1, gia_ban, gia_von, 0, gia_ban FROM BIEN_THE_SP WHERE ma_bien_the = @bt AND @hd IS NOT NULL;
UPDATE KHACH_HANG k JOIN DON_HANG d ON d.ma_don_hang = @dh
SET k.tong_chi_tieu = k.tong_chi_tieu + d.tong_thanh_toan, k.diem_tich_luy = k.diem_tich_luy + FLOOR(d.tong_thanh_toan / 10000)
WHERE k.ma_khach_hang = @kh;
