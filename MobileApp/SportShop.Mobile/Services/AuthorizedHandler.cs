using System.Net;
using System.Net.Http.Headers;

namespace SportShop.Mobile.Services;

// Gắn access token vào request cần đăng nhập; API trả 401 thì làm mới token và gửi lại đúng một lần.
public sealed class AuthorizedHandler(ISessionManager session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await GetTokenAsync(() => session.GetAccessTokenAsync(cancellationToken));
        Authorize(request, accessToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || accessToken is null) return response;

        var renewed = await GetTokenAsync(() => session.RenewAsync(accessToken, cancellationToken));
        if (renewed is null) return response;
        response.Dispose();
        Authorize(request, renewed);
        return await base.SendAsync(request, cancellationToken);
    }

    private static void Authorize(HttpRequestMessage request, string? accessToken)
        => request.Headers.Authorization = accessToken is null ? null : new AuthenticationHeaderValue("Bearer", accessToken);

    // Không làm mới được vì mạng: báo như lỗi kết nối thường, phiên vẫn được giữ.
    private static async Task<string?> GetTokenAsync(Func<Task<string?>> read)
    {
        try { return await read(); }
        catch (AuthException ex) { throw new HttpRequestException(ex.Message, ex); }
    }
}
