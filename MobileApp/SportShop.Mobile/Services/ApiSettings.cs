namespace SportShop.Mobile.Services;

public static class ApiSettings
{
    // Android thật qua USB: adb reverse tcp:5080 tcp:5080 cho JSON và ảnh localhost.
    // Trước khi triển khai Release, đổi sang URL HTTPS công khai của API.
    public static Uri BaseUri { get; } = new("http://localhost:5080/");
}
