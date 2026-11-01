-- TEST ONLY: requires the fresh isolated loopback MySQL datadir created by Test-MobileApi.ps1.
SET @mobile_fixture_safe = (
    DATABASE() = 'sports_shop_mobile_test'
    AND LOCATE('/.test-artifacts/mobile-api-', REPLACE(@@datadir, CHAR(92), '/')) > 0
    AND @@bind_address = '127.0.0.1'
);
SET @mobile_fixture_guard_sql = IF(@mobile_fixture_safe, 'SELECT 1',
    'SELECT * FROM information_schema.__MOBILE_FIXTURE_REFUSED_WRONG_SERVER__');
PREPARE mobile_fixture_guard FROM @mobile_fixture_guard_sql;
EXECUTE mobile_fixture_guard;
DEALLOCATE PREPARE mobile_fixture_guard;
START TRANSACTION;
INSERT INTO LOAI_SP(ten_loai)
SELECT '__MOBILE_TEST_EDGE_CASES__' WHERE @mobile_fixture_safe
AND NOT EXISTS(SELECT 1 FROM LOAI_SP WHERE ten_loai='__MOBILE_TEST_EDGE_CASES__');
SET @mobile_fixture_category=(SELECT MIN(ma_loai) FROM LOAI_SP WHERE ten_loai='__MOBILE_TEST_EDGE_CASES__');
INSERT INTO SAN_PHAM(ma_loai,ten_san_pham,don_vi_tinh,trang_thai)
SELECT @mobile_fixture_category,n.name,'cái','hien' FROM (
    SELECT '__MOBILE_NO_VARIANTS__' AS name UNION ALL SELECT '__MOBILE_SOLD_OUT__'
    UNION ALL SELECT '__MOBILE_LITERAL_%_!__'
) n WHERE @mobile_fixture_safe AND NOT EXISTS(SELECT 1 FROM SAN_PHAM sp WHERE sp.ten_san_pham=n.name);
INSERT INTO BIEN_THE_SP(ma_san_pham,kich_co,mau_sac,gia_ban,gia_von,so_luong_ton,trang_thai)
SELECT sp.ma_san_pham,'M','Xanh',123000,100000,IF(sp.ten_san_pham='__MOBILE_SOLD_OUT__',0,5),'hien'
FROM SAN_PHAM sp WHERE @mobile_fixture_safe AND sp.ma_loai=@mobile_fixture_category
AND sp.ten_san_pham IN ('__MOBILE_SOLD_OUT__','__MOBILE_LITERAL_%_!__')
AND NOT EXISTS(SELECT 1 FROM BIEN_THE_SP bt WHERE bt.ma_san_pham=sp.ma_san_pham);
COMMIT;
