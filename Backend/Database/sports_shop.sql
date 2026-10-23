-- DROP DATABASE IF EXISTS sports_shop;

CREATE DATABASE sports_shop CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;
USE sports_shop;

-- 1. Quản lý tài khoản và phân quyền
CREATE TABLE VAI_TRO (
    ma_vai_tro      INT AUTO_INCREMENT PRIMARY KEY,
    ten_vai_tro     VARCHAR(50)  NOT NULL UNIQUE,      -- Quản lý, Nhân viên, Khách hàng
    mo_ta           VARCHAR(200) NULL
);

CREATE TABLE TAI_KHOAN (
    ma_tai_khoan    INT AUTO_INCREMENT PRIMARY KEY,
    ten_dang_nhap   VARCHAR(50)  NOT NULL UNIQUE,
    email           VARCHAR(100) NOT NULL UNIQUE,
    so_dien_thoai   VARCHAR(15)  NULL,
    mat_khau        VARCHAR(255) NOT NULL,            
    ma_vai_tro      INT          NOT NULL,
    trang_thai      VARCHAR(20)  NOT NULL DEFAULT 'hoat_dong',
    CONSTRAINT FK_TK_VAITRO FOREIGN KEY (ma_vai_tro) REFERENCES VAI_TRO(ma_vai_tro),
    CONSTRAINT CK_TK_TRANGTHAI CHECK (trang_thai IN ('hoat_dong','bi_khoa','da_xoa'))
);

-- 2. Quản lý nhân viên
CREATE TABLE NHAN_VIEN (
    ma_nhan_vien    INT AUTO_INCREMENT PRIMARY KEY,
    ma_tai_khoan    INT           NOT NULL UNIQUE,
    ho_lot          VARCHAR(70)   NOT NULL,
    ten             VARCHAR(30)   NOT NULL,
    gioi_tinh       VARCHAR(10)   NULL,
    ngay_sinh       DATE          NULL,
    luong_co_ban    DECIMAL(12,2) NOT NULL DEFAULT 0,
    chuc_vu         VARCHAR(50)   NULL,
    trang_thai      VARCHAR(20)   NOT NULL DEFAULT 'dang_lam',
    CONSTRAINT FK_NV_TK FOREIGN KEY (ma_tai_khoan) REFERENCES TAI_KHOAN(ma_tai_khoan),
    CONSTRAINT CK_NV_TRANGTHAI CHECK (trang_thai IN ('dang_lam','nghi_viec')),
    CONSTRAINT CK_NV_LUONG CHECK (luong_co_ban >= 0)
);

-- 3. Quản lý khách hàng
CREATE TABLE SO_THICH (
    ma_so_thich     INT AUTO_INCREMENT PRIMARY KEY,
    ten_so_thich    VARCHAR(100) NOT NULL UNIQUE       -- Cầu lông, chạy bộ, bóng đá...
);

CREATE TABLE NHOM_KHACH_HANG (
    ma_nhom_kh      INT AUTO_INCREMENT PRIMARY KEY,
    ten_nhom        VARCHAR(100)  NOT NULL,
    mo_ta           VARCHAR(200)  NULL,
    loai_tieu_chi   VARCHAR(20)   NOT NULL,
    gia_tri_tu      DECIMAL(15,2) NULL,                -- dùng cho tiêu chí tuổi / chi tiêu
    gia_tri_den     DECIMAL(15,2) NULL,
    gioi_tinh       VARCHAR(10)   NULL,                -- dùng cho tiêu chí giới tính
    ma_so_thich     INT           NULL,                -- dùng cho tiêu chí sở thích
    CONSTRAINT FK_NKH_SOTHICH FOREIGN KEY (ma_so_thich) REFERENCES SO_THICH(ma_so_thich),
    CONSTRAINT CK_NKH_TIEUCHI CHECK (loai_tieu_chi IN ('tuoi','gioi_tinh','so_thich','chi_tieu'))
);

