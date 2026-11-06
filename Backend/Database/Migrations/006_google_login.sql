-- Liên kết tài khoản Google với TAI_KHOAN để đăng nhập bằng Gmail; chạy lại được, giữ nguyên dữ liệu cũ.
CREATE TABLE IF NOT EXISTS MOBILE_GOOGLE_LOGIN (
    google_sub VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    account_id INT NOT NULL UNIQUE,
    created_utc DATETIME NOT NULL,
    CONSTRAINT FK_GOOGLE_LOGIN_ACCOUNT FOREIGN KEY (account_id) REFERENCES TAI_KHOAN(ma_tai_khoan) ON DELETE CASCADE
);
