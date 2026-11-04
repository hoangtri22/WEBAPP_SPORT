-- Additive, repeatable migration. Lưu thiết bị dùng Mobile App với tư cách khách để Web Admin hiển thị; preserves all existing data.
CREATE TABLE IF NOT EXISTS MOBILE_GUEST_DEVICE (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    device_key VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL UNIQUE,
    device_name VARCHAR(100) NOT NULL,
    model VARCHAR(100) NOT NULL,
    platform VARCHAR(30) NOT NULL,
    os_version VARCHAR(30) NOT NULL,
    first_seen DATETIME NOT NULL,
    last_seen DATETIME NOT NULL,
    visit_count INT NOT NULL DEFAULT 1,
    INDEX IX_MOBILE_GUEST_LAST_SEEN (last_seen)
);
