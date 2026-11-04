using SportShop.Contracts.Customer;

namespace SportShop.Mobile.Services;

// Giữ access / refresh token trong SecureStorage của máy; AuthViewModel đọc lại khi mở app để khôi phục phiên.
public sealed class SessionStore : ISessionStore
{
    private const string AccessKey = "access_token";
    private const string RefreshKey = "refresh_token";

    public bool IsSignedIn { get; private set; }

    public Task<string?> GetAccessTokenAsync() => ReadAsync(AccessKey);

    public Task<string?> GetRefreshTokenAsync() => ReadAsync(RefreshKey);

    public async Task SaveAsync(TokenResponse tokens)
    {
        await SecureStorage.Default.SetAsync(AccessKey, tokens.AccessToken);
        await SecureStorage.Default.SetAsync(RefreshKey, tokens.RefreshToken);
        IsSignedIn = true;
    }

    public void Clear()
    {
        SecureStorage.Default.Remove(AccessKey);
        SecureStorage.Default.Remove(RefreshKey);
        IsSignedIn = false;
    }

    // SecureStorage có thể lỗi khi khóa của máy đã đổi; coi như chưa có phiên.
    private static async Task<string?> ReadAsync(string key)
    {
        try { return await SecureStorage.Default.GetAsync(key); }
        catch (Exception) { return null; }
    }
}
