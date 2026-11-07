-- Xóa hạng VIP: khách và chương trình khuyến mãi đang ở hạng VIP chuyển sang Vàng (hạng cao nhất còn lại), giữ nguyên điểm và tổng chi tiêu. Chạy lại nhiều lần vẫn an toàn.
UPDATE KHACH_HANG SET hang_thanh_vien = 'vang' WHERE hang_thanh_vien = 'VIP';
UPDATE CTKM SET hang_ap_dung = 'vang' WHERE hang_ap_dung = 'VIP';
UPDATE NHOM_KHACH_HANG SET mo_ta = 'Khách chi tiêu cao' WHERE mo_ta = 'Khách VIP theo mức chi tiêu';

ALTER TABLE KHACH_HANG DROP CHECK CK_KH_HANG;
ALTER TABLE KHACH_HANG ADD CONSTRAINT CK_KH_HANG CHECK (hang_thanh_vien IN ('thuong','bac','vang'));
ALTER TABLE CTKM DROP CHECK CK_CTKM_HANG;
ALTER TABLE CTKM ADD CONSTRAINT CK_CTKM_HANG CHECK (hang_ap_dung IN ('thuong','bac','vang'));
