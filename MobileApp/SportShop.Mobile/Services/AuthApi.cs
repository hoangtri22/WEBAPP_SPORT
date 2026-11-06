using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SportShop.Contracts.Customer;

namespace SportShop.Mobile.Services;

public interface IAuthApi
{
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string accessToken, CancellationToken cancellationToken);
    Task RegisterGuestAsync(GuestDeviceRequest device, CancellationToken cancellationToken);
}

public interface ISessionStore
{
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task<DateTimeOffset?> GetAccessExpiryAsync();
    Task SaveAsync(TokenResponse tokens);
    void Clear();
}

public interface IGoogleSignIn
{
    Task<TokenResponse?> SignInAsync(CancellationToken cancellationToken);
}

public interface IDeviceIdentity
{
    GuestDeviceRequest Current();
}

public sealed class AuthApi(HttpClient http) : IAuthApi
{
    public Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        => PostTokenAsync("api/v1/auth/login", request, cancellationToken);

    public Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
        => PostTokenAsync("api/v1/auth/register", request, cancellationToken);

    public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        => PostTokenAsync("api/v1/auth/refresh", new RefreshRequest(refreshToken), cancellationToken);

    // Thu hồi phiên hiện tại trên API (AuthController.Logout), gửi kèm access token.
    public Task LogoutAsync(string accessToken, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return SendAsync(request, cancellationToken);
    }

    // Gửi tên thiết bị lên API (GuestDevicesController.Register) để Web Admin hiển thị.
    public Task RegisterGuestAsync(GuestDeviceRequest device, CancellationToken cancellationToken)
        => SendAsync(new HttpRequestMessage(HttpMethod.Post, "api/v1/guest-devices") { Content = JsonContent.Create(device) }, cancellationToken);

    // Gọi API đăng nhập / đăng ký / làm mới phiên (AuthController) rồi đọc TokenResponse; không kết nối MySQL trong app.
    private async Task<TokenResponse> PostTokenAsync<T>(string path, T body, CancellationToken cancellationToken)
    {
        var result = await SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) }, cancellationToken);
        return result ?? throw new AuthException("Máy chủ trả dữ liệu chưa hợp lệ. Vui lòng thử lại.");
    }

    // Gửi yêu cầu và đổi lỗi kết nối / ProblemDetails của API thành AuthException có câu thông báo tiếng Việt.
    private async Task<TokenResponse?> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using (request)
            using (var response = await http.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode) throw await FailureAsync(response, cancellationToken);
                return response.StatusCode == HttpStatusCode.NoContent ? null
                    : await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            }
        }
        catch (HttpRequestException)
        {
            throw new AuthException("Không kết nối được cửa hàng. Vui lòng kiểm tra kết nối và thử lại.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AuthException("Kết nối mất quá nhiều thời gian. Vui lòng thử lại.");
        }
        catch (JsonException)
        {
            throw new AuthException("Máy chủ trả dữ liệu chưa hợp lệ. Vui lòng thử lại.");
        }
    }

    // Đọc ProblemDetails của API thành AuthException; lỗi 401 đánh dấu Rejected.
    internal static async Task<AuthException> FailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        bool rejected = response.StatusCode == HttpStatusCode.Unauthorized;
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            return new AuthException("Máy chủ chưa sẵn sàng. Vui lòng thử lại sau.");
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<Problem>(cancellationToken);
            if (!string.IsNullOrWhiteSpace(problem?.Title)) return new AuthException(problem.Title, rejected);
        }
        catch (JsonException) { }
        return new AuthException("Máy chủ chưa xử lý được yêu cầu. Vui lòng thử lại.", rejected);
    }

    private sealed record Problem(string? Title);
}

public sealed class AuthException(string message, bool rejected = false) : Exception(message)
{
    public bool Rejected => rejected;
}
