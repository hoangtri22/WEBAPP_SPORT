using System.Globalization;
using SportShop.Contracts.Customer;

namespace SportShop.Mobile.Services;

// Giữ access / refresh token và hạn access token trong SecureStorage của máy; SessionManager đọc lại khi mở app để khôi phục phiên.
public sealed class SessionStore : ISessionStore
{
    private const string AccessKey = "access_token";
    private const string RefreshKey = "refresh_token";
    private const string ExpiryKey = "access_token_expires_at";

    public Task<string?> GetAccessTokenAsync() => ReadAsync(AccessKey);

    public Task<string?> GetRefreshTokenAsync() => ReadAsync(RefreshKey);

    public async Task<DateTimeOffset?> GetAccessExpiryAsync()
        => DateTimeOffset.TryParse(await ReadAsync(ExpiryKey), CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiry) ? expiry : null;

    // Ghi refresh token trước: nếu app dừng giữa chừng, access token cũ chỉ làm request bị 401 rồi tự làm mới.
    public async Task SaveAsync(TokenResponse tokens)
    {
        try
        {
            await SecureStorage.Default.SetAsync(RefreshKey, tokens.RefreshToken);
            await SecureStorage.Default.SetAsync(AccessKey, tokens.AccessToken);
            await SecureStorage.Default.SetAsync(ExpiryKey, tokens.AccessTokenExpiresAt.ToString("O", CultureInfo.InvariantCulture));
        }
        catch (Exception)
        {
            throw new AuthException("Không lưu được phiên đăng nhập trên máy. Vui lòng thử lại.");
        }
    }

    public void Clear()
    {
        SecureStorage.Default.Remove(AccessKey);
        SecureStorage.Default.Remove(RefreshKey);
        SecureStorage.Default.Remove(ExpiryKey);
    }

    // SecureStorage có thể lỗi khi khóa của máy đã đổi; coi như chưa có phiên.
    private static async Task<string?> ReadAsync(string key)
    {
        try { return await SecureStorage.Default.GetAsync(key); }
        catch (Exception) { return null; }
    }
}
