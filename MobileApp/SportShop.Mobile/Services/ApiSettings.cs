namespace SportShop.Mobile.Services;

// Địa chỉ API của app.
public static class ApiSettings
{
    // Android qua USB: adb reverse tcp:5080 tcp:5080; trước khi phát hành đổi sang URL HTTPS công khai của API.
    public static Uri BaseUri { get; } = new("http://localhost:5080/");
}
