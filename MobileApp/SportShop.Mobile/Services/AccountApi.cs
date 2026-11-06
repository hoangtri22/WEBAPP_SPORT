using System.Net.Http.Json;
using System.Text.Json;
using SportShop.Contracts.Customer;

namespace SportShop.Mobile.Services;

public interface IAccountApi
{
    Task<CustomerMeResponse?> GetProfileAsync(CancellationToken cancellationToken);
    Task<CustomerMeResponse> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken);
}

// Dùng HttpClient có AuthorizedHandler: token hết hạn được làm mới tự động.
public sealed class AccountApi(HttpClient http) : IAccountApi
{
    // Trả null khi chưa lấy được hồ sơ (mất mạng, hết phiên); màn hình Tài khoản hiện lỗi và nút thử lại.
    public async Task<CustomerMeResponse?> GetProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync("api/v1/me", cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<CustomerMeResponse>(cancellationToken)
                : null;
        }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
    }

    // MeController.Update trả lại hồ sơ mới; lỗi của API (trùng email...) thành AuthException có câu tiếng Việt.
    public async Task<CustomerMeResponse> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PutAsJsonAsync("api/v1/me", request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw await AuthApi.FailureAsync(response, cancellationToken);
            return await response.Content.ReadFromJsonAsync<CustomerMeResponse>(cancellationToken)
                ?? throw new AuthException("Máy chủ trả dữ liệu chưa hợp lệ. Vui lòng thử lại.");
        }
        catch (JsonException) { throw new AuthException("Máy chủ trả dữ liệu chưa hợp lệ. Vui lòng thử lại."); }
        catch (HttpRequestException) { throw ConnectionError(); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw ConnectionError(); }
    }

    // MeController.Password: thành công thì API thu hồi mọi phiên mobile, kể cả phiên đang dùng.
    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/v1/me/password", request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw await AuthApi.FailureAsync(response, cancellationToken);
        }
        catch (HttpRequestException) { throw ConnectionError(); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw ConnectionError(); }
    }

    private static AuthException ConnectionError() => new("Không kết nối được cửa hàng. Vui lòng kiểm tra kết nối và thử lại.");
}
