using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using SportShop.Contracts.Customer;

namespace SportShop.Mobile.Services;

// Mở trang Google qua API rồi đổi vé một lần lấy token; secret của Google chỉ nằm trên máy chủ.
public sealed class GoogleSignIn(HttpClient http) : IGoogleSignIn
{
    // Trả null khi khách đóng trình duyệt hoặc bấm hủy.
    public async Task<TokenResponse?> SignInAsync(CancellationToken cancellationToken)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        WebAuthenticatorResult result;
        try
        {
            result = await WebAuthenticator.Default.AuthenticateAsync(
                new Uri(ApiSettings.BaseUri, $"api/v1/auth/google/start?challenge={challenge}"), new Uri("sportshop://auth"));
        }
        catch (TaskCanceledException) { return null; }

        if (result.Properties.TryGetValue("error", out var error))
            return error == "cancelled" ? null : throw new AuthException(ErrorText(error));
        if (!result.Properties.TryGetValue("ticket", out var ticket))
            throw new AuthException("Đăng nhập Google chưa thành công. Vui lòng thử lại.");

        try
        {
            using var response = await http.PostAsJsonAsync("api/v1/auth/google/exchange", new GoogleExchangeRequest(ticket, verifier), cancellationToken);
            if (!response.IsSuccessStatusCode) throw await AuthApi.FailureAsync(response, cancellationToken);
            return await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new AuthException("Máy chủ trả dữ liệu chưa hợp lệ. Vui lòng thử lại.");
        }
        catch (HttpRequestException) { throw new AuthException("Không kết nối được cửa hàng. Vui lòng kiểm tra kết nối và thử lại."); }
    }

    // Đổi mã lỗi đăng nhập Google sang thông báo tiếng Việt.
    private static string ErrorText(string error) => error switch
    {
        "not_configured" => "Đăng nhập Google chưa được cấu hình trên máy chủ.",
        "rejected" => "Tài khoản Google này không đăng nhập được vào cửa hàng.",
        _ => "Không kết nối được Google. Vui lòng thử lại."
    };

    // Mã hóa base64url cho PKCE.
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
