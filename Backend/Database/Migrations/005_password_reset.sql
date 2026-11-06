-- Thêm bảng mã OTP quên mật khẩu (mỗi tài khoản một mã, chỉ lưu hash); chạy lại được, giữ nguyên dữ liệu cũ.
CREATE TABLE IF NOT EXISTS MOBILE_PASSWORD_RESET (
    account_id INT NOT NULL PRIMARY KEY,
    code_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    expires_utc DATETIME NOT NULL,
    sent_utc DATETIME NOT NULL,
    attempts INT NOT NULL DEFAULT 0,
    CONSTRAINT FK_PASSWORD_RESET_ACCOUNT FOREIGN KEY (account_id) REFERENCES TAI_KHOAN(ma_tai_khoan) ON DELETE CASCADE
);
