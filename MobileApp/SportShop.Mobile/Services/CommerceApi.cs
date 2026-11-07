using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SportShop.Contracts.Commerce;
using SportShop.Contracts.Common;

namespace SportShop.Mobile.Services;

// Các API giỏ hàng, thanh toán và đơn hàng.
public interface ICommerceApi
{
    Task<CartDto> GetCartAsync(CancellationToken cancellationToken = default);
    Task<CartDto> AddCartItemAsync(int variantId, int quantity, CancellationToken cancellationToken = default);
    Task<CartDto> SetCartItemQuantityAsync(int variantId, int quantity, CancellationToken cancellationToken = default);
    Task<CartDto> RemoveCartItemAsync(int variantId, CancellationToken cancellationToken = default);
    Task ClearCartAsync(CancellationToken cancellationToken = default);

    Task<CheckoutDto> GetCheckoutAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EligibleVoucherDto>> GetEligibleVouchersAsync(CancellationToken cancellationToken = default);
    Task<CheckoutDto> QuoteCheckoutAsync(string? voucherCode, CancellationToken cancellationToken = default);

    Task<PagedResponse<OrderDto>> GetOrdersAsync(int page = 1, int pageSize = 20, string? status = null, CancellationToken cancellationToken = default);
    Task<OrderDto> GetOrderDetailAsync(int orderId, CancellationToken cancellationToken = default);
    Task<OrderDto> PlaceOrderAsync(PlaceOrderRequest request, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<OrderDto> CancelOrderAsync(int orderId, CancellationToken cancellationToken = default);
}

// Cài đặt ICommerceApi bằng HttpClient, kèm token hoặc mã thiết bị khách.
public sealed class CommerceApi(
    HttpClient http,
    ISessionManager session,
    AppMemoryCache? cache = null,
    IDeviceIdentity? deviceIdentity = null) : ICommerceApi
{
    public const string GuestDeviceHeader = "X-Guest-Device-Key";

    // Lấy giỏ hàng.
    public Task<CartDto> GetCartAsync(CancellationToken cancellationToken = default) =>
        SendAuthenticatedAsync<CartDto>(() => new HttpRequestMessage(HttpMethod.Get, "api/v1/cart"), cancellationToken);

    // Thêm biến thể vào giỏ.
    public async Task<CartDto> AddCartItemAsync(int variantId, int quantity, CancellationToken cancellationToken = default)
    {
        var cart = await SendAuthenticatedAsync<CartDto>(() => new HttpRequestMessage(HttpMethod.Post, "api/v1/cart/items")
        {
            Content = JsonContent.Create(new AddCartItemRequest(variantId, quantity))
        }, cancellationToken);
        cache?.InvalidateCommerce();
        return cart;
    }

    // Đổi số lượng một dòng giỏ.
    public async Task<CartDto> SetCartItemQuantityAsync(int variantId, int quantity, CancellationToken cancellationToken = default)
    {
        var cart = await SendAuthenticatedAsync<CartDto>(() => new HttpRequestMessage(HttpMethod.Put, $"api/v1/cart/items/{variantId}")
        {
            Content = JsonContent.Create(new SetCartQuantityRequest(quantity))
        }, cancellationToken);
        cache?.InvalidateCommerce();
        return cart;
    }

    // Xóa một dòng giỏ.
    public async Task<CartDto> RemoveCartItemAsync(int variantId, CancellationToken cancellationToken = default)
    {
        var cart = await SendAuthenticatedAsync<CartDto>(() => new HttpRequestMessage(HttpMethod.Delete, $"api/v1/cart/items/{variantId}"), cancellationToken);
        cache?.InvalidateCommerce();
        return cart;
    }

    // Xóa hết giỏ.
    public async Task ClearCartAsync(CancellationToken cancellationToken = default)
    {
        await SendAuthenticatedNoContentAsync(() => new HttpRequestMessage(HttpMethod.Delete, "api/v1/cart"), cancellationToken);
        cache?.InvalidateCommerce();
    }

    // Lấy dữ liệu trang thanh toán.
    public Task<CheckoutDto> GetCheckoutAsync(CancellationToken cancellationToken = default) =>
        SendAuthenticatedAsync<CheckoutDto>(() => new HttpRequestMessage(HttpMethod.Get, "api/v1/checkout"), cancellationToken);

    // Lấy các voucher dùng được.
    public Task<IReadOnlyList<EligibleVoucherDto>> GetEligibleVouchersAsync(CancellationToken cancellationToken = default) =>
        SendAuthenticatedAsync<IReadOnlyList<EligibleVoucherDto>>(() => new HttpRequestMessage(HttpMethod.Get, "api/v1/checkout/vouchers"), cancellationToken);

    // Tính thử voucher.
    public Task<CheckoutDto> QuoteCheckoutAsync(string? voucherCode, CancellationToken cancellationToken = default) =>
        SendAuthenticatedAsync<CheckoutDto>(() => new HttpRequestMessage(HttpMethod.Post, "api/v1/checkout/quote")
        {
            Content = JsonContent.Create(new CheckoutQuoteRequest(string.IsNullOrWhiteSpace(voucherCode) ? null : voucherCode.Trim()))
        }, cancellationToken);

    // Lấy danh sách đơn hàng có phân trang.
    public Task<PagedResponse<OrderDto>> GetOrdersAsync(
        int page = 1,
        int pageSize = 20,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/orders?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status))
            url += "&status=" + Uri.EscapeDataString(status.Trim());
        return SendAuthenticatedAsync<PagedResponse<OrderDto>>(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
    }

