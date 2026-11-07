using Dapper;
using SportShop.Contracts.Customer;
using SportShop.Infrastructure;

namespace SportShop.Api.Auth;

// Ghi thiết bị khách vào MOBILE_GUEST_DEVICE: lần đầu thêm mới, lần sau theo device_key thì cập nhật tên, thời điểm và tăng số lần vào. Gọi từ GuestDevicesController.Register; Web Admin đọc qua ShopData.GuestDevices().
public class GuestDeviceStore(MySqlConnectionFactory connections)
{
    // Thêm hoặc cập nhật thiết bị khách trong MOBILE_GUEST_DEVICE theo device_key. Gọi từ GuestDevicesController.
    public virtual async Task SaveAsync(GuestDeviceRequest device, CancellationToken cancellationToken)
    {
        await using var c = await connections.OpenAsync(cancellationToken);
        await c.ExecuteAsync(new CommandDefinition("""
            INSERT INTO MOBILE_GUEST_DEVICE(device_key,device_name,model,platform,os_version,first_seen,last_seen)
            VALUES(@DeviceKey,@DeviceName,@Model,@Platform,@OsVersion,NOW(),NOW())
            ON DUPLICATE KEY UPDATE device_name=@DeviceName,model=@Model,platform=@Platform,os_version=@OsVersion,
                last_seen=NOW(),visit_count=visit_count+1
            """, device, cancellationToken: cancellationToken));
    }

    // Ánh xạ device_key của khách vãng lai sang một ma_khach_hang (ma_tai_khoan = NULL) trong KHACH_HANG để dùng chung luồng Giỏ hàng - Thanh toán - Đơn hàng.
    public virtual async Task<int?> ResolveOrCreateGuestCustomerAsync(string deviceKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceKey) || deviceKey.Length is < 8 or > 64 ||
            !deviceKey.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_'))
            return null;

        await using var c = await connections.OpenAsync(cancellationToken);
        await c.ExecuteAsync(new CommandDefinition("""
            CREATE TABLE IF NOT EXISTS MOBILE_GUEST_CUSTOMER (
                device_key VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
                customer_id INT NOT NULL,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT FK_MOBILE_GUEST_CUSTOMER_KH FOREIGN KEY (customer_id) REFERENCES KHACH_HANG(ma_khach_hang)
            ) ENGINE=InnoDB;
            """, cancellationToken: cancellationToken));

        var existingId = await c.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT customer_id FROM MOBILE_GUEST_CUSTOMER WHERE device_key = @deviceKey",
            new { deviceKey }, cancellationToken: cancellationToken));
        if (existingId is int id && id > 0)
            return id;

        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        var lockedId = await c.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT customer_id FROM MOBILE_GUEST_CUSTOMER WHERE device_key = @deviceKey FOR UPDATE",
            new { deviceKey }, tx, cancellationToken: cancellationToken));
        if (lockedId is int foundId && foundId > 0)
        {
            await tx.CommitAsync(cancellationToken);
            return foundId;
        }

        var newCustomerId = await c.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO KHACH_HANG (ma_tai_khoan, ho_lot, ten, so_dien_thoai)
            VALUES (NULL, 'Khách', 'Vãng lai', NULL);
            SELECT LAST_INSERT_ID();
            """, transaction: tx, cancellationToken: cancellationToken));

        await c.ExecuteAsync(new CommandDefinition("""
            INSERT INTO MOBILE_GUEST_CUSTOMER (device_key, customer_id)
            VALUES (@deviceKey, @newCustomerId)
            """, new { deviceKey, newCustomerId }, tx, cancellationToken: cancellationToken));

        await tx.CommitAsync(cancellationToken);
        return newCustomerId;
    }

    // Cập nhật họ tên và số điện thoại người nhận gần nhất cho hồ sơ khách vãng lai khi đặt đơn.
    public virtual async Task UpdateGuestContactAsync(int customerId, string fullName, string phone, CancellationToken cancellationToken)
    {
        var trimmed = (fullName ?? "").Trim();
        var lastSpace = trimmed.LastIndexOf(' ');
        var hoLot = lastSpace > 0 ? trimmed[..lastSpace].Trim() : "Khách vãng lai";
        var ten = lastSpace > 0 ? trimmed[(lastSpace + 1)..].Trim() : (trimmed.Length > 0 ? trimmed : "Khách");
        if (hoLot.Length > 70) hoLot = hoLot[..70];
        if (ten.Length > 30) ten = ten[..30];

        await using var c = await connections.OpenAsync(cancellationToken);
        await c.ExecuteAsync(new CommandDefinition("""
            UPDATE KHACH_HANG
            SET ho_lot = @hoLot, ten = @ten, so_dien_thoai = @phone
            WHERE ma_khach_hang = @customerId AND ma_tai_khoan IS NULL
            """, new { customerId, hoLot, ten, phone = (phone ?? "").Trim() }, cancellationToken: cancellationToken));
    }
}
