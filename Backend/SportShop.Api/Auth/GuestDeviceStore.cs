using Dapper;
using SportShop.Contracts.Customer;
using SportShop.Infrastructure;

namespace SportShop.Api.Auth;

// Ghi thiết bị khách vào MOBILE_GUEST_DEVICE: lần đầu thêm mới, lần sau theo device_key thì cập nhật tên, thời điểm và tăng số lần vào. Gọi từ GuestDevicesController.Register; Web Admin đọc qua ShopData.GuestDevices().
public class GuestDeviceStore(MySqlConnectionFactory connections)
{
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
}