    // Lấy chi tiết đơn hàng.
    public Task<OrderDto> GetOrderDetailAsync(int orderId, CancellationToken cancellationToken = default) =>
        SendAuthenticatedAsync<OrderDto>(() => new HttpRequestMessage(HttpMethod.Get, $"api/v1/orders/{orderId}"), cancellationToken);

    // Đặt đơn hàng kèm Idempotency-Key.
    public async Task<OrderDto> PlaceOrderAsync(
        PlaceOrderRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var order = await SendAuthenticatedAsync<OrderDto>(() =>
        {
            var msg = new HttpRequestMessage(HttpMethod.Post, "api/v1/orders")
            {
                Content = JsonContent.Create(request)
            };
            msg.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            return msg;
        }, cancellationToken);
        cache?.InvalidateCommerce();
        return order;
    }

    // Hủy đơn hàng.
    public async Task<OrderDto> CancelOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await SendAuthenticatedAsync<OrderDto>(() => new HttpRequestMessage(HttpMethod.Post, $"api/v1/orders/{orderId}/cancel"), cancellationToken);
        cache?.InvalidateCommerce();
        return order;
    }

    // Gửi request có xác thực và đọc kết quả; bị từ chối 401 thì làm mới token và gửi lại.
    private async Task<T> SendAuthenticatedAsync<T>(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        var token = await TryGetValidAccessTokenAsync(cancellationToken);
        try
        {
            using var req = requestFactory();
            AttachAuthOrGuestHeader(req, token);
            using var resp = await http.SendAsync(req, cancellationToken);
            if (resp.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrWhiteSpace(token))
            {
                token = await RenewAccessTokenAsync(token, cancellationToken);
                using var retryReq = requestFactory();
                AttachAuthOrGuestHeader(retryReq, token);
                using var retryResp = await http.SendAsync(retryReq, cancellationToken);
                return await ReadResponseAsync<T>(retryResp, cancellationToken);
            }
            return await ReadResponseAsync<T>(resp, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new CommerceException("Không kết nối được máy chủ SportShop. Kiểm tra API và mạng.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CommerceException("Máy chủ phản hồi quá lâu. Vui lòng thử lại.", ex);
        }
    }

    // Gửi request có xác thực, không đọc nội dung trả về.
    private async Task SendAuthenticatedNoContentAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        var token = await TryGetValidAccessTokenAsync(cancellationToken);
        try
        {
            using var req = requestFactory();
            AttachAuthOrGuestHeader(req, token);
            using var resp = await http.SendAsync(req, cancellationToken);
            if (resp.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrWhiteSpace(token))
            {
                token = await RenewAccessTokenAsync(token, cancellationToken);
                using var retryReq = requestFactory();
                AttachAuthOrGuestHeader(retryReq, token);
                using var retryResp = await http.SendAsync(retryReq, cancellationToken);
                if (!retryResp.IsSuccessStatusCode)
                    throw await CreateExceptionAsync(retryResp, cancellationToken);
                return;
            }
            if (!resp.IsSuccessStatusCode)
                throw await CreateExceptionAsync(resp, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new CommerceException("Không kết nối được máy chủ SportShop. Kiểm tra API và mạng.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CommerceException("Máy chủ phản hồi quá lâu. Vui lòng thử lại.", ex);
        }
    }

    // Gắn Bearer token, chưa đăng nhập thì gắn mã thiết bị khách.
    private void AttachAuthOrGuestHeader(HttpRequestMessage req, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            req.Headers.TryAddWithoutValidation(GuestDeviceHeader, GetGuestDeviceKey());
        }
    }

    private static readonly string FallbackGuestKey = Guid.NewGuid().ToString("N");

    // Lấy mã thiết bị khách, không có thì dùng mã dự phòng.
    private string GetGuestDeviceKey()
    {
        if (deviceIdentity is not null)
        {
            var key = deviceIdentity.Current().DeviceKey;
            if (!string.IsNullOrWhiteSpace(key))
                return key;
        }
        return FallbackGuestKey;
    }

    // SessionManager là nơi duy nhất làm mới token; khách (chưa đăng nhập) nhận null và gửi kèm mã thiết bị.
    private async Task<string?> TryGetValidAccessTokenAsync(CancellationToken cancellationToken)
    {
        try { return await session.GetAccessTokenAsync(cancellationToken); }
        catch (AuthException ex) { throw new CommerceException(ex.Message, ex); }
    }

    // Làm mới access token sau khi bị từ chối.
    private async Task<string> RenewAccessTokenAsync(string rejected, CancellationToken cancellationToken)
    {
        try
        {
            return await session.RenewAsync(rejected, cancellationToken)
                ?? throw new CommerceException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }
        catch (AuthException ex) { throw new CommerceException(ex.Message, ex); }
    }

    // Đọc kết quả JSON, lỗi thì đổi thành CommerceException.
    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            throw await CreateExceptionAsync(response, cancellationToken);
        try
        {
            var data = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return data ?? throw new CommerceException("Dữ liệu trả về từ máy chủ không hợp lệ.");
        }
        catch (JsonException ex)
        {
            throw new CommerceException("Dữ liệu trả về từ máy chủ không hợp lệ.", ex);
        }
    }

    // Tạo CommerceException từ phản hồi lỗi.
    private static async Task<CommerceException> CreateExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = await TryReadProblemDetailAsync(response, cancellationToken);
        var msg = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Vui lòng đăng nhập để tiếp tục.",
            HttpStatusCode.Forbidden => "Tài khoản không có quyền thực hiện thao tác này.",
            HttpStatusCode.NotFound => detail ?? "Không tìm thấy dữ liệu yêu cầu.",
            HttpStatusCode.Conflict => detail ?? "Dữ liệu giỏ hàng hoặc tồn kho đã thay đổi. Vui lòng kiểm tra lại.",
            HttpStatusCode.BadRequest => detail ?? "Thông tin gửi lên chưa hợp lệ.",
            HttpStatusCode.ServiceUnavailable => detail ?? "Cơ sở dữ liệu cửa hàng tạm thời chưa sẵn sàng.",
            _ => detail ?? "Không thực hiện được yêu cầu. Vui lòng thử lại."
        };
        return new CommerceException(msg, statusCode: (int)response.StatusCode);
    }

    // Đọc nội dung lỗi ProblemDetails nếu có.
    private static async Task<string?> TryReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in errors.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                        return prop.Value[0].GetString();
                }
            }
            if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString();
            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString();
        }
        catch
        {
            // Bỏ qua nếu body không phải ProblemDetails JSON.
        }
        return null;
    }
}

// Lỗi khi gọi API giỏ hàng, thanh toán, đơn hàng.
public sealed class CommerceException(string message, Exception? innerException = null, int? statusCode = null)
    : Exception(message, innerException)
{
    public int? StatusCode { get; } = statusCode;
}
