using SportShop.Contracts.Customer;

namespace SportShop.Mobile.Services;

public interface ISessionManager
{
    bool IsSignedIn { get; }
    // API từ chối refresh token: phiên đã bị xóa, người dùng cần đăng nhập lại.
    event Action? Expired;
    Task<bool> RestoreAsync(CancellationToken cancellationToken);
    Task StartAsync(TokenResponse tokens);
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
    Task<string?> RenewAsync(string rejectedAccessToken, CancellationToken cancellationToken);
    Task SignOutAsync();
}

// Nơi duy nhất làm mới token: API xoay refresh token và coi token dùng lại là bị đánh cắp (thu hồi cả phiên), nên mỗi lúc chỉ được một refresh chạy.
public sealed class SessionManager(IAuthApi api, ISessionStore store) : ISessionManager
{
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _isSignedIn;

    public bool IsSignedIn => _isSignedIn;
    public event Action? Expired;

    // Mở app: còn refresh token thì coi như đã đăng nhập. Mất mạng vẫn giữ phiên; chỉ API từ chối mới xóa.
    public async Task<bool> RestoreAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(await store.GetRefreshTokenAsync())) return false;
        _isSignedIn = true;
        try { await GetAccessTokenAsync(cancellationToken); }
        catch (AuthException) { }
        return _isSignedIn;
    }

    public async Task StartAsync(TokenResponse tokens)
    {
        await _gate.WaitAsync();
        try
        {
            await store.SaveAsync(tokens);
            _isSignedIn = true;
        }
        finally { _gate.Release(); }
    }

    // Token sắp hết hạn thì làm mới trước khi gửi request; trả null nếu không còn phiên.
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) => ReadOrRefreshAsync(null, cancellationToken);

    // API trả 401 với token này: request khác đã làm mới xong thì dùng luôn token mới, không refresh lần nữa.
    public Task<string?> RenewAsync(string rejectedAccessToken, CancellationToken cancellationToken)
        => ReadOrRefreshAsync(rejectedAccessToken, cancellationToken);

    // Thu hồi phiên trên API trước (lỗi mạng vẫn xóa token trong máy).
    public async Task SignOutAsync()
    {
        try
        {
            var accessToken = await GetAccessTokenAsync(default);
            if (!string.IsNullOrEmpty(accessToken)) await api.LogoutAsync(accessToken, default);
        }
        catch (AuthException) { }
        await _gate.WaitAsync();
        try { ClearSession(); }
        finally { _gate.Release(); }
    }

    private async Task<string?> ReadOrRefreshAsync(string? rejected, CancellationToken cancellationToken)
    {
        if (!_isSignedIn) return null;
        var expired = false;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_isSignedIn) return null;
            var accessToken = await store.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(accessToken) && accessToken != rejected
                && await store.GetAccessExpiryAsync() > DateTimeOffset.UtcNow + ExpiryMargin)
                return accessToken;
            var refreshToken = await store.GetRefreshTokenAsync();
            if (!string.IsNullOrEmpty(refreshToken))
                try
                {
                    // Không truyền cancellationToken: hủy giữa chừng có thể làm mất refresh token mới mà API đã xoay.
                    var tokens = await api.RefreshAsync(refreshToken, CancellationToken.None);
                    await store.SaveAsync(tokens);
                    return tokens.AccessToken;
                }
                catch (AuthException ex) when (ex.Rejected) { }
            ClearSession();
            expired = true;
            return null;
        }
        finally
        {
            _gate.Release();
            if (expired) Expired?.Invoke();
        }
    }

    private void ClearSession()
    {
        store.Clear();
        _isSignedIn = false;
    }
}
