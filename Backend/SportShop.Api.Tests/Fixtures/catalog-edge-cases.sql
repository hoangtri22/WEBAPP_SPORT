-- TEST FIXTURE ONLY. Never import into a development, shared, or production DB.
-- Requires the separate MySQL instance bound to 127.0.0.1:13380, with its
-- datadir below .test-artifacts/mysql-api-phase1 (a timestamp suffix is OK).
-- First import Backend/Database/sports_shop.sql into that isolated instance.
-- Run this file with mysql --no-defaults --protocol=TCP --host=127.0.0.1
--   --port=13380 --user=root --default-character-set=utf8mb4
--   --execute="source D:/WEBAPP_SPORT/Backend/SportShop.Api.Tests/Fixtures/catalog-edge-cases.sql"
-- Do not use --force. The guard performs no writes: an unsafe server causes
-- PREPARE to fail against a deliberately nonexistent information_schema table.
-- Every INSERT is additionally gated by @api_fixture_safe, even if a caller
-- ignores SQL errors. No CREATE, UPDATE, DELETE, DROP, or TRUNCATE is used.
-- Re-running on the same fixture adds no duplicate rows and resets no data.

SET @api_fixture_safe = (
    @@port = 13380
    AND LOCATE('/.test-artifacts/mysql-api-phase1', REPLACE(@@datadir, CHAR(92), '/')) > 0
    AND LOWER(SUBSTRING_INDEX(USER(), '@', -1)) IN ('localhost', '127.0.0.1', '::1')
);
SET @api_fixture_guard_sql = IF(
    @api_fixture_safe,
    'SELECT 1 AS isolated_fixture_guard',
    'SELECT * FROM information_schema.__API_FIXTURE_REFUSED_WRONG_SERVER__'
);
PREPARE api_fixture_guard FROM @api_fixture_guard_sql;
EXECUTE api_fixture_guard;
DEALLOCATE PREPARE api_fixture_guard;

USE sports_shop;
START TRANSACTION;

INSERT INTO LOAI_SP (ten_loai)
SELECT '__API_PHASE1_FIXTURE__'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM LOAI_SP WHERE ten_loai = '__API_PHASE1_FIXTURE__');
SET @api_fixture_category = (SELECT ma_loai FROM LOAI_SP WHERE ten_loai = '__API_PHASE1_FIXTURE__');

INSERT INTO SAN_PHAM (ma_loai, ten_san_pham, don_vi_tinh, mo_ta, trang_thai)
SELECT @api_fixture_category, '__API_PHASE1_SOLD_OUT__', 'cai',
       'Only hidden stock/image must not count', 'hien'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM SAN_PHAM WHERE ma_loai = @api_fixture_category AND ten_san_pham = '__API_PHASE1_SOLD_OUT__');
SET @api_sold_out = (SELECT MIN(ma_san_pham) FROM SAN_PHAM WHERE ma_loai = @api_fixture_category AND ten_san_pham = '__API_PHASE1_SOLD_OUT__');
INSERT INTO BIEN_THE_SP (ma_san_pham, kich_co, mau_sac, gia_ban, gia_von, so_luong_ton, trang_thai, hinh_anh)
SELECT @api_sold_out, 'visible', 'clear', 123456.78, 800.50, 0, 'hien', NULL
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM BIEN_THE_SP WHERE ma_san_pham = @api_sold_out AND kich_co = 'visible' AND mau_sac = 'clear');
INSERT INTO BIEN_THE_SP (ma_san_pham, kich_co, mau_sac, gia_ban, gia_von, so_luong_ton, trang_thai, hinh_anh)
SELECT @api_sold_out, 'hidden', 'secret', 999999.99, 100.00, 999, 'an', '/images/giay-pegasus.jpg'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM BIEN_THE_SP WHERE ma_san_pham = @api_sold_out AND kich_co = 'hidden' AND mau_sac = 'secret');

INSERT INTO SAN_PHAM (ma_loai, ten_san_pham, don_vi_tinh, mo_ta, trang_thai)
SELECT @api_fixture_category, '__API_PHASE1_ZERO_VARIANTS__', 'cai',
       'Visible product without variants', 'hien'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM SAN_PHAM WHERE ma_loai = @api_fixture_category AND ten_san_pham = '__API_PHASE1_ZERO_VARIANTS__');

INSERT INTO SAN_PHAM (ma_loai, ten_san_pham, don_vi_tinh, mo_ta, trang_thai)
SELECT @api_fixture_category, '__API_PHASE1_HIDDEN_PRODUCT__', 'cai',
       'Hidden parent cannot be exposed', 'an'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM SAN_PHAM WHERE ma_loai = @api_fixture_category AND ten_san_pham = '__API_PHASE1_HIDDEN_PRODUCT__');
