using System.Net.Http.Json;
using SportShop.Contracts.Customer;

namespace SportShop.Mobile.Services;

public interface IPasswordResetApi
{
    Task SendCodeAsync(string email, CancellationToken cancellationToken);
    Task ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
}

// Quên mật khẩu không cần đăng nhập nên dùng HttpClient thường, không qua AuthorizedHandler.
public sealed class PasswordResetApi(HttpClient http) : IPasswordResetApi
{
    public Task SendCodeAsync(string email, CancellationToken cancellationToken)
        => PostAsync("api/v1/auth/password/forgot", new ForgotPasswordRequest(email), cancellationToken);

    public Task ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
        => PostAsync("api/v1/auth/password/reset", request, cancellationToken);

    private async Task PostAsync<T>(string path, T body, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(path, body, cancellationToken);
            if (!response.IsSuccessStatusCode) throw await AuthApi.FailureAsync(response, cancellationToken);
        }
        catch (HttpRequestException) { throw ConnectionError(); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw ConnectionError(); }
    }

    private static AuthException ConnectionError() => new("Không kết nối được cửa hàng. Vui lòng kiểm tra kết nối và thử lại.");
}
