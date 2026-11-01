-- Additive, repeatable migration. Run against the existing sports_shop schema; preserves all existing data.
CREATE TABLE IF NOT EXISTS MOBILE_AUTH_SESSION (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    customer_id INT NOT NULL,
    access_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL UNIQUE,
    refresh_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL UNIQUE,
    password_fingerprint CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    access_expires_utc DATETIME(6) NOT NULL,
    refresh_expires_utc DATETIME(6) NOT NULL,
    revoked TINYINT(1) NOT NULL DEFAULT 0,
    created_utc DATETIME(6) NOT NULL,
    CONSTRAINT FK_MOBILE_AUTH_CUSTOMER FOREIGN KEY (customer_id) REFERENCES KHACH_HANG(ma_khach_hang),
    INDEX IX_MOBILE_AUTH_CUSTOMER (customer_id)
);
CREATE TABLE IF NOT EXISTS MOBILE_AUTH_REFRESH_USED (
    refresh_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
    session_id BIGINT NOT NULL,
    CONSTRAINT FK_MOBILE_REFRESH_SESSION FOREIGN KEY (session_id) REFERENCES MOBILE_AUTH_SESSION(id) ON DELETE CASCADE
);