SET @api_hidden_product = (SELECT MIN(ma_san_pham) FROM SAN_PHAM WHERE ma_loai = @api_fixture_category AND ten_san_pham = '__API_PHASE1_HIDDEN_PRODUCT__');
INSERT INTO BIEN_THE_SP (ma_san_pham, kich_co, mau_sac, gia_ban, gia_von, so_luong_ton, trang_thai)
SELECT @api_hidden_product, 'one', 'one', 20000.00, 100.00, 1, 'hien'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM BIEN_THE_SP WHERE ma_san_pham = @api_hidden_product AND kich_co = 'one' AND mau_sac = 'one');

INSERT INTO SAN_PHAM (ma_loai, ten_san_pham, don_vi_tinh, mo_ta, trang_thai)
SELECT @api_fixture_category, '__API_PHASE1_LITERAL_%_!__', 'cai',
       'Search wildcards are literal', 'hien'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM SAN_PHAM WHERE ma_loai = @api_fixture_category AND ten_san_pham = '__API_PHASE1_LITERAL_%_!__');
SET @api_literal_product = (SELECT MIN(ma_san_pham) FROM SAN_PHAM WHERE ma_loai = @api_fixture_category AND ten_san_pham = '__API_PHASE1_LITERAL_%_!__');
INSERT INTO BIEN_THE_SP (ma_san_pham, kich_co, mau_sac, gia_ban, gia_von, so_luong_ton, trang_thai)
SELECT @api_literal_product, 'one', 'one', 123.45, 100.00, 2, 'hien'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM BIEN_THE_SP WHERE ma_san_pham = @api_literal_product AND kich_co = 'one' AND mau_sac = 'one');

INSERT INTO SAN_PHAM (ma_loai, ten_san_pham, don_vi_tinh, mo_ta, trang_thai)
SELECT @api_fixture_category, '__API_PHASE1_LARGE_STOCK__', 'cai',
       'Aggregate stock exceeds signed int32', 'hien'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM SAN_PHAM WHERE ma_loai = @api_fixture_category AND ten_san_pham = '__API_PHASE1_LARGE_STOCK__');
SET @api_large_stock = (SELECT MIN(ma_san_pham) FROM SAN_PHAM WHERE ma_loai = @api_fixture_category AND ten_san_pham = '__API_PHASE1_LARGE_STOCK__');
INSERT INTO BIEN_THE_SP (ma_san_pham, kich_co, mau_sac, gia_ban, gia_von, so_luong_ton, trang_thai)
SELECT @api_large_stock, 'one', 'one', 100.00, 50.00, 2147483647, 'hien'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM BIEN_THE_SP WHERE ma_san_pham = @api_large_stock AND kich_co = 'one' AND mau_sac = 'one');
INSERT INTO BIEN_THE_SP (ma_san_pham, kich_co, mau_sac, gia_ban, gia_von, so_luong_ton, trang_thai)
SELECT @api_large_stock, 'two', 'two', 200.00, 50.00, 2147483647, 'hien'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM BIEN_THE_SP WHERE ma_san_pham = @api_large_stock AND kich_co = 'two' AND mau_sac = 'two');

INSERT INTO LOAI_SP (ten_loai)
SELECT '[Đã xóa]'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM LOAI_SP WHERE ten_loai = '[Đã xóa]');
SET @api_archived_category = (SELECT ma_loai FROM LOAI_SP WHERE ten_loai = '[Đã xóa]');
INSERT INTO SAN_PHAM (ma_loai, ten_san_pham, don_vi_tinh, mo_ta, trang_thai)
SELECT @api_archived_category, '__API_PHASE1_ARCHIVED__', 'cai',
       'Archived category cannot be exposed', 'hien'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM SAN_PHAM WHERE ma_loai = @api_archived_category AND ten_san_pham = '__API_PHASE1_ARCHIVED__');
SET @api_archived_product = (SELECT MIN(ma_san_pham) FROM SAN_PHAM WHERE ma_loai = @api_archived_category AND ten_san_pham = '__API_PHASE1_ARCHIVED__');
INSERT INTO BIEN_THE_SP (ma_san_pham, kich_co, mau_sac, gia_ban, gia_von, so_luong_ton, trang_thai)
SELECT @api_archived_product, 'one', 'one', 10000.00, 100.00, 1, 'hien'
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM BIEN_THE_SP WHERE ma_san_pham = @api_archived_product AND kich_co = 'one' AND mau_sac = 'one');