CREATE TABLE KHACH_HANG (
    ma_khach_hang   INT AUTO_INCREMENT PRIMARY KEY,
    ma_tai_khoan    INT           NULL UNIQUE,         -- NULL nếu khách mua tại quầy chưa có tài khoản
    ho_lot          VARCHAR(70)   NOT NULL,
    ten             VARCHAR(30)   NOT NULL,
    gioi_tinh       VARCHAR(10)   NULL,
    ngay_sinh       DATE          NULL,
    so_dien_thoai   VARCHAR(15)   NULL,
    hang_thanh_vien VARCHAR(10)   NOT NULL DEFAULT 'thuong',
    diem_tich_luy   INT           NOT NULL DEFAULT 0,
    tong_chi_tieu   DECIMAL(15,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_KH_TK FOREIGN KEY (ma_tai_khoan) REFERENCES TAI_KHOAN(ma_tai_khoan),
    CONSTRAINT CK_KH_GIOITINH CHECK (gioi_tinh IN ('Nam','Nữ','Khác')),
    CONSTRAINT CK_KH_HANG CHECK (hang_thanh_vien IN ('thuong','bac','vang','VIP')),
    CONSTRAINT CK_KH_DIEM CHECK (diem_tich_luy >= 0 AND tong_chi_tieu >= 0)
);

CREATE TABLE SO_DIA_CHI (
    ma_dia_chi       INT AUTO_INCREMENT PRIMARY KEY,
    ma_khach_hang    INT          NOT NULL,
    ten_nguoi_nhan   VARCHAR(100) NOT NULL,
    sdt_nguoi_nhan   VARCHAR(15)  NOT NULL,
    dia_chi_chi_tiet VARCHAR(255) NOT NULL,
    la_mac_dinh      TINYINT(1)   NOT NULL DEFAULT 0,  -- 1 là địa chỉ mặc định
    CONSTRAINT FK_SDC_KH FOREIGN KEY (ma_khach_hang) REFERENCES KHACH_HANG(ma_khach_hang)
);

CREATE TABLE KHACH_HANG_SO_THICH (
    ma_khach_hang   INT NOT NULL,
    ma_so_thich     INT NOT NULL,
    PRIMARY KEY (ma_khach_hang, ma_so_thich),
    CONSTRAINT FK_KHST_KH FOREIGN KEY (ma_khach_hang) REFERENCES KHACH_HANG(ma_khach_hang),
    CONSTRAINT FK_KHST_ST FOREIGN KEY (ma_so_thich)   REFERENCES SO_THICH(ma_so_thich)
);

CREATE TABLE KHACH_HANG_NHOM_KH (                  
    ma_khach_hang   INT NOT NULL,
    ma_nhom_kh      INT NOT NULL,
    PRIMARY KEY (ma_khach_hang, ma_nhom_kh),
    CONSTRAINT FK_KHNKH_KH  FOREIGN KEY (ma_khach_hang) REFERENCES KHACH_HANG(ma_khach_hang),
    CONSTRAINT FK_KHNKH_NKH FOREIGN KEY (ma_nhom_kh)    REFERENCES NHOM_KHACH_HANG(ma_nhom_kh)
);

CREATE TABLE LICH_SU_CHAM_SOC (
    ma_cham_soc          INT AUTO_INCREMENT PRIMARY KEY,
    ma_khach_hang        INT          NOT NULL,
    ma_nhan_vien         INT          NOT NULL,        -- người thực hiện chăm sóc
    hinh_thuc            VARCHAR(30)  NOT NULL,
    noi_dung             VARCHAR(500) NULL,
    ngay_cham_soc        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ngay_hen_lien_he_lai DATE         NULL,
    CONSTRAINT FK_LSCS_KH FOREIGN KEY (ma_khach_hang) REFERENCES KHACH_HANG(ma_khach_hang),
    CONSTRAINT FK_LSCS_NV FOREIGN KEY (ma_nhan_vien)  REFERENCES NHAN_VIEN(ma_nhan_vien),
    CONSTRAINT CK_LSCS_HINHTHUC CHECK (hinh_thuc IN ('goi_dien','email','tin_nhan','gap_truc_tiep'))
);

-- 4. Quản lý sản phẩm
CREATE TABLE LOAI_SP (
    ma_loai         INT AUTO_INCREMENT PRIMARY KEY,
    ten_loai        VARCHAR(100) NOT NULL UNIQUE       -- Quần áo, giày, vợt cầu lông, phụ kiện
);

CREATE TABLE SAN_PHAM (
    ma_san_pham     INT AUTO_INCREMENT PRIMARY KEY,
    ma_loai         INT          NOT NULL,
    ten_san_pham    VARCHAR(150) NOT NULL,
    don_vi_tinh     VARCHAR(20)  NOT NULL,             -- cái, đôi, bộ, hộp...
    mo_ta           LONGTEXT     NULL,
    trang_thai      VARCHAR(10)  NOT NULL DEFAULT 'hien',
    CONSTRAINT FK_SP_LOAI FOREIGN KEY (ma_loai) REFERENCES LOAI_SP(ma_loai),
    CONSTRAINT CK_SP_TRANGTHAI CHECK (trang_thai IN ('hien','an'))
);

CREATE TABLE BIEN_THE_SP (
    ma_bien_the     INT AUTO_INCREMENT PRIMARY KEY,
    ma_san_pham     INT           NOT NULL,
    kich_co         VARCHAR(20)   NULL,
    mau_sac         VARCHAR(30)   NULL,
    gia_ban         DECIMAL(12,2) NOT NULL,
    gia_von         DECIMAL(12,2) NOT NULL DEFAULT 0,  -- cập nhật theo đơn giá nhập gần nhất
    so_luong_ton    INT           NOT NULL DEFAULT 0,
    trang_thai      VARCHAR(10)   NOT NULL DEFAULT 'hien',
    hinh_anh        VARCHAR(255) NULL,                 -- đường dẫn ảnh
    CONSTRAINT FK_BT_SP FOREIGN KEY (ma_san_pham) REFERENCES SAN_PHAM(ma_san_pham),
    CONSTRAINT UQ_BT_SP_SIZE_MAU UNIQUE (ma_san_pham, kich_co, mau_sac),
    CONSTRAINT CK_BT_GIA CHECK (gia_ban >= 0 AND gia_von >= 0),
    CONSTRAINT CK_BT_TON CHECK (so_luong_ton >= 0),
    CONSTRAINT CK_BT_TRANGTHAI CHECK (trang_thai IN ('hien','an'))
);

-- 5. Quản lý nhà cung cấp
CREATE TABLE NHA_CUNG_CAP (
    ma_ncc          INT AUTO_INCREMENT PRIMARY KEY,
    ten_ncc         VARCHAR(150) NOT NULL,
    so_dien_thoai   VARCHAR(15)  NULL,
    email           VARCHAR(100) NULL,
    dia_chi         VARCHAR(255) NULL,
    trang_thai      VARCHAR(20)  NOT NULL DEFAULT 'hop_tac',
    CONSTRAINT CK_NCC_TRANGTHAI CHECK (trang_thai IN ('hop_tac','ngung_hop_tac'))
);

-- 6. Quản lý phiếu nhập hàng
CREATE TABLE PHIEU_NHAP_HANG (
    ma_phieu_nhap   INT AUTO_INCREMENT PRIMARY KEY,
    ngay_nhap       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ma_nhan_vien    INT           NOT NULL,            -- người lập phiếu
    ma_ncc          INT           NOT NULL,
    tong_tien       DECIMAL(15,2) NOT NULL DEFAULT 0,  -- tổng thanh_tien các dòng chi tiết
    CONSTRAINT FK_PN_NV  FOREIGN KEY (ma_nhan_vien) REFERENCES NHAN_VIEN(ma_nhan_vien),
    CONSTRAINT FK_PN_NCC FOREIGN KEY (ma_ncc)       REFERENCES NHA_CUNG_CAP(ma_ncc)
);

CREATE TABLE CHI_TIET_PHIEU_NHAP_HANG (
    ma_phieu_nhap   INT           NOT NULL,
    ma_bien_the     INT           NOT NULL,
    so_luong        INT           NOT NULL,
    don_gia         DECIMAL(12,2) NOT NULL,            -- giá nhập
    thanh_tien      DECIMAL(15,2) NOT NULL,
    PRIMARY KEY (ma_phieu_nhap, ma_bien_the),
    CONSTRAINT FK_CTPN_PN FOREIGN KEY (ma_phieu_nhap) REFERENCES PHIEU_NHAP_HANG(ma_phieu_nhap),
    CONSTRAINT FK_CTPN_BT FOREIGN KEY (ma_bien_the)   REFERENCES BIEN_THE_SP(ma_bien_the),
    CONSTRAINT CK_CTPN_SL CHECK (so_luong > 0 AND don_gia >= 0)
);

-- 7. Khuyến mãi và marketing
CREATE TABLE CTKM (
    ma_ctkm             INT AUTO_INCREMENT PRIMARY KEY,
    ten_ctkm            VARCHAR(150)  NOT NULL,
    loai_khuyen_mai     VARCHAR(30)   NOT NULL,
    ngay_bat_dau        DATETIME      NOT NULL,
    ngay_ket_thuc       DATETIME      NOT NULL,
    gia_tri_giam_toi_da DECIMAL(12,2) NULL,            -- mức giảm tối đa
    hang_ap_dung        VARCHAR(10)   NULL,            -- NULL: áp dụng cho mọi hạng
    so_luong_gioi_han   INT           NULL,            -- NULL: không giới hạn
    so_luot_da_dung     INT           NOT NULL DEFAULT 0,
    gioi_han_khach_hang INT           NULL,            -- số lần tối đa mỗi khách được dùng
    trang_thai          VARCHAR(20)   NOT NULL DEFAULT 'hoat_dong',
    CONSTRAINT CK_CTKM_LOAI CHECK (loai_khuyen_mai IN ('theo_san_pham','theo_don_hang','theo_voucher')),
    CONSTRAINT CK_CTKM_HANG CHECK (hang_ap_dung IN ('thuong','bac','vang','VIP')),
    CONSTRAINT CK_CTKM_TRANGTHAI CHECK (trang_thai IN ('hoat_dong','het_han','ngung')),
    CONSTRAINT CK_CTKM_NGAY CHECK (ngay_ket_thuc > ngay_bat_dau)
);

CREATE TABLE KM_THEO_SP (
    ma_ctkm         INT          NOT NULL,
    ma_san_pham     INT          NOT NULL,
    phan_tram_giam  DECIMAL(5,2) NOT NULL,
    PRIMARY KEY (ma_ctkm, ma_san_pham),
    CONSTRAINT FK_KMSP_CTKM FOREIGN KEY (ma_ctkm)     REFERENCES CTKM(ma_ctkm),
    CONSTRAINT FK_KMSP_SP   FOREIGN KEY (ma_san_pham) REFERENCES SAN_PHAM(ma_san_pham),
    CONSTRAINT CK_KMSP_PT CHECK (phan_tram_giam > 0 AND phan_tram_giam <= 100)
);

CREATE TABLE KM_THEO_DH (
    ma_ctkm           INT           NOT NULL,
    muc_tien_don_hang DECIMAL(15,2) NOT NULL,          -- mốc tiền đơn hàng tối thiểu
    phan_tram_giam    DECIMAL(5,2)  NOT NULL,
    PRIMARY KEY (ma_ctkm, muc_tien_don_hang),
    CONSTRAINT FK_KMDH_CTKM FOREIGN KEY (ma_ctkm) REFERENCES CTKM(ma_ctkm),
    CONSTRAINT CK_KMDH_PT CHECK (phan_tram_giam > 0 AND phan_tram_giam <= 100)
);

CREATE TABLE KM_THEO_VOUCHER (
    ma_ctkm         INT          NOT NULL,
    ma_voucher      VARCHAR(30)  NOT NULL UNIQUE,      -- mã khách nhập khi thanh toán
    phan_tram_giam  DECIMAL(5,2) NOT NULL,
    PRIMARY KEY (ma_ctkm, ma_voucher),
    CONSTRAINT FK_KMVC_CTKM FOREIGN KEY (ma_ctkm) REFERENCES CTKM(ma_ctkm),
    CONSTRAINT CK_KMVC_PT CHECK (phan_tram_giam > 0 AND phan_tram_giam <= 100)
);

-- 8. Quản lý đơn hàng và giao hàng
CREATE TABLE PHUONG_THUC_THANH_TOAN (
    ma_phuong_thuc_thanh_toan  INT AUTO_INCREMENT PRIMARY KEY,
    ten_phuong_thuc_thanh_toan VARCHAR(50) NOT NULL UNIQUE,
    trang_thai                 VARCHAR(10) NOT NULL DEFAULT 'hoat_dong',
    CONSTRAINT CK_PTTT_TRANGTHAI CHECK (trang_thai IN ('hoat_dong','ngung'))
);

CREATE TABLE DON_HANG (
    ma_don_hang               INT AUTO_INCREMENT PRIMARY KEY,
    ma_khach_hang             INT           NOT NULL,
    ma_nhan_vien              INT           NULL,      -- NULL nếu khách tự đặt online
    ngay_dat                  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ten_nguoi_nhan            VARCHAR(100)  NOT NULL,
    sdt_nguoi_nhan            VARCHAR(15)   NOT NULL,
    dia_chi_giao_hang         VARCHAR(255)  NOT NULL,
    ma_ctkm                   INT           NULL,      -- KM toàn đơn (mốc tiền / voucher)
    tam_tinh                  DECIMAL(15,2) NOT NULL DEFAULT 0,
    tong_tien_giam_gia        DECIMAL(15,2) NOT NULL DEFAULT 0,
    tong_thanh_toan           DECIMAL(15,2) NOT NULL DEFAULT 0,
    ma_phuong_thuc_thanh_toan INT           NOT NULL,
    trang_thai_thanh_toan     VARCHAR(20)   NOT NULL DEFAULT 'chua_thanh_toan',
    trang_thai_don_hang       VARCHAR(20)   NOT NULL DEFAULT 'cho_xac_nhan',
    ly_do_huy                 VARCHAR(200)  NULL,
    ngay_cap_nhat             DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT FK_DH_KH   FOREIGN KEY (ma_khach_hang) REFERENCES KHACH_HANG(ma_khach_hang),
    CONSTRAINT FK_DH_NV   FOREIGN KEY (ma_nhan_vien)  REFERENCES NHAN_VIEN(ma_nhan_vien),
    CONSTRAINT FK_DH_CTKM FOREIGN KEY (ma_ctkm)       REFERENCES CTKM(ma_ctkm),
    CONSTRAINT FK_DH_PTTT FOREIGN KEY (ma_phuong_thuc_thanh_toan) REFERENCES PHUONG_THUC_THANH_TOAN(ma_phuong_thuc_thanh_toan),
    CONSTRAINT CK_DH_TTTT CHECK (trang_thai_thanh_toan IN ('chua_thanh_toan','da_thanh_toan')),
    CONSTRAINT CK_DH_TTDH CHECK (trang_thai_don_hang IN ('cho_xac_nhan','dang_chuan_bi','dang_giao','da_giao','da_huy')),
    CONSTRAINT CK_DH_HUY CHECK (trang_thai_don_hang <> 'da_huy' OR ly_do_huy IS NOT NULL),
    CONSTRAINT CK_DH_TIEN CHECK (tam_tinh >= 0 AND tong_tien_giam_gia >= 0 AND tong_thanh_toan >= 0)
);

CREATE TABLE CHI_TIET_DON_HANG (
    ma_don_hang     INT           NOT NULL,
    ma_bien_the     INT           NOT NULL,
    so_luong        INT           NOT NULL,
    don_gia         DECIMAL(12,2) NOT NULL,            -- giá bán tại thời điểm đặt
    giam_gia        DECIMAL(12,2) NOT NULL DEFAULT 0,
    thanh_tien      DECIMAL(15,2) NOT NULL,            -- so_luong * don_gia - giam_gia
    PRIMARY KEY (ma_don_hang, ma_bien_the),
    CONSTRAINT FK_CTDH_DH FOREIGN KEY (ma_don_hang) REFERENCES DON_HANG(ma_don_hang),
    CONSTRAINT FK_CTDH_BT FOREIGN KEY (ma_bien_the) REFERENCES BIEN_THE_SP(ma_bien_the),
    CONSTRAINT CK_CTDH_SL CHECK (so_luong > 0 AND don_gia >= 0 AND giam_gia >= 0)
);

CREATE TABLE VAN_CHUYEN (
    ma_don_hang           INT           NOT NULL PRIMARY KEY,  -- mỗi đơn hàng một lần giao
    ma_nhan_vien          INT           NULL,
    ten_don_vi_van_chuyen VARCHAR(100)  NOT NULL,
    ma_van_don            VARCHAR(50)   NULL,
    phi_van_chuyen        DECIMAL(12,2) NOT NULL DEFAULT 0,
    ngay_giao_du_kien     DATE          NULL,
    ngay_giao_thuc_te     DATE          NULL,
    trang_thai            VARCHAR(20)   NOT NULL DEFAULT 'cho_lay_hang',
    CONSTRAINT FK_VC_DH FOREIGN KEY (ma_don_hang)  REFERENCES DON_HANG(ma_don_hang),
    CONSTRAINT FK_VC_NV FOREIGN KEY (ma_nhan_vien) REFERENCES NHAN_VIEN(ma_nhan_vien),
    CONSTRAINT CK_VC_TRANGTHAI CHECK (trang_thai IN ('cho_lay_hang','dang_giao','da_giao','that_bai')),
    CONSTRAINT CK_VC_PHI CHECK (phi_van_chuyen >= 0)
);

-- 9. Quản lý hóa đơn
CREATE TABLE HOA_DON (
    ma_hoa_don                INT AUTO_INCREMENT PRIMARY KEY,
    so_hoa_don                VARCHAR(30)   NOT NULL UNIQUE,
    ma_don_hang               INT           NOT NULL UNIQUE,  -- mỗi đơn tối đa một hóa đơn
    ma_phuong_thuc_thanh_toan INT           NOT NULL,
    ngay_thanh_toan           DATETIME      NOT NULL,
    ngay_xuat                 DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    tong_tien                 DECIMAL(15,2) NOT NULL,
    tong_tien_giam_gia        DECIMAL(15,2) NOT NULL DEFAULT 0,
    phi_van_chuyen            DECIMAL(12,2) NOT NULL DEFAULT 0,
    tong_thanh_toan           DECIMAL(15,2) NOT NULL,
    CONSTRAINT FK_HD_DH   FOREIGN KEY (ma_don_hang) REFERENCES DON_HANG(ma_don_hang),
    CONSTRAINT FK_HD_PTTT FOREIGN KEY (ma_phuong_thuc_thanh_toan) REFERENCES PHUONG_THUC_THANH_TOAN(ma_phuong_thuc_thanh_toan)
);

CREATE TABLE CHI_TIET_HOA_DON (
    ma_hoa_don      INT           NOT NULL,
    ma_bien_the     INT           NOT NULL,
    so_luong        INT           NOT NULL,
    don_gia         DECIMAL(12,2) NOT NULL,            -- giá bán tại thời điểm bán
    gia_von         DECIMAL(12,2) NOT NULL,            -- giá vốn tại thời điểm bán
    giam_gia        DECIMAL(12,2) NOT NULL DEFAULT 0,
    thanh_tien      DECIMAL(15,2) NOT NULL,
    PRIMARY KEY (ma_hoa_don, ma_bien_the),
    CONSTRAINT FK_CTHD_HD FOREIGN KEY (ma_hoa_don)  REFERENCES HOA_DON(ma_hoa_don),
    CONSTRAINT FK_CTHD_BT FOREIGN KEY (ma_bien_the) REFERENCES BIEN_THE_SP(ma_bien_the),
    CONSTRAINT CK_CTHD_SL CHECK (so_luong > 0)
);

CREATE TABLE LUOT_SU_DUNG_KHUYEN_MAI (
    ma_luot         INT AUTO_INCREMENT PRIMARY KEY,
    ma_ctkm         INT           NOT NULL,
    ma_khach_hang   INT           NOT NULL,
    ma_don_hang     INT           NOT NULL,
    so_tien_giam    DECIMAL(12,2) NOT NULL,
    ngay_su_dung    DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_LSD_CTKM FOREIGN KEY (ma_ctkm)       REFERENCES CTKM(ma_ctkm),
    CONSTRAINT FK_LSD_KH   FOREIGN KEY (ma_khach_hang) REFERENCES KHACH_HANG(ma_khach_hang),
    CONSTRAINT FK_LSD_DH   FOREIGN KEY (ma_don_hang)   REFERENCES DON_HANG(ma_don_hang)
);

CREATE TABLE CHIEN_DICH_THONG_BAO (
    ma_chien_dich   INT AUTO_INCREMENT PRIMARY KEY,
    tieu_de         VARCHAR(150) NOT NULL,
    noi_dung        LONGTEXT     NOT NULL,
    kenh_gui        VARCHAR(20)  NOT NULL,
    ma_ctkm         INT          NULL,
    ma_nhom_kh      INT          NULL,                 -- NULL: gửi cho tất cả khách
    thoi_gian_gui   DATETIME     NULL,
    trang_thai      VARCHAR(20)  NOT NULL DEFAULT 'nhap',
    ma_nhan_vien    INT          NOT NULL,             -- người tạo chiến dịch
    CONSTRAINT FK_CD_CTKM FOREIGN KEY (ma_ctkm)      REFERENCES CTKM(ma_ctkm),
    CONSTRAINT FK_CD_NKH  FOREIGN KEY (ma_nhom_kh)   REFERENCES NHOM_KHACH_HANG(ma_nhom_kh),
    CONSTRAINT FK_CD_NV   FOREIGN KEY (ma_nhan_vien) REFERENCES NHAN_VIEN(ma_nhan_vien),
    CONSTRAINT CK_CD_KENH CHECK (kenh_gui IN ('email','sms','app')),
    CONSTRAINT CK_CD_TRANGTHAI CHECK (trang_thai IN ('nhap','da_gui'))
);

-- 10. Phản hồi và khảo sát khách hàng
CREATE TABLE PHAN_HOI (
    ma_phan_hoi     INT AUTO_INCREMENT PRIMARY KEY,
    ma_khach_hang   INT           NOT NULL,
    ma_san_pham     INT           NULL,
    ma_don_hang     INT           NULL,
    loai            VARCHAR(20)   NOT NULL,
    so_sao          TINYINT       NULL,
    tieu_de         VARCHAR(150)  NULL,
    noi_dung        VARCHAR(1000) NOT NULL,
    ngay_gui        DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    trang_thai      VARCHAR(20)   NOT NULL DEFAULT 'moi',
    an_hien         TINYINT(1)    NOT NULL DEFAULT 1,  -- 1 là hiển thị
    CONSTRAINT FK_PH_KH FOREIGN KEY (ma_khach_hang) REFERENCES KHACH_HANG(ma_khach_hang),
    CONSTRAINT FK_PH_SP FOREIGN KEY (ma_san_pham)   REFERENCES SAN_PHAM(ma_san_pham),
    CONSTRAINT FK_PH_DH FOREIGN KEY (ma_don_hang)   REFERENCES DON_HANG(ma_don_hang),
    CONSTRAINT CK_PH_LOAI CHECK (loai IN ('danh_gia','gop_y','khieu_nai')),
    CONSTRAINT CK_PH_SAO CHECK (so_sao BETWEEN 1 AND 5),
    CONSTRAINT CK_PH_TRANGTHAI CHECK (trang_thai IN ('moi','dang_xu_ly','da_tra_loi'))
);

CREATE TABLE TRA_LOI_PHAN_HOI (
    ma_tra_loi      INT AUTO_INCREMENT PRIMARY KEY,
    ma_phan_hoi     INT           NOT NULL,
    ma_nhan_vien    INT           NOT NULL,
    noi_dung        VARCHAR(1000) NOT NULL,
    ngay_tra_loi    DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_TLPH_PH FOREIGN KEY (ma_phan_hoi)  REFERENCES PHAN_HOI(ma_phan_hoi),
    CONSTRAINT FK_TLPH_NV FOREIGN KEY (ma_nhan_vien) REFERENCES NHAN_VIEN(ma_nhan_vien)
);

CREATE TABLE KHAO_SAT (
    ma_khao_sat     INT AUTO_INCREMENT PRIMARY KEY,
    tieu_de         VARCHAR(150) NOT NULL,
    mo_ta           VARCHAR(500) NULL,
    ngay_bat_dau    DATETIME     NOT NULL,
    ngay_ket_thuc   DATETIME     NOT NULL,
    trang_thai      VARCHAR(20)  NOT NULL DEFAULT 'nhap',
    ma_nhan_vien    INT          NOT NULL,             -- người tạo khảo sát
    ngay_tao        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_KS_NV FOREIGN KEY (ma_nhan_vien) REFERENCES NHAN_VIEN(ma_nhan_vien),
    CONSTRAINT CK_KS_TRANGTHAI CHECK (trang_thai IN ('nhap','da_gui','da_dong')),
    CONSTRAINT CK_KS_NGAY CHECK (ngay_ket_thuc > ngay_bat_dau)
);

CREATE TABLE CAU_HOI_KHAO_SAT (
    ma_cau_hoi      INT AUTO_INCREMENT PRIMARY KEY,
    ma_khao_sat     INT          NOT NULL,
    noi_dung        VARCHAR(300) NOT NULL,
    loai_cau_hoi    VARCHAR(20)  NOT NULL,
    bat_buoc        TINYINT(1)   NOT NULL DEFAULT 0,
    thu_tu          INT          NOT NULL DEFAULT 1,
    CONSTRAINT FK_CH_KS FOREIGN KEY (ma_khao_sat) REFERENCES KHAO_SAT(ma_khao_sat),
    CONSTRAINT CK_CH_LOAI CHECK (loai_cau_hoi IN ('mot_lua_chon','nhieu_lua_chon','thang_diem','van_ban'))
);

CREATE TABLE LUA_CHON_CAU_HOI (
    ma_lua_chon     INT AUTO_INCREMENT PRIMARY KEY,
    ma_cau_hoi      INT          NOT NULL,
    noi_dung        VARCHAR(200) NOT NULL,
    CONSTRAINT FK_LC_CH FOREIGN KEY (ma_cau_hoi) REFERENCES CAU_HOI_KHAO_SAT(ma_cau_hoi)
);

CREATE TABLE KHAO_SAT_KHACH_HANG (
    ma_khao_sat     INT         NOT NULL,
    ma_khach_hang   INT         NOT NULL,
    ngay_gui        DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    trang_thai      VARCHAR(20) NOT NULL DEFAULT 'chua_lam',
    ngay_hoan_thanh DATETIME    NULL,
    PRIMARY KEY (ma_khao_sat, ma_khach_hang),          -- mỗi khách một lượt cho mỗi khảo sát
    CONSTRAINT FK_KSKH_KS FOREIGN KEY (ma_khao_sat)   REFERENCES KHAO_SAT(ma_khao_sat),
    CONSTRAINT FK_KSKH_KH FOREIGN KEY (ma_khach_hang) REFERENCES KHACH_HANG(ma_khach_hang),
    CONSTRAINT CK_KSKH_TRANGTHAI CHECK (trang_thai IN ('chua_lam','dang_lam','hoan_thanh'))
);

CREATE TABLE TRA_LOI_KHAO_SAT (
    ma_tra_loi       INT AUTO_INCREMENT PRIMARY KEY,
    ma_khao_sat      INT          NOT NULL,
    ma_khach_hang    INT          NOT NULL,
    ma_cau_hoi       INT          NOT NULL,
    ma_lua_chon      INT          NULL,                -- NULL nếu là câu hỏi văn bản
    noi_dung_tra_loi VARCHAR(500) NULL,                -- NULL nếu là câu hỏi chọn đáp án
    ngay_tra_loi     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_TLKS_KSKH FOREIGN KEY (ma_khao_sat, ma_khach_hang)
        REFERENCES KHAO_SAT_KHACH_HANG(ma_khao_sat, ma_khach_hang),
    CONSTRAINT FK_TLKS_CH FOREIGN KEY (ma_cau_hoi)  REFERENCES CAU_HOI_KHAO_SAT(ma_cau_hoi),
    CONSTRAINT FK_TLKS_LC FOREIGN KEY (ma_lua_chon) REFERENCES LUA_CHON_CAU_HOI(ma_lua_chon),
    CONSTRAINT CK_TLKS_NOIDUNG CHECK (ma_lua_chon IS NOT NULL OR noi_dung_tra_loi IS NOT NULL)
);

-- Quy ước tính tiền:
--   tam_tinh           = tổng so_luong * don_gia (chưa giảm)
--   tong_tien_giam_gia = giảm theo dòng (KM theo SP) + giảm toàn đơn (mốc tiền / voucher)
--   tong_thanh_toan    = tam_tinh - tong_tien_giam_gia + phí vận chuyển
--   Hóa đơn chỉ lập cho đơn đã thanh toán
--   Tồn kho = tổng nhập - tổng bán (các UPDATE bên dưới tự cập nhật)

-- 1. Vai trò
INSERT INTO VAI_TRO (ten_vai_tro, mo_ta) VALUES
	('Quản lý',    'Toàn quyền, cấp và thay đổi vai trò cho tài khoản'),
	('Nhân viên',  'Xử lý nghiệp vụ bán hàng, nhập hàng và chăm sóc khách'),
	('Khách hàng', 'Mua sắm, quản lý thông tin cá nhân, gửi phản hồi, làm khảo sát');

-- 2. Tài khoản
INSERT INTO TAI_KHOAN (ten_dang_nhap, email, so_dien_thoai, mat_khau, ma_vai_tro, trang_thai) VALUES
	('admin',     'admin@sportshop.vn',     '0901000001', '123456', 1, 'hoat_dong'),  -- 1
	('nv_lan',    'lan.nv@sportshop.vn',    '0901000002', '123456', 2, 'hoat_dong'),  -- 2
	('nv_tuan',   'tuan.nv@sportshop.vn',   '0901000003', '123456', 2, 'hoat_dong'),  -- 3
	('kh_an',     'an.kh@gmail.com',        '0912000001', '123456', 3, 'hoat_dong'),  -- 4
	('kh_binh',   'binh.kh@gmail.com',      '0912000002', '123456', 3, 'hoat_dong'),  -- 5
	('kh_chi',    'chi.kh@gmail.com',       '0912000003', '123456', 3, 'hoat_dong'),  -- 6
	('kh_dung',   'dung.kh@gmail.com',      '0912000004', '123456', 3, 'bi_khoa'),    -- 7
	('nv_hoa',    'hoa.nv@sportshop.vn',    '0901000004', '123456', 2, 'hoat_dong'),  -- 8
	('nv_nam',    'nam.nv@sportshop.vn',    '0901000005', '123456', 2, 'hoat_dong'),  -- 9
	('nv_thao',   'thao.nv@sportshop.vn',   '0901000006', '123456', 2, 'hoat_dong'),  -- 10
	('nv_phuc',   'phuc.nv@sportshop.vn',   '0901000007', '123456', 2, 'hoat_dong'),  -- 11
	('nv_vy',     'vy.nv@sportshop.vn',     '0901000008', '123456', 2, 'hoat_dong'),  -- 12
	('nv_khoa',   'khoa.nv@sportshop.vn',   '0901000009', '123456', 2, 'hoat_dong'),  -- 13
	('nv_trang',  'trang.nv@sportshop.vn',  '0901000010', '123456', 2, 'bi_khoa'),    -- 14 (đã nghỉ việc)
	('kh_mai',    'mai.kh@gmail.com',       '0912000006', '123456', 3, 'hoat_dong'),  -- 15
	('kh_khanh',  'khanh.kh@gmail.com',     '0912000007', '123456', 3, 'hoat_dong'),  -- 16
	('kh_long',   'long.kh@gmail.com',      '0912000008', '123456', 3, 'hoat_dong'),  -- 17
	('kh_linh',   'linh.kh@gmail.com',      '0912000009', '123456', 3, 'hoat_dong');  -- 18

-- 3. Nhân viên
INSERT INTO NHAN_VIEN (ma_tai_khoan, ho_lot, ten, gioi_tinh, ngay_sinh, luong_co_ban, chuc_vu, trang_thai) VALUES
	(1,  'Nguyễn Văn',  'Hùng',  'Nam', '1988-03-12', 15000000, 'Quản lý cửa hàng',            'dang_lam'),   -- 1
	(2,  'Trần Thị',    'Lan',   'Nữ',  '1998-07-25',  8000000, 'Nhân viên bán hàng',          'dang_lam'),   -- 2
	(3,  'Lê Minh',     'Tuấn',  'Nam', '2000-11-02',  7500000, 'Nhân viên kho',               'dang_lam'),   -- 3
	(8,  'Phạm Thị',    'Hoa',   'Nữ',  '1997-04-18',  8500000, 'Nhân viên bán hàng',          'dang_lam'),   -- 4
	(9,  'Nguyễn Văn',  'Nam',   'Nam', '1999-09-09',  7500000, 'Nhân viên kho',               'dang_lam'),   -- 5
	(10, 'Võ Thanh',    'Thảo',  'Nữ',  '2001-01-15',  8000000, 'Nhân viên chăm sóc khách hàng', 'dang_lam'), -- 6
	(11, 'Trần Minh',   'Phúc',  'Nam', '1996-06-06',  7000000, 'Nhân viên giao hàng',         'dang_lam'),   -- 7
	(12, 'Đặng Ngọc',   'Vy',    'Nữ',  '2000-12-20',  8000000, 'Nhân viên bán hàng',          'dang_lam'),   -- 8
	(13, 'Huỳnh Đăng',  'Khoa',  'Nam', '1994-03-03', 12000000, 'Trưởng nhóm kinh doanh',      'dang_lam'),   -- 9
	(14, 'Lê Thị',      'Trang', 'Nữ',  '1998-05-27',  7500000, 'Nhân viên bán hàng',          'nghi_viec');  -- 10

-- 4. Sở thích
INSERT INTO SO_THICH (ten_so_thich) VALUES
	('Cầu lông'), ('Chạy bộ'), ('Bóng đá'), ('Gym'), ('Tennis'),          -- 1-5
	('Bơi lội'), ('Yoga'), ('Bóng rổ'), ('Đạp xe'), ('Pickleball');       -- 6-10

-- 5. Nhóm khách hàng
INSERT INTO NHOM_KHACH_HANG (ten_nhom, mo_ta, loai_tieu_chi, gia_tri_tu, gia_tri_den, gioi_tinh, ma_so_thich) VALUES
	('Khách trẻ 18-25 tuổi',   'Nhóm khách hàng trẻ',             'tuoi',      18,       25,   NULL,  NULL),  -- 1
	('Khách nữ',                'Khách hàng giới tính nữ',         'gioi_tinh', NULL,     NULL, 'Nữ',  NULL),  -- 2
	('Yêu thích cầu lông',      'Khách có sở thích cầu lông',      'so_thich',  NULL,     NULL, NULL,  1),     -- 3
	('Chi tiêu trên 5 triệu',   'Khách thân thiết theo chi tiêu',  'chi_tieu',  5000000,  NULL, NULL,  NULL),  -- 4
	('Khách 26-35 tuổi',        'Nhóm khách hàng trưởng thành trẻ','tuoi',      26,       35,   NULL,  NULL),  -- 5
	('Khách trên 40 tuổi',      'Nhóm khách hàng trung niên',      'tuoi',      40,       NULL, NULL,  NULL),  -- 6
	('Khách nam',               'Khách hàng giới tính nam',        'gioi_tinh', NULL,     NULL, 'Nam', NULL),  -- 7
	('Yêu thích chạy bộ',       'Khách có sở thích chạy bộ',       'so_thich',  NULL,     NULL, NULL,  2),     -- 8
	('Yêu thích gym',           'Khách có sở thích gym',           'so_thich',  NULL,     NULL, NULL,  4),     -- 9
	('Chi tiêu trên 10 triệu',  'Khách VIP theo mức chi tiêu',     'chi_tieu',  10000000, NULL, NULL,  NULL);  -- 10

-- 6. Khách hàng (khách 5 và 10 mua tại quầy, chưa có tài khoản)
INSERT INTO KHACH_HANG (ma_tai_khoan, ho_lot, ten, gioi_tinh, ngay_sinh, so_dien_thoai, hang_thanh_vien, diem_tich_luy, tong_chi_tieu) VALUES
	(4,    'Phạm Minh',  'An',    'Nam', '1999-05-10', '0912000001', 'vang',   0, 0),  -- 1
	(5,    'Võ Thị',     'Bình',  'Nữ',  '2002-09-21', '0912000002', 'thuong', 0, 0),  -- 2
	(6,    'Đỗ Ngọc',    'Chi',   'Nữ',  '1985-01-30', '0912000003', 'bac',    0, 0),  -- 3
	(7,    'Hoàng Văn',  'Dũng',  'Nam', '1995-12-05', '0912000004', 'thuong', 0, 0),  -- 4
	(NULL, 'Bùi Thanh',  'Hà',    'Nữ',  '1990-06-18', '0912000005', 'thuong', 0, 0),  -- 5
	(15,   'Nguyễn Thị', 'Mai',   'Nữ',  '2001-03-14', '0912000006', 'thuong', 0, 0),  -- 6
	(16,   'Trần Quốc',  'Khánh', 'Nam', '1992-08-09', '0912000007', 'bac',    0, 0),  -- 7
	(17,   'Lê Hoàng',   'Long',  'Nam', '2003-11-22', '0912000008', 'thuong', 0, 0),  -- 8
	(18,   'Phan Thùy',  'Linh',  'Nữ',  '1988-02-17', '0912000009', 'thuong', 0, 0),  -- 9
	(NULL, 'Ngô Đức',    'Thịnh', 'Nam', '1978-07-30', '0912000010', 'thuong', 0, 0);  -- 10

-- 7. Sổ địa chỉ
INSERT INTO SO_DIA_CHI (ma_khach_hang, ten_nguoi_nhan, sdt_nguoi_nhan, dia_chi_chi_tiet, la_mac_dinh) VALUES
	(1,  'Phạm Minh An',     '0912000001', '273 An Dương Vương, Phường 3, Quận 5, TP.HCM',  1),
	(1,  'Phạm Minh An',     '0912000001', '12 Nguyễn Huệ, Quận 1, TP.HCM (công ty)',       0),
	(2,  'Võ Thị Bình',      '0912000002', '45 Lê Văn Sỹ, Quận 3, TP.HCM',                  1),
	(3,  'Đỗ Ngọc Chi',      '0912000003', '88 Phan Xích Long, Phú Nhuận, TP.HCM',          1),
	(4,  'Hoàng Văn Dũng',   '0912000004', '19 Hoàng Diệu, Quận 4, TP.HCM',                 1),
	(5,  'Bùi Thanh Hà',     '0912000005', '63 Lê Văn Việt, TP. Thủ Đức, TP.HCM',           1),
	(6,  'Nguyễn Thị Mai',   '0912000006', '102 Nguyễn Thị Minh Khai, Quận 3, TP.HCM',      1),
	(6,  'Nguyễn Thị Mai',   '0912000006', '25 Trần Hưng Đạo, Quận 1, TP.HCM (công ty)',    0),
	(7,  'Trần Quốc Khánh',  '0912000007', '56 Cách Mạng Tháng Tám, Quận 10, TP.HCM',       1),
	(8,  'Lê Hoàng Long',    '0912000008', '210 Lý Thường Kiệt, Quận 11, TP.HCM',           1),
	(9,  'Phan Thùy Linh',   '0912000009', '34 Nguyễn Văn Lượng, Gò Vấp, TP.HCM',           1),
	(10, 'Ngô Đức Thịnh',    '0912000010', '78 Cộng Hòa, Tân Bình, TP.HCM',                 1);

-- 8. Sở thích của khách hàng
INSERT INTO KHACH_HANG_SO_THICH (ma_khach_hang, ma_so_thich) VALUES
	(1,1), (1,2), (2,4), (3,1), (3,5), (4,3), (5,2),
	(6,7), (6,4), (7,1), (7,3), (8,8), (8,3), (9,7), (9,6), (10,1), (10,5), (10,10);

-- 9. Gán khách vào nhóm theo tiêu chí
-- nhóm 1: 18-25 tuổi | 2: nữ | 3: cầu lông | 4: chi tiêu > 5tr | 5: 26-35 tuổi
-- nhóm 6: > 40 tuổi  | 7: nam | 8: chạy bộ | 9: gym | 10: chi tiêu > 10tr
INSERT INTO KHACH_HANG_NHOM_KH (ma_khach_hang, ma_nhom_kh) VALUES
	(1,3), (2,1), (2,2), (3,2), (3,3), (5,2),
	(6,1), (8,1),
	(6,2), (9,2),
	(7,3), (10,3),
	(1,4), (7,4),
	(1,5), (4,5), (7,5),
	(3,6), (10,6),
	(1,7), (4,7), (7,7), (8,7), (10,7),
	(1,8), (5,8),
	(2,9), (6,9);

-- 10. Lịch sử chăm sóc khách hàng
INSERT INTO LICH_SU_CHAM_SOC (ma_khach_hang, ma_nhan_vien, hinh_thuc, noi_dung, ngay_cham_soc, ngay_hen_lien_he_lai) VALUES
	(1,  2, 'goi_dien',      'Tư vấn vợt cầu lông mới, khách hẹn ghé cửa hàng',                           '2026-09-20 10:00:00', '2026-10-05'),
	(3,  2, 'email',         'Gửi thông tin chương trình tri ân hạng bạc',                                '2026-09-25 14:30:00', NULL),
	(2,  6, 'goi_dien',      'Hỏi thăm sau khi mua áo Dri-Fit, ghi nhận mong muốn có thêm size XL',       '2026-09-19 10:00:00', '2026-10-01'),
	(4,  6, 'tin_nhan',      'Gửi tin nhắn mời tham gia khảo sát hài lòng quý 3',                         '2026-09-20 09:30:00', NULL),
	(1,  2, 'gap_truc_tiep', 'Khách ghé cửa hàng thử vợt Astrox 77 và nhận tư vấn căng cước',             '2026-09-22 15:00:00', NULL),
	(9,  6, 'email',         'Gửi voucher chào bạn mới và hướng dẫn tích điểm',                           '2026-09-29 09:00:00', NULL),
	(7,  6, 'email',         'Gửi voucher tri ân và giới thiệu vợt Nanoflare 700',                        '2026-10-02 11:00:00', '2026-10-10'),
	(6,  6, 'tin_nhan',      'Cảm ơn khách đã mua combo đồ tập gym, mời đánh giá sản phẩm',               '2026-10-07 14:00:00', NULL),
	(10, 4, 'gap_truc_tiep', 'Tư vấn dụng cụ sau khi khách mua vợt Axforce 80 tại quầy',                  '2026-10-09 11:30:00', '2026-11-09'),
	(3,  6, 'goi_dien',      'Liên hệ giải đáp khiếu nại về việc tổng đài trả lời chậm',                  '2026-10-12 10:30:00', '2026-10-19');

-- 11. Loại sản phẩm
INSERT INTO LOAI_SP (ten_loai) VALUES ('Quần áo'), ('Giày'), ('Vợt cầu lông'), ('Phụ kiện');

-- 12. Sản phẩm (quần và áo thuộc loại 1 "Quần áo", dụng cụ thuộc loại 4 "Phụ kiện");
INSERT INTO SAN_PHAM (ma_loai, ten_san_pham, don_vi_tinh, mo_ta, trang_thai) VALUES
	(1, 'Áo thể thao Dri-Fit',                  'cái', 'Áo thun thể thao thoáng khí',                                                'hien'),       -- 1
	(2, 'Giày chạy bộ Pegasus',                 'đôi', 'Giày chạy bộ êm, nhẹ',                                                       'hien'),       -- 2
	(3, 'Vợt cầu lông Yonex Astrox 77',         'cây', 'Vợt thiên công, trọng lượng 4U',                                             'hien'),       -- 3
	(4, 'Quấn cán vợt Yonex',                   'cái', 'Băng quấn cán chống trơn',                                                   'hien'),       -- 4
	(4, 'Túi đựng vợt 2 ngăn',                  'cái', 'Túi đựng được 6 cây vợt',                                                    'an'),         -- 5
	(2, 'Giày đá bóng Nike Mercurial Vapor',    'đôi', 'Giày đá bóng sân cỏ nhân tạo, đế TF bám sân tốt, thân giày ôm chân',         'hien'),     	-- 6
	(2, 'Giày cầu lông Yonex Power Cushion 65Z','đôi', 'Giày cầu lông đệm êm, hỗ trợ di chuyển nhanh và giảm chấn',                  'hien'),     	-- 7
	(2, 'Giày chạy bộ Adidas Ultraboost Light', 'đôi', 'Giày chạy bộ đệm Boost nhẹ, hoàn trả năng lượng tốt',                        'hien'),    	-- 8
	(2, 'Giày tập gym Nike Metcon',             'đôi', 'Giày tập luyện đế phẳng, ổn định khi nâng tạ và tập cường độ cao',            'hien'),      -- 9
	(3, 'Vợt cầu lông Yonex Nanoflare 700',     'cây', 'Vợt thiên tốc độ, đầu nhẹ, phù hợp lối chơi phản tạt nhanh',                  'hien'),   	-- 10
	(3, 'Vợt cầu lông Lining Axforce 80',       'cây', 'Vợt thiên công, khung cứng, cho cú đập mạnh',                                 'hien'),      -- 11
	(3, 'Vợt cầu lông Victor Auraspeed 90S',    'cây', 'Vợt cân bằng, thân vợt linh hoạt, dễ kiểm soát cầu',                          'hien'),   	-- 12
	(1, 'Quần short thể thao nam Dri-Fit',      'cái', 'Quần short chạy bộ, tập gym, vải nhanh khô, lưng thun có dây rút',            'hien'),     	-- 13
	(1, 'Quần legging nữ tập gym',              'cái', 'Legging co giãn 4 chiều, lưng cao, ôm vừa vặn khi tập yoga/gym',              'hien'),    	-- 14
	(1, 'Quần jogger thể thao',                 'cái', 'Quần dài ống bo gấu, chất nỉ mỏng thoáng, mặc tập luyện hoặc đi chơi',        'hien'),      -- 15
	(1, 'Quần short cầu lông Yonex',            'cái', 'Quần short cầu lông co giãn, thấm hút mồ hôi tốt',                            'hien'),      -- 16
	(1, 'Áo thun cầu lông Yonex nam',           'cái', 'Áo thi đấu cầu lông cổ tròn, vải mè thoáng khí',                              'hien'),  	-- 17
	(1, 'Áo polo thể thao Adidas',              'cái', 'Áo polo vải thun lạnh, phù hợp tennis, golf và mặc hằng ngày',                'hien'),     	-- 18
	(1, 'Áo bra thể thao nữ',                   'cái', 'Áo bra nâng đỡ vừa, mút tháo rời, dùng cho yoga và gym',                      'hien'),      -- 19
	(1, 'Áo khoác gió thể thao',                'cái', 'Áo khoác nhẹ chống gió, chống nước nhẹ, có thể gấp gọn',                      'hien'),      -- 20
	(4, 'Ống cầu lông Yonex AS-50',             'ống', 'Ống 12 quả cầu lông lông vịt, độ bay ổn định, dùng thi đấu',                  'hien'),     	-- 21
	(4, 'Bóng đá Adidas',                       'quả', 'Bóng đá khâu máy, độ nảy tốt, bền trên sân cỏ nhân tạo',                      'hien'),     	-- 22
	(4, 'Dây nhảy tốc độ',                      'cái', 'Dây nhảy cáp thép bọc nhựa, tay cầm chống trượt, chỉnh được độ dài',          'hien'),     	-- 23
	(4, 'Tạ tay cao su',                        'đôi', 'Tạ tay bọc cao su chống trượt, dùng tập tại nhà',                             'hien'),      -- 24
	(4, 'Thảm tập yoga TPE',                    'tấm', 'Thảm yoga chống trượt hai mặt, dày 6mm, kèm dây đeo',                         'hien'),      -- 25
	(4, 'Con lăn foam massage',                 'cái', 'Con lăn giãn cơ, giảm mỏi sau tập luyện',                                     'hien'),      -- 26
	(1, 'Áo thun thể thao nữ Nike Dri-FIT',     'cái', 'Áo thun nữ thấm hút mồ hôi, form suông nhẹ, dùng tập gym và chạy bộ', 'hien'),  -- 27
	(1, 'Áo tank top gym nam',                  'cái', 'Áo ba lỗ tập gym, vải co giãn thoáng khí', 'hien'),  -- 28
	(1, 'Áo hoodie thể thao nỉ',                'cái', 'Áo hoodie nỉ mỏng, có mũ và túi trước, giữ ấm khi tập ngoài trời', 'hien'),  -- 29
	(1, 'Áo thun chạy bộ Adidas Own The Run',   'cái', 'Áo chạy bộ nhẹ, có chi tiết phản quang cho chạy buổi tối', 'hien'),  -- 30
	(1, 'Áo đá bóng Adidas Tiro',               'cái', 'Áo đá bóng vải lưới thoáng, cổ tròn, co giãn tốt', 'hien'),  -- 31
	(1, 'Quần short bóng đá Nike',              'cái', 'Quần short đá bóng nhẹ, lưng thun có dây rút', 'hien'),  -- 32
	(1, 'Quần dài thể thao Adidas 3 sọc',       'cái', 'Quần dài ống đứng, 3 sọc bên hông, có túi khóa kéo', 'hien'),  -- 33
	(1, 'Quần legging nam giữ nhiệt',           'cái', 'Quần bó giữ nhiệt mặc lót khi chạy bộ, đá bóng mùa lạnh', 'hien'),  -- 34
	(1, 'Váy tennis nữ',                        'cái', 'Váy tennis xếp ly kèm quần lót trong, vải co giãn', 'hien'),  -- 35
	(1, 'Bộ đồ thể thao nỉ nam',                'bộ', 'Bộ áo và quần nỉ thể thao, dùng tập luyện và mặc hằng ngày', 'hien'),  -- 36
	(2, 'Giày chạy bộ Asics Gel-Kayano 30',     'đôi', 'Giày chạy bộ ổn định, đệm gel êm, phù hợp người chạy đường dài', 'hien'),  -- 37
	(2, 'Giày chạy bộ Hoka Clifton 9',          'đôi', 'Giày chạy bộ đế dày, nhẹ và êm, giảm chấn tốt', 'hien'),  -- 38
	(2, 'Giày đá bóng Adidas Predator',        	'đôi', 'Giày đá bóng sân cỏ nhân tạo, thân giày ôm chân, kiểm soát bóng tốt', 'hien'),  -- 39
	(2, 'Giày đá bóng Puma Future', 			'đôi', 'Giày đá bóng đế TF, thân giày dệt co giãn', 'hien'),  -- 40
	(2, 'Giày cầu lông Li-Ning Saga', 			'đôi', 'Giày cầu lông đế cao su bám sàn, thân lưới thoáng', 'hien'),  -- 41
	(2, 'Giày cầu lông Victor P9200', 			'đôi', 'Giày cầu lông hỗ trợ cổ chân, đế chống trượt', 'hien'),  -- 42
	(2, 'Giày bóng rổ Under Armour Curry Flow', 'đôi', 'Giày bóng rổ cổ thấp, đế bám sàn tốt, đệm êm', 'hien'),  -- 43
	(2, 'Giày tennis Asics Gel-Resolution', 	'đôi', 'Giày tennis ổn định, bền, phù hợp sân cứng', 'hien'),  -- 44
	(2, 'Giày tập luyện Reebok Nano X4', 		'đôi', 'Giày tập đa năng, đế phẳng, dùng cho gym và crossfit', 'hien'),  -- 45
	(2, 'Giày chạy bộ Brooks Ghost 16', 		'đôi', 'Giày chạy bộ đệm mềm, phù hợp chạy hằng ngày', 'hien'),  -- 46
	(3, 'Vợt cầu lông Yonex Astrox 99 Pro', 	'cây', 'Vợt thiên công, đầu nặng, dành cho người chơi lực tốt', 'hien'),  -- 47
	(3, 'Vợt cầu lông Yonex Arcsaber 11 Pro', 	'cây', 'Vợt cân bằng, kiểm soát cầu chính xác, phù hợp lối chơi kỹ thuật', 'hien'),  -- 48
	(3, 'Vợt cầu lông Yonex Duora 10', 			'cây', 'Vợt hai đầu, đổi mặt vợt linh hoạt để tạo cú đánh khác nhau', 'hien'),  -- 49
	(3, 'Vợt cầu lông Li-Ning Windstorm 72', 	'cây', 'Vợt nhẹ đầu, thiên tốc độ, dễ phản tạt', 'hien'),  -- 50
	(3, 'Vợt cầu lông Li-Ning Bladex 900', 		'cây', 'Vợt thiên công, khung cứng, cho cú smash mạnh', 'hien'),  -- 51
	(3, 'Vợt cầu lông Victor Thruster Ryuga', 	'cây', 'Vợt đầu nặng, lực đập tốt, phù hợp đánh đơn', 'hien'),  -- 52
	(3, 'Vợt cầu lông Victor Jetspeed S12', 	'cây', 'Vợt nhẹ, tốc độ vung nhanh, thiên về phòng thủ và phản tạt', 'hien'),  -- 53
	(3, 'Vợt cầu lông Mizuno Fortius 10', 		'cây', 'Vợt cân bằng, thân vợt đàn hồi tốt, dễ làm quen', 'hien'),  -- 54
	(3, 'Vợt cầu lông Apacs Ziggler LHI', 		'cây', 'Vợt thiên công giá dễ tiếp cận, trọng lượng vừa phải', 'hien'),  -- 55
	(3, 'Vợt cầu lông Kumpoo Power Control', 	'cây', 'Vợt cho người mới chơi, nhẹ và dễ kiểm soát', 'hien'),  -- 56
	(4, 'Túi đựng vợt Yonex Pro 6 cây', 		'cái', 'Túi đựng vợt 6 cây, có ngăn giày và ngăn phụ kiện', 'hien'),  -- 57
	(4, 'Balo thể thao 30L', 					'cái', 'Balo vải chống nước nhẹ, ngăn laptop và ngăn giày riêng', 'hien'),  -- 58
	(4, 'Bình nước thể thao 750ml', 			'cái', 'Bình nước nhựa an toàn, nắp bật, có vạch chia ml', 'hien'),  -- 59
	(4, 'Băng cổ tay thể thao', 				'đôi', 'Băng cổ tay thấm mồ hôi, chất cotton co giãn', 'hien'),  -- 60
	(4, 'Vớ cầu lông Yonex', 					'đôi', 'Vớ dày phần gót và mũi, thấm hút mồ hôi', 'hien'),  -- 61
	(4, 'Mũ lưỡi trai thể thao', 				'cái', 'Mũ vải nhẹ, thoáng khí, có khóa chỉnh size phía sau', 'hien'),  -- 62
	(4, 'Băng đầu gối thể thao', 				'đôi', 'Băng đầu gối co giãn, hỗ trợ khi chạy bộ và đá bóng', 'hien'),  -- 63
	(4, 'Khăn thể thao microfiber', 			'cái', 'Khăn microfiber thấm nước nhanh, gọn nhẹ', 'hien'),  -- 64
	(4, 'Cước vợt Yonex BG65', 					'cuộn', 'Dây cước căng vợt bền, cảm giác đánh tốt', 'hien');  -- 65

-- 13. Biến thể sản phẩm
INSERT INTO BIEN_THE_SP (ma_san_pham, kich_co, mau_sac, gia_ban, gia_von, so_luong_ton, trang_thai, hinh_anh) VALUES
	(1,  'M',   'Đen',   350000,  200000,  0, 'hien', '/images/ao-drifit.jpg'),   -- 1
	(1,  'L',   'Trắng', 350000,  200000,  0, 'hien', '/images/ao-drifit.jpg'),   -- 2
	(2,  '41',  'Xanh',  2500000, 1700000, 0, 'hien', '/images/giay-pegasus.jpg'),   -- 3
	(2,  '42',  'Đen',   2500000, 1700000, 0, 'hien', '/images/giay-pegasus.jpg'),   -- 4
	(3,  '4U',  'Đỏ',    3200000, 2300000, 0, 'hien', '/images/vot-astrox77.jpg'),   -- 5
	(4,  NULL,  'Trắng', 50000,   25000,   0, 'hien', '/images/quan-can.jpg'),   -- 6
	(5,  NULL,  'Đen',   450000,  280000,  0, 'an', '/images/tui-vot.jpg'),     -- 7
	(6,  '40',  'Cam',   2200000, 1500000, 0, 'hien', '/images/giay-mercurial.jpg'),   -- 8
	(6,  '41',  'Cam',   2200000, 1500000, 0, 'hien', '/images/giay-mercurial.jpg'),   -- 9
	(6,  '42',  'Đen',   2200000, 1500000, 0, 'hien', '/images/giay-mercurial.jpg'),   -- 10
	(7,  '40',  'Trắng', 2400000, 1650000, 0, 'hien', '/images/giay-yonex-65z.jpg'),   -- 11
	(7,  '41',  'Trắng', 2400000, 1650000, 0, 'hien', '/images/giay-yonex-65z.jpg'),   -- 12
	(7,  '42',  'Xanh',  2400000, 1650000, 0, 'hien', '/images/giay-yonex-65z.jpg'),   -- 13
	(8,  '41',  'Đen',   4200000, 3000000, 0, 'hien', '/images/giay-ultraboost.jpg'),   -- 14
	(8,  '42',  'Xám',   4200000, 3000000, 0, 'hien', '/images/giay-ultraboost.jpg'),   -- 15
	(9,  '41',  'Đen',   3500000, 2500000, 0, 'hien', '/images/giay-metcon.jpg'),   -- 16
	(9,  '42',  'Trắng', 3500000, 2500000, 0, 'hien', '/images/giay-metcon.jpg'),   -- 17
	(10, '3U',  'Xanh',  3600000, 2600000, 0, 'hien', '/images/vot-nanoflare700.jpg'),   -- 18
	(10, '4U',  'Xanh',  3600000, 2600000, 0, 'hien', '/images/vot-nanoflare700.jpg'),   -- 19
	(11, '3U',  'Đen',   3300000, 2350000, 0, 'hien', '/images/vot-axforce80.jpg'),   -- 20
	(11, '4U',  'Đen',   3300000, 2350000, 0, 'hien', '/images/vot-axforce80.jpg'),   -- 21
	(12, '4U',  'Xám',   3800000, 2700000, 0, 'hien', '/images/vot-auraspeed90s.jpg'),   -- 22
	(13, 'M',   'Đen',   450000,  270000,  0, 'hien', '/images/quan-short-nam.jpg'),   -- 23
	(13, 'L',   'Đen',   450000,  270000,  0, 'hien', '/images/quan-short-nam.jpg'),   -- 24
	(13, 'XL',  'Xám',   450000,  270000,  0, 'hien', '/images/quan-short-nam.jpg'),   -- 25
	(14, 'S',   'Đen',   520000,  310000,  0, 'hien', '/images/quan-legging-nu.jpg'),   -- 26
	(14, 'M',   'Đen',   520000,  310000,  0, 'hien', '/images/quan-legging-nu.jpg'),   -- 27
	(14, 'L',   'Tím',   520000,  310000,  0, 'hien', '/images/quan-legging-nu.jpg'),   -- 28
	(15, 'M',   'Xám',   590000,  360000,  0, 'hien', '/images/quan-jogger.jpg'),   -- 29
	(15, 'L',   'Xám',   590000,  360000,  0, 'hien', '/images/quan-jogger.jpg'),   -- 30
	(15, 'L',   'Đen',   590000,  360000,  0, 'hien', '/images/quan-jogger.jpg'),   -- 31
	(16, 'M',   'Trắng', 380000,  220000,  0, 'hien', '/images/quan-cau-long.jpg'),   -- 32
	(16, 'L',   'Navy',  380000,  220000,  0, 'hien', '/images/quan-cau-long.jpg'),   -- 33
	(17, 'M',   'Xanh',  420000,  250000,  0, 'hien', '/images/ao-cau-long-yonex.jpg'),   -- 34
	(17, 'L',   'Xanh',  420000,  250000,  0, 'hien', '/images/ao-cau-long-yonex.jpg'),   -- 35
	(17, 'L',   'Trắng', 420000,  250000,  0, 'hien', '/images/ao-cau-long-yonex.jpg'),   -- 36
	(18, 'M',   'Trắng', 480000,  290000,  0, 'hien', '/images/ao-polo-adidas.jpg'),   -- 37
	(18, 'L',   'Navy',  480000,  290000,  0, 'hien', '/images/ao-polo-adidas.jpg'),   -- 38
	(19, 'S',   'Hồng',  350000,  200000,  0, 'hien', '/images/ao-bra-nu.jpg'),   -- 39
	(19, 'M',   'Hồng',  350000,  200000,  0, 'hien', '/images/ao-bra-nu.jpg'),   -- 40
	(19, 'M',   'Đen',   350000,  200000,  0, 'hien', '/images/ao-bra-nu.jpg'),   -- 41
	(20, 'M',   'Đen',   750000,  460000,  0, 'hien', '/images/ao-khoac-gio.jpg'),   -- 42
	(20, 'L',   'Đen',   750000,  460000,  0, 'hien', '/images/ao-khoac-gio.jpg'),   -- 43
	(20, 'XL',  'Xanh',  750000,  460000,  0, 'hien', '/images/ao-khoac-gio.jpg'),   -- 44
	(21, NULL,  'Trắng', 620000,  460000,  0, 'hien', '/images/cau-yonex-as50.jpg'),   -- 45
	(22, '5',   'Trắng', 550000,  350000,  0, 'hien', '/images/bong-da-adidas.jpg'),   -- 46
	(22, '4',   'Cam',   450000,  280000,  0, 'hien', '/images/bong-da-adidas.jpg'),   -- 47
	(23, NULL,  'Đen',   120000,  60000,   0, 'hien', '/images/day-nhay.jpg'),   -- 48
	(23, NULL,  'Đỏ',    120000,  60000,   0, 'hien', '/images/day-nhay.jpg'),   -- 49
	(24, '2kg', 'Đen',   180000,  110000,  0, 'hien', '/images/ta-tay.jpg'),   -- 50
	(24, '5kg', 'Đen',   350000,  220000,  0, 'hien', '/images/ta-tay.jpg'),   -- 51
	(25, '6mm', 'Tím',   280000,  160000,  0, 'hien', '/images/tham-yoga.jpg'),   -- 52
	(25, '6mm', 'Xanh',  280000,  160000,  0, 'hien', '/images/tham-yoga.jpg'),   -- 53
	(26, NULL,  'Xám',   220000,  130000,  0, 'hien', '/images/con-lan-foam.jpg'),   -- 54
	(27, 'S', 'Hồng', 480000, 290000, 0, 'hien', '/images/ao-thun-nu-drifit.jpg'),  -- 55
	(27, 'M', 'Hồng', 480000, 290000, 0, 'hien', '/images/ao-thun-nu-drifit.jpg'),  -- 56
	(27, 'M', 'Trắng', 480000, 290000, 0, 'hien', '/images/ao-thun-nu-drifit.jpg'),  -- 57
	(28, 'M', 'Đen', 280000, 160000, 0, 'hien', '/images/ao-tank-top-nam.jpg'),  -- 58
	(28, 'L', 'Đen', 280000, 160000, 0, 'hien', '/images/ao-tank-top-nam.jpg'),  -- 59
	(28, 'L', 'Xám', 280000, 160000, 0, 'hien', '/images/ao-tank-top-nam.jpg'),  -- 60
	(29, 'M', 'Xám', 690000, 420000, 0, 'hien', '/images/ao-hoodie-the-thao.jpg'),  -- 61
	(29, 'L', 'Xám', 690000, 420000, 0, 'hien', '/images/ao-hoodie-the-thao.jpg'),  -- 62
	(29, 'L', 'Đen', 690000, 420000, 0, 'hien', '/images/ao-hoodie-the-thao.jpg'),  -- 63
	(30, 'M', 'Xanh', 650000, 400000, 0, 'hien', '/images/ao-chay-bo-adidas.jpg'),  -- 64
	(30, 'L', 'Xanh', 650000, 400000, 0, 'hien', '/images/ao-chay-bo-adidas.jpg'),  -- 65
	(31, 'M', 'Đỏ', 520000, 310000, 0, 'hien', '/images/ao-da-bong-tiro.jpg'),  -- 66
	(31, 'L', 'Đỏ', 520000, 310000, 0, 'hien', '/images/ao-da-bong-tiro.jpg'),  -- 67
	(31, 'L', 'Xanh', 520000, 310000, 0, 'hien', '/images/ao-da-bong-tiro.jpg'),  -- 68
	(32, 'M', 'Đen', 380000, 220000, 0, 'hien', '/images/quan-short-bong-da.jpg'),  -- 69
	(32, 'L', 'Đen', 380000, 220000, 0, 'hien', '/images/quan-short-bong-da.jpg'),  -- 70
	(33, 'M', 'Đen', 750000, 460000, 0, 'hien', '/images/quan-dai-adidas.jpg'),  -- 71
	(33, 'L', 'Đen', 750000, 460000, 0, 'hien', '/images/quan-dai-adidas.jpg'),  -- 72
	(33, 'XL', 'Xám', 750000, 460000, 0, 'hien', '/images/quan-dai-adidas.jpg'),  -- 73
	(34, 'M', 'Đen', 420000, 250000, 0, 'hien', '/images/quan-legging-nam.jpg'),  -- 74
	(34, 'L', 'Đen', 420000, 250000, 0, 'hien', '/images/quan-legging-nam.jpg'),  -- 75
	(35, 'S', 'Trắng', 450000, 270000, 0, 'hien', '/images/vay-tennis-nu.jpg'),  -- 76
	(35, 'M', 'Trắng', 450000, 270000, 0, 'hien', '/images/vay-tennis-nu.jpg'),  -- 77
	(35, 'M', 'Hồng', 450000, 270000, 0, 'hien', '/images/vay-tennis-nu.jpg'),  -- 78
	(36, 'M', 'Xám', 980000, 620000, 0, 'hien', '/images/bo-do-the-thao-ni.jpg'),  -- 79
	(36, 'L', 'Xám', 980000, 620000, 0, 'hien', '/images/bo-do-the-thao-ni.jpg'),  -- 80
	(36, 'L', 'Đen', 980000, 620000, 0, 'hien', '/images/bo-do-the-thao-ni.jpg'),  -- 81
	(37, '41', 'Xanh', 4200000, 3000000, 0, 'hien', '/images/giay-gel-kayano.jpg'),  -- 82
	(37, '42', 'Xanh', 4200000, 3000000, 0, 'hien', '/images/giay-gel-kayano.jpg'),  -- 83
	(37, '43', 'Đen', 4200000, 3000000, 0, 'hien', '/images/giay-gel-kayano.jpg'),  -- 84
	(38, '41', 'Trắng', 3900000, 2800000, 0, 'hien', '/images/giay-hoka-clifton.jpg'),  -- 85
	(38, '42', 'Đen', 3900000, 2800000, 0, 'hien', '/images/giay-hoka-clifton.jpg'),  -- 86
	(39, '40', 'Đỏ', 3200000, 2200000, 0, 'hien', '/images/giay-da-bong-predator.jpg'),  -- 87
	(39, '41', 'Đỏ', 3200000, 2200000, 0, 'hien', '/images/giay-da-bong-predator.jpg'),  -- 88
	(39, '42', 'Đen', 3200000, 2200000, 0, 'hien', '/images/giay-da-bong-predator.jpg'),  -- 89
	(40, '41', 'Cam', 2800000, 1900000, 0, 'hien', '/images/giay-da-bong-puma.jpg'),  -- 90
	(40, '42', 'Cam', 2800000, 1900000, 0, 'hien', '/images/giay-da-bong-puma.jpg'),  -- 91
	(41, '40', 'Trắng', 1900000, 1300000, 0, 'hien', '/images/giay-cau-long-lining.jpg'),  -- 92
	(41, '41', 'Trắng', 1900000, 1300000, 0, 'hien', '/images/giay-cau-long-lining.jpg'),  -- 93
	(41, '42', 'Xanh', 1900000, 1300000, 0, 'hien', '/images/giay-cau-long-lining.jpg'),  -- 94
	(42, '41', 'Trắng', 2600000, 1800000, 0, 'hien', '/images/giay-cau-long-victor.jpg'),  -- 95
	(42, '42', 'Đen', 2600000, 1800000, 0, 'hien', '/images/giay-cau-long-victor.jpg'),  -- 96
	(43, '42', 'Đen', 3400000, 2400000, 0, 'hien', '/images/giay-bong-ro-curry.jpg'),  -- 97
	(43, '43', 'Trắng', 3400000, 2400000, 0, 'hien', '/images/giay-bong-ro-curry.jpg'),  -- 98
	(44, '41', 'Trắng', 3600000, 2550000, 0, 'hien', '/images/giay-tennis-asics.jpg'),  -- 99
	(44, '42', 'Trắng', 3600000, 2550000, 0, 'hien', '/images/giay-tennis-asics.jpg'),  -- 100
	(45, '41', 'Đen', 3300000, 2300000, 0, 'hien', '/images/giay-reebok-nano.jpg'),  -- 101
	(45, '42', 'Xám', 3300000, 2300000, 0, 'hien', '/images/giay-reebok-nano.jpg'),  -- 102
	(46, '41', 'Xanh', 3800000, 2700000, 0, 'hien', '/images/giay-brooks-ghost-men.jpg'),  -- 103
	(46, '42', 'Xám', 3800000, 2700000, 0, 'hien', '/images/giay-brooks-ghost-women.jpg'),  -- 104
	(46, '43', 'Đen', 3800000, 2700000, 0, 'hien', '/images/giay-brooks-ghost-men.jpg'),  -- 105
	(47, '3U', 'Xanh', 4500000, 3300000, 0, 'hien', '/images/vot-astrox99.jpg'),  -- 106
	(47, '4U', 'Xanh', 4500000, 3300000, 0, 'hien', '/images/vot-astrox99.jpg'),  -- 107
	(48, '3U', 'Xám', 4200000, 3000000, 0, 'hien', '/images/vot-arcsaber11.jpg'),  -- 108
	(48, '4U', 'Xám', 4200000, 3000000, 0, 'hien', '/images/vot-arcsaber11.jpg'),  -- 109
	(49, '4U', 'Đen', 3400000, 2400000, 0, 'hien', '/images/vot-duora10.jpg'),  -- 110
	(50, '3U', 'Đỏ', 2900000, 2000000, 0, 'hien', '/images/vot-windstorm72.jpg'),  -- 111
	(50, '4U', 'Đỏ', 2900000, 2000000, 0, 'hien', '/images/vot-windstorm72.jpg'),  -- 112
	(51, '4U', 'Xanh', 3200000, 2250000, 0, 'hien', '/images/vot-bladex900.jpg'),  -- 113
	(52, '3U', 'Xanh', 3700000, 2650000, 0, 'hien', '/images/vot-thruster-ryuga.jpg'),  -- 114
	(52, '4U', 'Xanh', 3700000, 2650000, 0, 'hien', '/images/vot-thruster-ryuga.jpg'),  -- 115
	(53, '4U', 'Trắng', 3300000, 2350000, 0, 'hien', '/images/vot-jetspeed-s12.jpg'),  -- 116
	(54, '4U', 'Đen', 2800000, 1950000, 0, 'hien', '/images/vot-mizuno-fortius.jpg'),  -- 117
	(55, '3U', 'Đỏ', 2400000, 1650000, 0, 'hien', '/images/vot-apacs-ziggler.jpg'),  -- 118
	(55, '4U', 'Đỏ', 2400000, 1650000, 0, 'hien', '/images/vot-apacs-ziggler.jpg'),  -- 119
	(56, '4U', 'Xám', 1800000, 1200000, 0, 'hien', '/images/vot-kumpoo-power.jpg'),  -- 120
	(57, NULL, 'Đen', 1450000, 950000, 0, 'hien', '/images/tui-vot-yonex-pro-black.jpg'),  -- 121
	(57, NULL, 'Xanh', 1450000, 950000, 0, 'hien', '/images/tui-vot-yonex-pro-darkgreen.jpg'),  -- 122
	(58, NULL, 'Đen', 650000, 400000, 0, 'hien', '/images/balo-the-thao-30l.jpg'),  -- 123
	(58, NULL, 'Xám', 650000, 400000, 0, 'hien', '/images/balo-the-thao-30l.jpg'),  -- 124
	(59, NULL, 'Xanh', 180000, 90000, 0, 'hien', '/images/binh-nuoc-750ml.jpg'),  -- 125
	(59, NULL, 'Đen', 180000, 90000, 0, 'hien', '/images/binh-nuoc-750ml.jpg'),  -- 126
	(60, NULL, 'Đen', 60000, 30000, 0, 'hien', '/images/bang-co-tay.jpg'),  -- 127
	(60, NULL, 'Trắng', 60000, 30000, 0, 'hien', '/images/bang-co-tay.jpg'),  -- 128
	(61, 'M', 'Trắng', 120000, 65000, 0, 'hien', '/images/vo-cau-long-yonex.jpg'),  -- 129
	(61, 'L', 'Trắng', 120000, 65000, 0, 'hien', '/images/vo-cau-long-yonex.jpg'),  -- 130
	(62, NULL, 'Đen', 250000, 140000, 0, 'hien', '/images/mu-luoi-trai.jpg'),  -- 131
	(62, NULL, 'Trắng', 250000, 140000, 0, 'hien', '/images/mu-luoi-trai.jpg'),  -- 132
	(63, 'M', 'Đen', 220000, 120000, 0, 'hien', '/images/bang-dau-goi.jpg'),  -- 133
	(63, 'L', 'Đen', 220000, 120000, 0, 'hien', '/images/bang-dau-goi.jpg'),  -- 134
	(64, NULL, 'Xanh', 150000, 80000, 0, 'hien', '/images/khan-the-thao.jpg'),  -- 135
	(64, NULL, 'Xám', 150000, 80000, 0, 'hien', '/images/khan-the-thao.jpg'),  -- 136
	(65, NULL, 'Trắng', 170000, 100000, 0, 'hien', '/images/cuoc-vot-webp.jpg'),  -- 137
	(65, NULL, 'Vàng', 170000, 100000, 0, 'hien', '/images/cuoc-vot-webp.jpg');  -- 138
-- 14. Nhà cung cấp
INSERT INTO NHA_CUNG_CAP (ten_ncc, so_dien_thoai, email, dia_chi, trang_thai) VALUES
	 ('Công ty TNHH Thể Thao Việt',        '02838000001', 'sales@thethaoviet.vn',   'Quận Tân Bình, TP.HCM',   'hop_tac'),       -- 1
	 ('Nhà phân phối Yonex Miền Nam',      '02838000002', 'yonex.mn@gmail.com',     'Quận 10, TP.HCM',         'hop_tac'),       -- 2
	 ('Xưởng may Hòa Phát',                '02838000003', NULL,                     'Bình Dương',              'ngung_hop_tac'), -- 3
	 ('Công ty CP Giày Thể Thao Sài Gòn',  '02838000004', 'giay.sg@gmail.com',      'Quận Bình Thạnh, TP.HCM', 'hop_tac'),       -- 4
	 ('Nhà phân phối Adidas Miền Nam',     '02838000005', 'adidas.mn@gmail.com',    'Quận 7, TP.HCM',          'hop_tac'),       -- 5
	 ('Đại lý Li-Ning & Victor Việt Nam',  '02838000006', 'vot.lv@gmail.com',       'Quận 3, TP.HCM',          'hop_tac'),       -- 6
	 ('Công ty TNHH Dụng Cụ Gym Á Châu',   '02838000007', 'gym.achau@gmail.com',    'TP. Thủ Đức, TP.HCM',     'hop_tac'),       -- 7
	 ('Xưởng may Thành Công',              '02838000008', NULL,                     'Bình Dương',              'hop_tac'),       -- 8
	 ('Công ty TNHH Yoga Fitness Việt',    '02838000009', 'yoga.fitness@gmail.com', 'Quận Gò Vấp, TP.HCM',     'hop_tac'),       -- 9
	 ('Nhà phân phối Bóng Việt',           '02838000010', 'bongviet@gmail.com',     'Quận 12, TP.HCM',         'ngung_hop_tac'); -- 10

-- 15. Phiếu nhập hàng (tong_tien được tính lại ở UPDATE bên dưới)
INSERT INTO PHIEU_NHAP_HANG (ngay_nhap, ma_nhan_vien, ma_ncc, tong_tien) VALUES
	 ('2026-09-01 09:00:00', 3, 1,  0),   -- 1: hàng ban đầu (áo, giày: biến thể 1-4)
	 ('2026-09-02 09:30:00', 3, 2,  0),   -- 2: Yonex và phụ kiện (biến thể 5-7)
	 ('2026-09-03 09:00:00', 3, 4,  0),   -- 3: giày Nike (SP 6, 9)
	 ('2026-09-04 10:00:00', 5, 5,  0),   -- 4: Adidas (SP 8, 18)
	 ('2026-09-05 09:30:00', 5, 2,  0),   -- 5: Yonex (SP 7, 10, 16, 17, 21)
	 ('2026-09-06 14:00:00', 3, 6,  0),   -- 6: Li-Ning, Victor (SP 11, 12)
	 ('2026-09-08 08:30:00', 5, 8,  0),   -- 7: quần áo tập (SP 13, 14, 15, 19, 20)
	 ('2026-09-09 15:00:00', 3, 7,  0),   -- 8: dụng cụ gym (SP 23, 24, 26)
	 ('2026-09-10 09:00:00', 5, 9,  0),   -- 9: thảm yoga (SP 25)
	 ('2026-09-12 11:00:00', 3, 10, 0),   -- 10: bóng đá (SP 22)
	 ('2026-09-14 09:00:00', 3, 4, 0),   -- 11: giày (SP 37-46)
	 ('2026-09-15 09:00:00', 5, 2, 0),   -- 12: vợt (SP 47-56)
	 ('2026-09-16 09:00:00', 5, 8, 0),   -- 13: quần áo (SP 27-36)
	 ('2026-09-17 09:00:00', 3, 7, 0);   -- 14: phụ kiện (SP 57-66)

-- 16. Chi tiết phiếu nhập hàng
INSERT INTO CHI_TIET_PHIEU_NHAP_HANG (ma_phieu_nhap, ma_bien_the, so_luong, don_gia, thanh_tien) VALUES
	 (1, 1,  30,  200000,  6000000),
	 (1, 2,  30,  200000,  6000000),
	 (1, 3,  10,  1700000, 17000000),
	 (1, 4,  10,  1700000, 17000000),
	 (2, 5,  8,   2300000, 18400000),
	 (2, 6,  100, 25000,   2500000),
	 (2, 7,  5,   280000,  1400000),
	 -- phiếu 3
	 (3, 8,  8,   1500000, 12000000),
	 (3, 9,  12,  1500000, 18000000),
	 (3, 10, 10,  1500000, 15000000),
	 (3, 16, 6,   2500000, 15000000),
	 (3, 17, 6,   2500000, 15000000),
	 -- phiếu 4
	 (4, 14, 5,   3000000, 15000000),
	 (4, 15, 5,   3000000, 15000000),
	 (4, 37, 18,  290000,  5220000),
	 (4, 38, 18,  290000,  5220000),
	 -- phiếu 5
	 (5, 11, 6,   1650000, 9900000),
	 (5, 12, 9,   1650000, 14850000),
	 (5, 13, 7,   1650000, 11550000),
	 (5, 18, 8,   2600000, 20800000),
	 (5, 19, 10,  2600000, 26000000),
	 (5, 32, 20,  220000,  4400000),
	 (5, 33, 20,  220000,  4400000),
	 (5, 34, 25,  250000,  6250000),
	 (5, 35, 25,  250000,  6250000),
	 (5, 36, 20,  250000,  5000000),
	 (5, 45, 40,  460000,  18400000),
	 -- phiếu 6
	 (6, 20, 6,   2350000, 14100000),
	 (6, 21, 8,   2350000, 18800000),
	 (6, 22, 5,   2700000, 13500000),
	 -- phiếu 7
	 (7, 23, 30,  270000,  8100000),
	 (7, 24, 30,  270000,  8100000),
	 (7, 25, 20,  270000,  5400000),
	 (7, 26, 25,  310000,  7750000),
	 (7, 27, 25,  310000,  7750000),
	 (7, 28, 15,  310000,  4650000),
	 (7, 29, 20,  360000,  7200000),
	 (7, 30, 20,  360000,  7200000),
	 (7, 31, 18,  360000,  6480000),
	 (7, 39, 22,  200000,  4400000),
	 (7, 40, 22,  200000,  4400000),
	 (7, 41, 22,  200000,  4400000),
	 (7, 42, 12,  460000,  5520000),
	 (7, 43, 12,  460000,  5520000),
	 (7, 44, 10,  460000,  4600000),
	 -- phiếu 8
	 (8, 48, 50,  60000,   3000000),
	 (8, 49, 50,  60000,   3000000),
	 (8, 50, 30,  110000,  3300000),
	 (8, 51, 20,  220000,  4400000),
	 (8, 54, 20,  130000,  2600000),
	 -- phiếu 9
	 (9, 52, 25,  160000,  4000000),
	 (9, 53, 25,  160000,  4000000),
	 -- phiếu 10
	 (10, 46, 15, 350000,  5250000),
	 (10, 47, 15, 280000,  4200000),
	 -- phiếu 11
	 (11, 82, 5, 3000000, 15000000),
	 (11, 83, 5, 3000000, 15000000),
	 (11, 84, 5, 3000000, 15000000),
	 (11, 85, 5, 2800000, 14000000),
	 (11, 86, 5, 2800000, 14000000),
	 (11, 87, 6, 2200000, 13200000),
	 (11, 88, 6, 2200000, 13200000),
	 (11, 89, 6, 2200000, 13200000),
	 (11, 90, 6, 1900000, 11400000),
	 (11, 91, 6, 1900000, 11400000),
	 (11, 92, 8, 1300000, 10400000),
	 (11, 93, 8, 1300000, 10400000),
	 (11, 94, 8, 1300000, 10400000),
	 (11, 95, 6, 1800000, 10800000),
	 (11, 96, 6, 1800000, 10800000),
	 (11, 97, 5, 2400000, 12000000),
	 (11, 98, 5, 2400000, 12000000),
	 (11, 99, 5, 2550000, 12750000),
	 (11, 100, 5, 2550000, 12750000),
	 (11, 101, 6, 2300000, 13800000),
	 (11, 102, 6, 2300000, 13800000),
	 (11, 103, 5, 2700000, 13500000),
	 (11, 104, 5, 2700000, 13500000),
	 (11, 105, 5, 2700000, 13500000),
	 -- phiếu 12
	 (12, 106, 6, 3300000, 19800000),
	 (12, 107, 6, 3300000, 19800000),
	 (12, 108, 6, 3000000, 18000000),
	 (12, 109, 6, 3000000, 18000000),
	 (12, 110, 6, 2400000, 14400000),
	 (12, 111, 8, 2000000, 16000000),
	 (12, 112, 8, 2000000, 16000000),
	 (12, 113, 6, 2250000, 13500000),
	 (12, 114, 6, 2650000, 15900000),
	 (12, 115, 6, 2650000, 15900000),
	 (12, 116, 7, 2350000, 16450000),
	 (12, 117, 5, 1950000, 9750000),
	 (12, 118, 8, 1650000, 13200000),
	 (12, 119, 8, 1650000, 13200000),
	 (12, 120, 10, 1200000, 12000000),
	 -- phiếu 13
	 (13, 55, 20, 290000, 5800000),
	 (13, 56, 20, 290000, 5800000),
	 (13, 57, 20, 290000, 5800000),
	 (13, 58, 25, 160000, 4000000),
	 (13, 59, 25, 160000, 4000000),
	 (13, 60, 25, 160000, 4000000),
	 (13, 61, 15, 420000, 6300000),
	 (13, 62, 15, 420000, 6300000),
	 (13, 63, 15, 420000, 6300000),
	 (13, 64, 15, 400000, 6000000),
	 (13, 65, 15, 400000, 6000000),
	 (13, 66, 20, 310000, 6200000),
	 (13, 67, 20, 310000, 6200000),
	 (13, 68, 20, 310000, 6200000),
	 (13, 69, 20, 220000, 4400000),
	 (13, 70, 20, 220000, 4400000),
	 (13, 71, 12, 460000, 5520000),
	 (13, 72, 12, 460000, 5520000),
	 (13, 73, 12, 460000, 5520000),
	 (13, 74, 18, 250000, 4500000),
	 (13, 75, 18, 250000, 4500000),
	 (13, 76, 14, 270000, 3780000),
	 (13, 77, 14, 270000, 3780000),
	 (13, 78, 14, 270000, 3780000),
	 (13, 79, 10, 620000, 6200000),
	 (13, 80, 10, 620000, 6200000),
	 (13, 81, 10, 620000, 6200000),
	 -- phiếu 14
	 (14, 121, 10, 950000, 9500000),
	 (14, 122, 10, 950000, 9500000),
	 (14, 123, 15, 400000, 6000000),
	 (14, 124, 15, 400000, 6000000),
	 (14, 125, 40, 90000, 3600000),
	 (14, 126, 40, 90000, 3600000),
	 (14, 127, 60, 30000, 1800000),
	 (14, 128, 60, 30000, 1800000),
	 (14, 129, 50, 65000, 3250000),
	 (14, 130, 50, 65000, 3250000),
	 (14, 131, 25, 140000, 3500000),
	 (14, 132, 25, 140000, 3500000),
	 (14, 133, 30, 120000, 3600000),
	 (14, 134, 30, 120000, 3600000),
	 (14, 135, 40, 80000, 3200000),
	 (14, 136, 40, 80000, 3200000),
	 (14, 137, 40, 100000, 4000000),
	 (14, 138, 40, 100000, 4000000);

UPDATE PHIEU_NHAP_HANG pn
JOIN (SELECT ma_phieu_nhap, SUM(thanh_tien) AS tong FROM CHI_TIET_PHIEU_NHAP_HANG GROUP BY ma_phieu_nhap) ct
ON ct.ma_phieu_nhap = pn.ma_phieu_nhap
SET pn.tong_tien = ct.tong
WHERE pn.ma_phieu_nhap > 0;

-- Phiếu nhập: cộng tồn kho, giá vốn theo đơn giá nhập gần nhất
UPDATE BIEN_THE_SP bt
JOIN CHI_TIET_PHIEU_NHAP_HANG ct ON ct.ma_bien_the = bt.ma_bien_the
SET bt.so_luong_ton = bt.so_luong_ton + ct.so_luong,
    bt.gia_von      = ct.don_gia
WHERE bt.ma_bien_the > 0;

-- 17. Chương trình khuyến mãi
INSERT INTO CTKM (ten_ctkm, loai_khuyen_mai, ngay_bat_dau, ngay_ket_thuc, gia_tri_giam_toi_da, hang_ap_dung, so_luong_gioi_han, so_luot_da_dung, gioi_han_khach_hang, trang_thai) VALUES
	 ('Giảm giá vợt cầu lông tháng 9',         'theo_san_pham', '2026-09-01 00:00:00', '2026-12-31 23:59:59', NULL,    NULL,   NULL, 0, NULL, 'hoat_dong'),  -- 1
	 ('Đơn từ 2 triệu giảm thêm',              'theo_don_hang', '2026-09-01 00:00:00', '2026-12-31 23:59:59', 300000,  NULL,   500,  0, 2,    'hoat_dong'),  -- 2
	 ('Voucher tri ân hạng Vàng',              'theo_voucher',  '2026-09-01 00:00:00', '2026-11-30 23:59:59', 200000,  'vang', 100,  0, 1,    'hoat_dong'),  -- 3
	 ('Giảm giá giày chạy bộ và giày tập gym', 'theo_san_pham', '2026-10-01 00:00:00', '2026-10-31 23:59:59', NULL,    NULL,   200,  0, NULL, 'hoat_dong'),  -- 4
	 ('Combo trang phục tập gym',              'theo_san_pham', '2026-10-01 00:00:00', '2026-11-30 23:59:59', NULL,    NULL,   NULL, 0, NULL, 'hoat_dong'),  -- 5
	 ('Xả kho dụng cụ tập luyện',              'theo_san_pham', '2026-09-15 00:00:00', '2026-10-05 23:59:59', NULL,    NULL,   100,  0, NULL, 'het_han'),    -- 6
	 ('Black Friday 2026',                     'theo_don_hang', '2026-11-20 00:00:00', '2026-11-30 23:59:59', 1000000, NULL,   300,  0, 1,    'hoat_dong'),  -- 7
	 ('Đơn từ 1 triệu giảm nhẹ',               'theo_don_hang', '2026-10-01 00:00:00', '2026-12-31 23:59:59', 200000,  NULL,   NULL, 0, 3,    'hoat_dong'),  -- 8
	 ('Voucher chào bạn mới',                  'theo_voucher',  '2026-09-01 00:00:00', '2026-12-31 23:59:59', 100000,  NULL,   500,  0, 1,    'hoat_dong'),  -- 9
	 ('Voucher sinh nhật hạng Bạc',            'theo_voucher',  '2026-09-01 00:00:00', '2026-12-31 23:59:59', 150000,  'bac',  50,   0, 1,    'hoat_dong');  -- 10

-- 18. KM theo sản phẩm (ma_san_pham là id trong SAN_PHAM)
INSERT INTO KM_THEO_SP (ma_ctkm, ma_san_pham, phan_tram_giam) VALUES
	 (1, 3, 10),                              -- Astrox 77
	 (1, 10, 10), (1, 11, 8), (1, 12, 8),     -- vợt cầu lông
	 (4, 8, 12),  (4, 9, 10),                 -- giày chạy bộ, giày tập gym
	 (5, 14, 15), (5, 19, 10),                -- legging nữ, áo bra
	 (6, 23, 20), (6, 24, 15), (6, 26, 25);   -- dây nhảy, tạ tay, con lăn foam

-- 19. KM theo mốc tiền đơn hàng
INSERT INTO KM_THEO_DH (ma_ctkm, muc_tien_don_hang, phan_tram_giam) VALUES
	 (2, 2000000, 5), (2, 5000000, 8), (2, 10000000, 10),
	 (7, 1000000, 10), (7, 3000000, 15), (7, 5000000, 20),
	 (8, 1000000, 3),  (8, 3000000, 5),  (8, 7000000, 8), (8, 10000000, 10);

-- 20. KM theo voucher
INSERT INTO KM_THEO_VOUCHER (ma_ctkm, ma_voucher, phan_tram_giam) VALUES
	 (3,  'VANG10',      10),
	 (3,  'VANG15',      15),
	 (3,  'VANG5',       5),
	 (9,  'WELCOME10',   10),
	 (9,  'WELCOME5',    5),
	 (9,  'NEWBIE15',    15),
	 (9,  'SPORT20',     20),
	 (10, 'SINHNHATBAC', 12),
	 (10, 'BAC8',        8),
	 (10, 'BAC15',       15);

-- 21. Phương thức thanh toán
INSERT INTO PHUONG_THUC_THANH_TOAN (ten_phuong_thuc_thanh_toan, trang_thai) VALUES
	 ('Tiền mặt',                       'hoat_dong'),  -- 1
	 ('Chuyển khoản',                   'hoat_dong'),  -- 2
	 ('Ví điện tử',                     'hoat_dong'),  -- 3
	 ('Thẻ',                            'hoat_dong'),  -- 4
	 ('MoMo',                           'hoat_dong'),  -- 5
	 ('ZaloPay',                        'hoat_dong'),  -- 6
	 ('VNPay',                          'hoat_dong'),  -- 7
	 ('ShopeePay',                      'ngung'),      -- 8
	 ('Thanh toán khi nhận hàng (COD)', 'hoat_dong'),  -- 9
	 ('Trả góp qua thẻ',                'hoat_dong');  -- 10

-- 22. Đơn hàng
-- Đơn  1: An     - online, vợt giảm 10% (KM 1) + đơn từ 2 triệu giảm 5% (KM 2), chuyển khoản, đã giao
-- Đơn  2: Bình   - mua tại quầy (NV Lan), tiền mặt, đã giao
-- Đơn  3: Chi    - online, ví điện tử, chờ xác nhận
-- Đơn  4: An     - online, đặt rồi hủy
-- Đơn  5: Mai    - online, combo gym (KM 5) + đơn từ 1 triệu (KM 8), MoMo, đã giao
-- Đơn  6: Khánh  - online, vợt giảm 10% (KM 1) + đơn từ 2 triệu (KM 2), VNPay, đã giao
-- Đơn  7: Long   - mua tại quầy (NV Hoa), giày giảm 12% (KM 4) + KM 2, tiền mặt, đã giao
-- Đơn  8: Linh   - online, xả kho dụng cụ (KM 6) + voucher WELCOME10 (KM 9), ZaloPay, đã giao
-- Đơn  9: Thịnh  - mua tại quầy (NV Vy), vợt giảm 8% (KM 1) + KM 2, thẻ, đã giao
-- Đơn 10: Bình   - online, COD, khách hủy
-- Đơn 11: Khánh  - online, KM 2, ví điện tử, đang giao
-- Đơn 12: An     - online, vợt giảm 8% (KM 1) + voucher VANG10 (KM 3, giảm tối đa 200.000), chuyển khoản, đang chuẩn bị
INSERT INTO DON_HANG (ma_khach_hang, ma_nhan_vien, ngay_dat, ten_nguoi_nhan, sdt_nguoi_nhan, dia_chi_giao_hang, ma_ctkm,
                      tam_tinh, tong_tien_giam_gia, tong_thanh_toan, ma_phuong_thuc_thanh_toan,
                      trang_thai_thanh_toan, trang_thai_don_hang, ly_do_huy) VALUES
 (1,  NULL, '2026-09-10 20:15:00', 'Phạm Minh An',     '0912000001', '273 An Dương Vương, Phường 3, Quận 5, TP.HCM',  2,    3250000, 466500, 2813500, 2,  'da_thanh_toan',   'da_giao',       NULL),
 (2,  2,    '2026-09-15 17:40:00', 'Võ Thị Bình',      '0912000002', 'Mua tại cửa hàng',                              NULL, 700000,  0,      700000,  1,  'da_thanh_toan',   'da_giao',       NULL),
 (3,  NULL, '2026-10-10 08:05:00', 'Đỗ Ngọc Chi',      '0912000003', '88 Phan Xích Long, Phú Nhuận, TP.HCM',          NULL, 2500000, 0,      2530000, 3,  'chua_thanh_toan', 'cho_xac_nhan',  NULL),
 (1,  NULL, '2026-10-01 21:00:00', 'Phạm Minh An',     '0912000001', '273 An Dương Vương, Phường 3, Quận 5, TP.HCM',  NULL, 350000,  0,      350000,  1,  'chua_thanh_toan', 'da_huy',        'Khách yêu cầu hủy do đặt nhầm size'),
 (6,  NULL, '2026-10-03 09:20:00', 'Nguyễn Thị Mai',   '0912000006', '102 Nguyễn Thị Minh Khai, Quận 3, TP.HCM',      8,    1220000, 180160, 1069840, 5,  'da_thanh_toan',   'da_giao',       NULL),
 (7,  NULL, '2026-10-05 19:45:00', 'Trần Quốc Khánh',  '0912000007', '56 Cách Mạng Tháng Tám, Quận 10, TP.HCM',       2,    4840000, 584000, 4286000, 7,  'da_thanh_toan',   'da_giao',       NULL),
 (8,  4,    '2026-10-06 16:10:00', 'Lê Hoàng Long',    '0912000008', 'Mua tại cửa hàng',                              2,    4320000, 694800, 3625200, 1,  'da_thanh_toan',   'da_giao',       NULL),
 (9,  NULL, '2026-09-28 20:15:00', 'Phan Thùy Linh',   '0912000009', '34 Nguyễn Văn Lượng, Gò Vấp, TP.HCM',           9,    850000,  181750, 698250,  6,  'da_thanh_toan',   'da_giao',       NULL),
 (10, 8,    '2026-10-09 11:00:00', 'Ngô Đức Thịnh',    '0912000010', 'Mua tại cửa hàng',                              2,    3820000, 441800, 3378200, 4,  'da_thanh_toan',   'da_giao',       NULL),
 (2,  NULL, '2026-10-12 14:20:00', 'Võ Thị Bình',      '0912000002', '45 Lê Văn Sỹ, Quận 3, TP.HCM',                  NULL, 590000,  0,      590000,  9,  'chua_thanh_toan', 'da_huy',        'Khách đổi ý, không muốn mua nữa'),
 (7,  NULL, '2026-10-13 18:30:00', 'Trần Quốc Khánh',  '0912000007', '56 Cách Mạng Tháng Tám, Quận 10, TP.HCM',       2,    2200000, 110000, 2120000, 3,  'da_thanh_toan',   'dang_giao',     NULL),
 (1,  NULL, '2026-10-14 10:05:00', 'Phạm Minh An',     '0912000001', '273 An Dương Vương, Phường 3, Quận 5, TP.HCM',  3,    3800000, 504000, 3326000, 2,  'da_thanh_toan',   'dang_chuan_bi', NULL);

-- 23. Chi tiết đơn hàng
INSERT INTO CHI_TIET_DON_HANG (ma_don_hang, ma_bien_the, so_luong, don_gia, giam_gia, thanh_tien) VALUES
 (1,  5,  1, 3200000, 320000, 2880000),
 (1,  6,  1, 50000,   0,      50000),
 (2,  1,  2, 350000,  0,      700000),
 (3,  3,  1, 2500000, 0,      2500000),
 (4,  2,  1, 350000,  0,      350000),
 (5,  26, 1, 520000,  78000,  442000),
 (5,  40, 2, 350000,  70000,  630000),
 (6,  19, 1, 3600000, 360000, 3240000),
 (6,  45, 2, 620000,  0,      1240000),
 (7,  14, 1, 4200000, 504000, 3696000),
 (7,  48, 1, 120000,  0,      120000),
 (8,  51, 1, 350000,  52500,  297500),
 (8,  54, 1, 220000,  55000,  165000),
 (8,  52, 1, 280000,  0,      280000),
 (9,  20, 1, 3300000, 264000, 3036000),
 (9,  6,  2, 50000,   0,      100000),
 (9,  34, 1, 420000,  0,      420000),
 (10, 29, 1, 590000,  0,      590000),
 (11, 9,  1, 2200000, 0,      2200000),
 (12, 22, 1, 3800000, 304000, 3496000);

-- Trừ tồn kho cho các đơn chưa hủy (đơn đã hủy không trừ tồn kho)
UPDATE BIEN_THE_SP bt
JOIN (SELECT ct.ma_bien_the, SUM(ct.so_luong) AS sl
        FROM CHI_TIET_DON_HANG ct JOIN DON_HANG dh ON dh.ma_don_hang = ct.ma_don_hang
       WHERE dh.trang_thai_don_hang <> 'da_huy'
       GROUP BY ct.ma_bien_the) x ON x.ma_bien_the = bt.ma_bien_the
SET bt.so_luong_ton = bt.so_luong_ton - x.sl
WHERE bt.ma_bien_the > 0;

-- 24. Vận chuyển (chỉ đơn online chưa hủy; đơn mua tại quầy không giao)
INSERT INTO VAN_CHUYEN (ma_don_hang, ma_nhan_vien, ten_don_vi_van_chuyen, ma_van_don, phi_van_chuyen, ngay_giao_du_kien, ngay_giao_thuc_te, trang_thai) VALUES
 (1,  2,    'Giao Hàng Nhanh',      'GHN123456789',  30000, '2026-09-13', '2026-09-12', 'da_giao'),
 (3,  NULL, 'Giao Hàng Tiết Kiệm',  NULL,            30000, '2026-10-14', NULL,         'cho_lay_hang'),
 (5,  7,    'Giao Hàng Nhanh',      'GHN987654321',  30000, '2026-10-06', '2026-10-05', 'da_giao'),
 (6,  7,    'Giao Hàng Tiết Kiệm',  'GHTK555000111', 30000, '2026-10-08', '2026-10-08', 'da_giao'),
 (8,  7,    'Viettel Post',         'VTP777888999',  30000, '2026-10-01', '2026-09-30', 'da_giao'),
 (11, 7,    'J&T Express',          'JT1234567890',  30000, '2026-10-16', NULL,         'dang_giao'),
 (12, NULL, 'Giao Hàng Nhanh',      NULL,            30000, '2026-10-17', NULL,         'cho_lay_hang');

-- 25. Hóa đơn (chỉ lập cho đơn đã thanh toán)
INSERT INTO HOA_DON (so_hoa_don, ma_don_hang, ma_phuong_thuc_thanh_toan, ngay_thanh_toan, ngay_xuat,
                     tong_tien, tong_tien_giam_gia, phi_van_chuyen, tong_thanh_toan) VALUES
 ('HD2026-000001', 1,  2, '2026-09-10 20:16:00', '2026-09-10 20:16:00', 3250000, 466500, 30000, 2813500),  -- 1
 ('HD2026-000002', 2,  1, '2026-09-15 17:45:00', '2026-09-15 17:45:00', 700000,  0,      0,     700000),   -- 2
 ('HD2026-000003', 5,  5, '2026-10-03 09:25:00', '2026-10-03 09:25:00', 1220000, 180160, 30000, 1069840),  -- 3
 ('HD2026-000004', 6,  7, '2026-10-05 19:47:00', '2026-10-05 19:47:00', 4840000, 584000, 30000, 4286000),  -- 4
 ('HD2026-000005', 7,  1, '2026-10-06 16:20:00', '2026-10-06 16:20:00', 4320000, 694800, 0,     3625200),  -- 5
 ('HD2026-000006', 8,  6, '2026-09-28 20:17:00', '2026-09-28 20:17:00', 850000,  181750, 30000, 698250),   -- 6
 ('HD2026-000007', 9,  4, '2026-10-09 11:10:00', '2026-10-09 11:10:00', 3820000, 441800, 0,     3378200),  -- 7
 ('HD2026-000008', 11, 3, '2026-10-13 18:35:00', '2026-10-13 18:35:00', 2200000, 110000, 30000, 2120000),  -- 8
 ('HD2026-000009', 12, 2, '2026-10-14 10:10:00', '2026-10-14 10:10:00', 3800000, 504000, 30000, 3326000);  -- 9

-- 26. Chi tiết hóa đơn
INSERT INTO CHI_TIET_HOA_DON (ma_hoa_don, ma_bien_the, so_luong, don_gia, gia_von, giam_gia, thanh_tien) VALUES
	 (1, 5,  1, 3200000, 2300000, 320000, 2880000),
	 (1, 6,  1, 50000,   25000,   0,      50000),
	 (2, 1,  2, 350000,  200000,  0,      700000),
	 (3, 26, 1, 520000,  310000,  78000,  442000),
	 (3, 40, 2, 350000,  200000,  70000,  630000),
	 (4, 19, 1, 3600000, 2600000, 360000, 3240000),
	 (4, 45, 2, 620000,  460000,  0,      1240000),
	 (5, 14, 1, 4200000, 3000000, 504000, 3696000),
	 (5, 48, 1, 120000,  60000,   0,      120000),
	 (6, 51, 1, 350000,  220000,  52500,  297500),
	 (6, 54, 1, 220000,  130000,  55000,  165000),
	 (6, 52, 1, 280000,  160000,  0,      280000),
	 (7, 20, 1, 3300000, 2350000, 264000, 3036000),
	 (7, 6,  2, 50000,   25000,   0,      100000),
	 (7, 34, 1, 420000,  250000,  0,      420000),
	 (8, 9,  1, 2200000, 1500000, 0,      2200000),
	 (9, 22, 1, 3800000, 2700000, 304000, 3496000);

-- 27. Lượt sử dụng khuyến mãi
INSERT INTO LUOT_SU_DUNG_KHUYEN_MAI (ma_ctkm, ma_khach_hang, ma_don_hang, so_tien_giam, ngay_su_dung) VALUES
	 (1, 1,  1,  320000, '2026-09-10 20:15:00'),
	 (2, 1,  1,  146500, '2026-09-10 20:15:00'),
	 (5, 6,  5,  148000, '2026-10-03 09:20:00'),
	 (8, 6,  5,  32160,  '2026-10-03 09:20:00'),
	 (1, 7,  6,  360000, '2026-10-05 19:45:00'),
	 (2, 7,  6,  224000, '2026-10-05 19:45:00'),
	 (4, 8,  7,  504000, '2026-10-06 16:10:00'),
	 (2, 8,  7,  190800, '2026-10-06 16:10:00'),
	 (6, 9,  8,  107500, '2026-09-28 20:15:00'),
	 (9, 9,  8,  74250,  '2026-09-28 20:15:00'),
	 (1, 10, 9,  264000, '2026-10-09 11:00:00'),
	 (2, 10, 9,  177800, '2026-10-09 11:00:00'),
	 (2, 7,  11, 110000, '2026-10-13 18:30:00'),
	 (1, 1,  12, 304000, '2026-10-14 10:05:00'),
	 (3, 1,  12, 200000, '2026-10-14 10:05:00');

UPDATE CTKM c
JOIN (SELECT ma_ctkm, COUNT(*) AS n FROM LUOT_SU_DUNG_KHUYEN_MAI GROUP BY ma_ctkm) l ON l.ma_ctkm = c.ma_ctkm
SET c.so_luot_da_dung = l.n
WHERE c.ma_ctkm > 0;

-- Cộng điểm tích lũy (1 điểm / 10.000đ) và tổng chi tiêu từ hóa đơn
UPDATE KHACH_HANG kh
JOIN (SELECT dh.ma_khach_hang, SUM(hd.tong_thanh_toan) AS tong
        FROM HOA_DON hd JOIN DON_HANG dh ON dh.ma_don_hang = hd.ma_don_hang
       GROUP BY dh.ma_khach_hang) x ON x.ma_khach_hang = kh.ma_khach_hang
SET kh.tong_chi_tieu = kh.tong_chi_tieu + x.tong,
    kh.diem_tich_luy = kh.diem_tich_luy + FLOOR(x.tong / 10000)
WHERE kh.ma_khach_hang > 0;

-- 28. Chiến dịch thông báo
INSERT INTO CHIEN_DICH_THONG_BAO (tieu_de, noi_dung, kenh_gui, ma_ctkm, ma_nhom_kh, thoi_gian_gui, trang_thai, ma_nhan_vien) VALUES
	 ('Vợt cầu lông giảm 10%',        'Vợt Yonex Astrox 77 giảm 10% đến hết năm 2026.',             'email', 1,  3,    '2026-09-05 09:00:00', 'da_gui', 1),  -- 1
	 ('Ưu đãi cuối năm',              'Đơn từ 2 triệu giảm đến 8%.',                                 'app',   2,  NULL, NULL,                  'nhap',   2),  -- 2
	 ('Giày chạy bộ giảm đến 12%',    'Ultraboost, Metcon giảm 10-12% đến hết 31/10.',              'sms',   4,  8,    '2026-10-01 08:00:00', 'da_gui', 6),  -- 3
	 ('Combo đồ tập gym',             'Legging, áo bra giảm đến 15% đến hết tháng 11.',             'app',   5,  9,    '2026-10-01 09:00:00', 'da_gui', 6),  -- 4
	 ('Xả kho dụng cụ tập luyện',     'Tạ tay, dây nhảy, con lăn foam giảm đến 25%.',               'email', 6,  NULL, '2026-09-15 08:00:00', 'da_gui', 2),  -- 5
	 ('Black Friday 2026',            'Chuẩn bị săn deal Black Friday từ ngày 20/11.',              'email', 7,  NULL, NULL,                  'nhap',   1),  -- 6
	 ('Giảm nhẹ cho đơn từ 1 triệu',  'Đơn từ 1 triệu giảm 3%, tối đa 200.000đ.',                   'app',   8,  NULL, '2026-10-01 10:00:00', 'da_gui', 4),  -- 7
	 ('Voucher chào bạn mới',         'Nhập mã WELCOME10 giảm 10% cho đơn đầu tiên.',               'email', 9,  1,    '2026-09-02 09:00:00', 'da_gui', 2),  -- 8
	 ('Quà sinh nhật hạng Bạc',       'Nhập mã SINHNHATBAC giảm 12% trong tháng sinh nhật.',        'sms',   10, NULL, '2026-09-03 09:00:00', 'da_gui', 6),  -- 9
	 ('Tri ân khách chi tiêu cao',    'Voucher VANG10 giảm 10% dành riêng cho khách hạng Vàng.',    'email', 3,  4,    '2026-09-10 09:00:00', 'da_gui', 1);  -- 10

-- 29. Phản hồi
INSERT INTO PHAN_HOI (ma_khach_hang, ma_san_pham, ma_don_hang, loai, so_sao, tieu_de, noi_dung, ngay_gui, trang_thai, an_hien) VALUES
	 (1,  3,    1,    'danh_gia',  5,    'Vợt rất tốt',            'Vợt cầm chắc tay, đập cầu uy lực.',                                '2026-09-14 19:00:00', 'da_tra_loi', 1),  -- 1
	 (2,  1,    2,    'gop_y',     NULL, 'Thêm size XL',           'Mong cửa hàng nhập thêm áo size XL.',                              '2026-09-18 08:30:00', 'dang_xu_ly', 1),  -- 2
	 (3,  NULL, NULL, 'khieu_nai', NULL, 'Phản hồi chậm',          'Tổng đài trả lời chậm khi tôi hỏi đơn hàng.',                      '2026-10-11 10:00:00', 'moi',        1),  -- 3
	 (6,  14,   5,    'danh_gia',  5,    'Legging co giãn tốt',    'Mặc tập yoga rất thoải mái, không bị xệ.',                         '2026-10-08 20:00:00', 'da_tra_loi', 1),  -- 4
	 (7,  10,   6,    'danh_gia',  4,    'Vợt nhẹ, dễ đánh',       'Vợt nhẹ đầu, phản tạt nhanh nhưng hơi khó đập mạnh.',              '2026-10-10 21:00:00', 'da_tra_loi', 1),  -- 5
	 (8,  8,    7,    'danh_gia',  5,    'Giày chạy êm',           'Giày chạy bộ êm chân, đáng tiền.',                                 '2026-10-09 09:00:00', 'da_tra_loi', 1),  -- 6
	 (9,  25,   8,    'danh_gia',  4,    'Thảm tốt',               'Thảm dày, chống trượt tốt, hơi nặng một chút.',                    '2026-10-03 18:30:00', 'da_tra_loi', 1),  -- 7
	 (10, 11,   9,    'danh_gia',  5,    'Vợt thiên công đã tay',  'Vợt cầm chắc tay, đập cầu rất uy lực.',                            '2026-10-11 19:00:00', 'da_tra_loi', 1),  -- 8
	 (1,  NULL, 12,   'khieu_nai', NULL, 'Đơn chuẩn bị quá lâu',   'Đơn đặt hôm qua nhưng vẫn chưa được giao cho đơn vị vận chuyển.',  '2026-10-15 08:30:00', 'dang_xu_ly', 1),  -- 9
	 (4,  NULL, NULL, 'gop_y',     NULL, 'Thêm sản phẩm bóng đá',  'Mong cửa hàng bán thêm găng tay thủ môn và tất đá bóng.',          '2026-10-12 12:00:00', 'moi',        1);  -- 10

-- 30. Trả lời phản hồi
INSERT INTO TRA_LOI_PHAN_HOI (ma_phan_hoi, ma_nhan_vien, noi_dung, ngay_tra_loi) VALUES
	 (1, 2, 'Cảm ơn anh An đã tin dùng sản phẩm của cửa hàng!',                                                '2026-09-14 20:00:00'),
	 (2, 4, 'Cảm ơn góp ý của chị Bình, shop sẽ nhập thêm size XL trong đợt hàng tháng sau.',                  '2026-09-19 09:00:00'),
	 (4, 6, 'Cảm ơn chị Mai đã tin dùng! Shop sẽ sớm có thêm nhiều mẫu đồ tập mới.',                           '2026-10-09 09:30:00'),
	 (5, 6, 'Cảm ơn anh Khánh đã góp ý, nếu thiên về đập cầu anh có thể tham khảo dòng Axforce 80 ạ.',         '2026-10-11 09:00:00'),
	 (6, 2, 'Cảm ơn anh Long đã ủng hộ cửa hàng!',                                                             '2026-10-09 15:00:00'),
	 (7, 4, 'Cảm ơn chị Linh, thảm nặng hơn vì chất liệu TPE dày 6mm giúp chống trượt tốt hơn ạ.',             '2026-10-04 10:00:00'),
	 (8, 8, 'Cảm ơn anh Thịnh đã chọn vợt của shop, chúc anh thi đấu thật tốt!',                               '2026-10-12 09:00:00'),
	 (9, 2, 'Shop xin lỗi anh An, đơn đang được chuẩn bị và sẽ bàn giao cho đơn vị vận chuyển trong hôm nay.',  '2026-10-15 09:00:00'),
	 (9, 1, 'Quản lý đã nhận thông tin và sẽ kiểm tra lại quy trình xử lý đơn hàng.',                           '2026-10-15 09:30:00');

-- 31. Khảo sát
INSERT INTO KHAO_SAT (tieu_de, mo_ta, ngay_bat_dau, ngay_ket_thuc, trang_thai, ma_nhan_vien, ngay_tao) VALUES
	 ('Khảo sát mức độ hài lòng quý 3/2026',    'Đánh giá chất lượng sản phẩm và dịch vụ',       '2026-09-20 00:00:00', '2026-10-31 23:59:59', 'da_gui',  1, '2026-09-19 15:00:00'),  -- 1
	 ('Khảo sát nhanh: tốc độ giao hàng',       'Đánh giá tốc độ giao hàng tháng 9',             '2026-09-01 00:00:00', '2026-09-30 23:59:59', 'da_dong', 6, '2026-08-30 15:00:00'),  -- 2
	 ('Khảo sát nhanh: phương thức thanh toán', 'Tìm hiểu cách khách hàng thanh toán',           '2026-09-05 00:00:00', '2026-10-05 23:59:59', 'da_dong', 2, '2026-09-04 15:00:00'),  -- 3
	 ('Khảo sát nhu cầu mua sắm',               'Nhu cầu mua thêm nhóm sản phẩm',                '2026-09-10 00:00:00', '2026-10-10 23:59:59', 'da_dong', 6, '2026-09-09 15:00:00'),  -- 4
	 ('Góp ý về nhân viên tư vấn',              'Chất lượng tư vấn của nhân viên',               '2026-09-15 00:00:00', '2026-10-15 23:59:59', 'da_gui',  4, '2026-09-14 15:00:00'),  -- 5
	 ('Đánh giá giao diện website/app',          'Mức độ dễ sử dụng của website và ứng dụng',     '2026-09-25 00:00:00', '2026-10-25 23:59:59', 'da_gui',  2, '2026-09-24 15:00:00'),  -- 6
	 ('Mong muốn về khuyến mãi',                'Khách hàng mong muốn ưu đãi nào',               '2026-10-01 00:00:00', '2026-10-31 23:59:59', 'da_gui',  6, '2026-09-30 15:00:00'),  -- 7
	 ('Khảo sát chương trình tích điểm',        'Mức hài lòng với chương trình tích điểm',       '2026-10-05 00:00:00', '2026-11-05 23:59:59', 'da_gui',  1, '2026-10-04 15:00:00'),  -- 8
	 ('Khảo sát môn thể thao yêu thích',        'Môn thể thao khách hàng chơi thường xuyên',     '2026-10-10 00:00:00', '2026-11-10 23:59:59', 'da_gui',  6, '2026-10-09 15:00:00'),  -- 9
	 ('Dịch vụ mong muốn bổ sung',              'Dịch vụ khách hàng muốn cửa hàng có thêm',      '2026-11-01 00:00:00', '2026-11-30 23:59:59', 'nhap',    2, '2026-10-12 15:00:00');  -- 10

-- 32. Câu hỏi khảo sát
INSERT INTO CAU_HOI_KHAO_SAT (ma_khao_sat, noi_dung, loai_cau_hoi, bat_buoc, thu_tu) VALUES
	 (1,  'Anh/chị đánh giá chất lượng sản phẩm như thế nào?',        'thang_diem',     1, 1),  -- 1
	 (1,  'Anh/chị biết đến cửa hàng qua kênh nào?',                   'mot_lua_chon',   1, 2),  -- 2
	 (1,  'Anh/chị muốn cửa hàng bổ sung thêm môn thể thao nào?',      'nhieu_lua_chon', 0, 3),  -- 3
	 (1,  'Góp ý khác cho cửa hàng',                                    'van_ban',        0, 4),  -- 4
	 (2,  'Tốc độ giao hàng của cửa hàng như thế nào?',                'mot_lua_chon',   1, 1),  -- 5
	 (3,  'Anh/chị thường thanh toán bằng phương thức nào?',           'mot_lua_chon',   1, 1),  -- 6
	 (4,  'Anh/chị muốn mua thêm nhóm sản phẩm nào?',                  'nhieu_lua_chon', 1, 1),  -- 7
	 (5,  'Anh/chị có góp ý gì cho nhân viên tư vấn?',                 'van_ban',        0, 1),  -- 8
	 (6,  'Anh/chị thấy website/app của cửa hàng dễ sử dụng không?',   'mot_lua_chon',   1, 1),  -- 9
	 (7,  'Anh/chị mong muốn chương trình khuyến mãi nào?',            'van_ban',        0, 1),  -- 10
	 (8,  'Anh/chị có hài lòng với chương trình tích điểm không?',     'mot_lua_chon',   1, 1),  -- 11
	 (9,  'Anh/chị chơi môn thể thao nào thường xuyên?',               'nhieu_lua_chon', 1, 1),  -- 12
	 (10, 'Anh/chị mong cửa hàng bổ sung dịch vụ nào?',                'van_ban',        0, 1);  -- 13

-- 33. Lựa chọn của câu hỏi
INSERT INTO LUA_CHON_CAU_HOI (ma_cau_hoi, noi_dung) VALUES
	 (1, '1'), (1, '2'), (1, '3'), (1, '4'), (1, '5'),                          -- 1..5
	 (2, 'Facebook'), (2, 'Bạn bè giới thiệu'), (2, 'Đi ngang cửa hàng'),       -- 6..8
	 (3, 'Tennis'), (3, 'Bơi lội'), (3, 'Pickleball'),                          -- 9..11
	 (5, 'Nhanh'), (5, 'Bình thường'), (5, 'Chậm'),                             -- 12..14
	 (6, 'Tiền mặt'), (6, 'Chuyển khoản'), (6, 'Ví điện tử'),                   -- 15..17
	 (7, 'Giày'), (7, 'Vợt'), (7, 'Quần áo'), (7, 'Phụ kiện'),                  -- 18..21
	 (9, 'Dễ sử dụng'), (9, 'Bình thường'), (9, 'Khó sử dụng'),                 -- 22..24
	 (11, 'Hài lòng'), (11, 'Bình thường'), (11, 'Chưa hài lòng'),              -- 25..27
	 (12, 'Cầu lông'), (12, 'Chạy bộ'), (12, 'Bóng đá'), (12, 'Gym');           -- 28..31

-- 34. Khảo sát gửi cho khách hàng
INSERT INTO KHAO_SAT_KHACH_HANG (ma_khao_sat, ma_khach_hang, ngay_gui, trang_thai, ngay_hoan_thanh) VALUES
	 (1, 1,  '2026-09-20 09:00:00', 'hoan_thanh', '2026-09-21 20:00:00'),
	 (1, 2,  '2026-09-20 09:00:00', 'chua_lam',   NULL),
	 (1, 3,  '2026-09-20 09:00:00', 'hoan_thanh', '2026-09-22 07:30:00'),
	 (2, 1,  '2026-09-01 08:00:00', 'hoan_thanh', '2026-09-02 10:00:00'),
	 (2, 9,  '2026-09-01 08:00:00', 'hoan_thanh', '2026-09-30 21:00:00'),
	 (2, 3,  '2026-09-01 08:00:00', 'chua_lam',   NULL),
	 (3, 1,  '2026-09-05 08:00:00', 'hoan_thanh', '2026-09-06 19:00:00'),
	 (3, 2,  '2026-09-05 08:00:00', 'hoan_thanh', '2026-09-16 09:00:00'),
	 (4, 9,  '2026-09-10 08:00:00', 'hoan_thanh', '2026-09-29 20:00:00'),
	 (4, 2,  '2026-09-10 08:00:00', 'hoan_thanh', '2026-09-20 18:00:00'),
	 (5, 10, '2026-09-15 08:00:00', 'hoan_thanh', '2026-10-10 10:00:00'),
	 (5, 3,  '2026-09-15 08:00:00', 'hoan_thanh', '2026-09-25 09:00:00'),
	 (5, 5,  '2026-09-15 08:00:00', 'chua_lam',   NULL),
	 (6, 6,  '2026-09-25 08:00:00', 'hoan_thanh', '2026-10-04 20:00:00'),
	 (6, 7,  '2026-09-25 08:00:00', 'hoan_thanh', '2026-10-06 21:00:00'),
	 (7, 1,  '2026-10-01 08:00:00', 'hoan_thanh', '2026-10-02 20:00:00'),
	 (7, 6,  '2026-10-01 08:00:00', 'dang_lam',   NULL),
	 (8, 7,  '2026-10-05 08:00:00', 'hoan_thanh', '2026-10-06 21:30:00'),
	 (8, 1,  '2026-10-05 08:00:00', 'dang_lam',   NULL),
	 (9, 8,  '2026-10-10 08:00:00', 'hoan_thanh', '2026-10-11 08:00:00'),
	 (9, 4,  '2026-10-10 08:00:00', 'chua_lam',   NULL);

-- 35. Trả lời khảo sát
INSERT INTO TRA_LOI_KHAO_SAT (ma_khao_sat, ma_khach_hang, ma_cau_hoi, ma_lua_chon, noi_dung_tra_loi, ngay_tra_loi) VALUES
	 (1, 1,  1,  5,    NULL,                                                       '2026-09-21 20:00:00'),
	 (1, 1,  2,  7,    NULL,                                                       '2026-09-21 20:00:00'),
	 (1, 1,  3,  11,   NULL,                                                       '2026-09-21 20:00:00'),
	 (1, 1,  4,  NULL, 'Nên có thêm dịch vụ căng cước vợt.',                       '2026-09-21 20:00:00'),
	 (1, 3,  1,  4,    NULL,                                                       '2026-09-22 07:30:00'),
	 (1, 3,  2,  6,    NULL,                                                       '2026-09-22 07:30:00'),
	 (1, 3,  3,  9,    NULL,                                                       '2026-09-22 07:30:00'),
	 (2, 1,  5,  12,   NULL,                                                       '2026-09-02 10:00:00'),
	 (2, 9,  5,  12,   NULL,                                                       '2026-09-30 21:00:00'),
	 (3, 1,  6,  16,   NULL,                                                       '2026-09-06 19:00:00'),
	 (3, 2,  6,  15,   NULL,                                                       '2026-09-16 09:00:00'),
	 (4, 9,  7,  18,   NULL,                                                       '2026-09-29 20:00:00'),
	 (4, 9,  7,  21,   NULL,                                                       '2026-09-29 20:00:00'),
	 (4, 2,  7,  20,   NULL,                                                       '2026-09-20 18:00:00'),
	 (5, 10, 8,  NULL, 'Nhân viên tư vấn nhiệt tình, hiểu rõ về vợt cầu lông.',    '2026-10-10 10:00:00'),
	 (5, 3,  8,  NULL, 'Nên có thêm nhân viên tư vấn vào cuối tuần.',              '2026-09-25 09:00:00'),
	 (6, 6,  9,  22,   NULL,                                                       '2026-10-04 20:00:00'),
	 (6, 7,  9,  23,   NULL,                                                       '2026-10-06 21:00:00'),
	 (7, 1,  10, NULL, 'Mong có khuyến mãi mua vợt tặng quấn cán.',                '2026-10-02 20:00:00'),
	 (8, 7,  11, 25,   NULL,                                                       '2026-10-06 21:30:00'),
	 (9, 8,  12, 30,   NULL,                                                       '2026-10-11 08:00:00');