using SportShop.Contracts.Customer;

namespace SportShop.Mobile.Services;

// Lấy tên, dòng máy, hệ điều hành từ DeviceInfo của MAUI; DeviceKey sinh một lần và lưu trong Preferences để API nhận ra thiết bị khi khách vào lại.
public sealed class DeviceIdentity : IDeviceIdentity
{
    private const string KeyName = "guest_device_key";

    public GuestDeviceRequest Current()
    {
        var key = Preferences.Default.Get(KeyName, "");
        if (key.Length == 0)
        {
            key = Guid.NewGuid().ToString("N");
            Preferences.Default.Set(KeyName, key);
        }
        var device = DeviceInfo.Current;
        var model = Limit($"{device.Manufacturer} {device.Model}".Trim(), 100);
        var name = string.IsNullOrWhiteSpace(device.Name) ? model : Limit(device.Name.Trim(), 100);
        return new GuestDeviceRequest(key, name, model, Limit(device.Platform.ToString(), 30), Limit(device.VersionString, 30));
    }

    private static string Limit(string value, int length) => value.Length <= length ? value : value[..length];
}