-- Promotion time is frozen at 2026-10-15 10:00 in Asia/Ho_Chi_Minh by
-- MySqlCatalogTests. Include both inclusive boundaries and every exclusion.
INSERT INTO CTKM (ten_ctkm, loai_khuyen_mai, ngay_bat_dau, ngay_ket_thuc,
                  gia_tri_giam_toi_da, hang_ap_dung, so_luong_gioi_han,
                  so_luot_da_dung, gioi_han_khach_hang, trang_thai)
SELECT fixture.promo_name, fixture.promo_type, fixture.starts_at, fixture.ends_at,
       100000.00, NULL, fixture.quota, fixture.used_count, 1, fixture.promo_status
FROM (
    SELECT '__API_PHASE1_PROMO_ACTIVE__' AS promo_name, 'theo_voucher' AS promo_type,
           '2026-10-01 00:00:00' AS starts_at, '2026-11-01 00:00:00' AS ends_at,
           5 AS quota, 1 AS used_count, 'hoat_dong' AS promo_status
    UNION ALL SELECT '__API_PHASE1_PROMO_EXPIRED__', 'theo_voucher', '2026-09-01 00:00:00', '2026-10-01 00:00:00', 5, 0, 'hoat_dong'
    UNION ALL SELECT '__API_PHASE1_PROMO_EXHAUSTED__', 'theo_voucher', '2026-10-01 00:00:00', '2026-11-01 00:00:00', 1, 1, 'hoat_dong'
    UNION ALL SELECT '__API_PHASE1_PROMO_INACTIVE__', 'theo_voucher', '2026-10-01 00:00:00', '2026-11-01 00:00:00', 5, 0, 'ngung'
    UNION ALL SELECT '__API_PHASE1_PROMO_WRONGTYPE__', 'theo_san_pham', '2026-10-01 00:00:00', '2026-11-01 00:00:00', 5, 0, 'hoat_dong'
    UNION ALL SELECT '__API_PHASE1_PROMO_FUTURE__', 'theo_voucher', '2026-11-01 00:00:00', '2026-12-01 00:00:00', 5, 0, 'hoat_dong'
    UNION ALL SELECT '__API_PHASE1_PROMO_AT_START__', 'theo_voucher', '2026-10-15 10:00:00', '2026-11-01 00:00:00', 5, 0, 'hoat_dong'
    UNION ALL SELECT '__API_PHASE1_PROMO_AT_END__', 'theo_voucher', '2026-10-01 00:00:00', '2026-10-15 10:00:00', 5, 0, 'hoat_dong'
    UNION ALL SELECT '__API_PHASE1_PROMO_UNLIMITED__', 'theo_voucher', '2026-10-01 00:00:00', '2026-11-01 00:00:00', NULL, 99, 'hoat_dong'
) AS fixture
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM CTKM existing WHERE existing.ten_ctkm = fixture.promo_name);

INSERT INTO KM_THEO_VOUCHER (ma_ctkm, ma_voucher, phan_tram_giam)
SELECT promotion.ma_ctkm, fixture.voucher_code, 5.50
FROM (
    SELECT '__API_PHASE1_PROMO_ACTIVE__' AS promo_name, 'API1_ACTIVE' AS voucher_code
    UNION ALL SELECT '__API_PHASE1_PROMO_EXPIRED__', 'API1_EXPIRED'
    UNION ALL SELECT '__API_PHASE1_PROMO_EXHAUSTED__', 'API1_EXHAUSTED'
    UNION ALL SELECT '__API_PHASE1_PROMO_INACTIVE__', 'API1_INACTIVE'
    UNION ALL SELECT '__API_PHASE1_PROMO_WRONGTYPE__', 'API1_WRONGTYPE'
    UNION ALL SELECT '__API_PHASE1_PROMO_FUTURE__', 'API1_FUTURE'
    UNION ALL SELECT '__API_PHASE1_PROMO_AT_START__', 'API1_AT_START'
    UNION ALL SELECT '__API_PHASE1_PROMO_AT_END__', 'API1_AT_END'
    UNION ALL SELECT '__API_PHASE1_PROMO_UNLIMITED__', 'API1_UNLIMITED'
) AS fixture
JOIN CTKM promotion ON promotion.ten_ctkm = fixture.promo_name
WHERE @api_fixture_safe
  AND NOT EXISTS (SELECT 1 FROM KM_THEO_VOUCHER existing WHERE existing.ma_voucher = fixture.voucher_code);

COMMIT;
SELECT ma_san_pham AS fixture_product_id, ten_san_pham AS fixture_name
FROM SAN_PHAM
WHERE @api_fixture_safe AND ma_loai IN (@api_fixture_category, @api_archived_category)
  AND LEFT(ten_san_pham, 13) = '__API_PHASE1_'
ORDER BY ma_san_pham;
