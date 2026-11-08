using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;
using SportShop.Contracts.Customer;

namespace SportShop.Mobile.Services;

// Nội dung hỗ trợ trả từ API, không sao chép chính sách vào app.
public sealed record ContentPageSummary(string Slug, string Eyebrow, string Title, string Intro);
public sealed record ContentSection(string Heading, IReadOnlyList<string> Paragraphs);
public sealed record ContentPageDetail(string Eyebrow, string Title, string Intro, IReadOnlyList<ContentSection> Sections);

// API khuyến mãi, nội dung và đánh giá; client xác thực do AuthorizedHandler quản lý token.
public interface IContentApi
{
    Task<IReadOnlyList<PromotionDto>> GetPromotionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentPageSummary>> GetPagesAsync(CancellationToken cancellationToken = default);
    Task<ContentPageDetail> GetPageAsync(string slug, CancellationToken cancellationToken = default);
    Task<ProductReviewsResponse> GetReviewsAsync(int productId, int page, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EligibleProductResponse>> GetEligibleProductsAsync(CancellationToken cancellationToken = default);
    Task<PagedResponse<FeedbackResponse>> GetMyFeedbackAsync(int productId, int page, CancellationToken cancellationToken = default);
    Task SendFeedbackAsync(FeedbackRequest request, CancellationToken cancellationToken = default);
}

public sealed class ContentApi(HttpClient publicHttp, HttpClient authorizedHttp) : IContentApi
{
    public async Task<IReadOnlyList<PromotionDto>> GetPromotionsAsync(CancellationToken cancellationToken = default)
        => Valid(await GetAsync<IReadOnlyList<PromotionDto>>(publicHttp, "api/v1/promotions", cancellationToken),
            items => items.All(p => p is not null && !string.IsNullOrWhiteSpace(p.Code) && p.Name is not null && p.Percent is >= 0 and <= 100));

    public async Task<IReadOnlyList<ContentPageSummary>> GetPagesAsync(CancellationToken cancellationToken = default)
        => Valid(await GetAsync<IReadOnlyList<ContentPageSummary>>(publicHttp, "api/v1/pages", cancellationToken),
            items => items.All(p => p is not null && !string.IsNullOrWhiteSpace(p.Slug) && p.Title is not null && p.Intro is not null));

    public async Task<ContentPageDetail> GetPageAsync(string slug, CancellationToken cancellationToken = default)
        => Valid(await GetAsync<ContentPageDetail>(publicHttp, "api/v1/pages/" + Uri.EscapeDataString(slug), cancellationToken),
            page => page.Title is not null && page.Intro is not null && page.Sections is not null
                && page.Sections.All(s => s is not null && s.Heading is not null && s.Paragraphs is not null && s.Paragraphs.All(p => p is not null)));

    public async Task<ProductReviewsResponse> GetReviewsAsync(int productId, int page, CancellationToken cancellationToken = default)
        => Valid(await GetAsync<ProductReviewsResponse>(publicHttp, $"api/v1/products/{productId}/reviews?page={page}&pageSize=20", cancellationToken),
            result => result.Items is not null && result.Page == page && result.PageSize is >= 1 and <= 100 && result.TotalItems >= 0
                && (result.AverageStars is null || result.AverageStars is >= 1 and <= 5)
                && result.Items.All(r => r is not null && r.Id > 0 && r.Stars is >= 1 and <= 5 && r.Author is not null && r.Content is not null));

    public async Task<IReadOnlyList<EligibleProductResponse>> GetEligibleProductsAsync(CancellationToken cancellationToken = default)
        => Valid(await GetAsync<IReadOnlyList<EligibleProductResponse>>(authorizedHttp, "api/v1/me/feedback/eligible-products", cancellationToken),
            items => items.All(p => p is not null && p.Id > 0 && p.Name is not null));

    public async Task<PagedResponse<FeedbackResponse>> GetMyFeedbackAsync(int productId, int page, CancellationToken cancellationToken = default)
        => Valid(await GetAsync<PagedResponse<FeedbackResponse>>(authorizedHttp, $"api/v1/me/feedback?productId={productId}&page={page}&pageSize=20", cancellationToken),
            result => result.Items is not null && result.Page == page && result.PageSize is >= 1 and <= 100 && result.TotalItems >= 0
                && result.Items.All(r => r is not null && r.Id > 0 && r.ProductId == productId && r.Content is not null && r.Status is not null));

    // Gửi theo contract hiện có; chỉ báo thành công sau khi API chấp nhận.
    public async Task SendFeedbackAsync(FeedbackRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await authorizedHttp.PostAsJsonAsync("api/v1/me/feedback", request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw await FailureAsync(response, cancellationToken);
        }
        catch (HttpRequestException) { throw ConnectionError(); }
        catch (AuthException ex) { throw new ContentException(ex.Message); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw TimeoutError(); }
    }

    // Đổi lỗi mạng/JSON thành thông báo có thể thử lại; không xóa phiên đăng nhập.
    private static async Task<T> GetAsync<T>(HttpClient client, string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode) throw await FailureAsync(response, cancellationToken);
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new ContentException("Dữ liệu cửa hàng chưa hợp lệ. Vui lòng thử lại.");
        }
        catch (JsonException) { throw new ContentException("Dữ liệu cửa hàng chưa hợp lệ. Vui lòng thử lại."); }
        catch (HttpRequestException) { throw ConnectionError(); }
        catch (AuthException ex) { throw new ContentException(ex.Message); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw TimeoutError(); }
    }

    private static async Task<ContentException> FailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new ContentException("Vui lòng đăng nhập để gửi hoặc xem đánh giá của bạn.");
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            return new ContentException("Cửa hàng tạm thời chưa sẵn sàng. Vui lòng thử lại sau.");
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ContentProblem>(cancellationToken);
            var message = problem?.Detail ?? problem?.Title;
            if (!string.IsNullOrWhiteSpace(message)) return new ContentException(message);
        }
        catch (JsonException) { }
        return new ContentException(response.StatusCode == HttpStatusCode.NotFound
            ? "Nội dung hoặc sản phẩm không còn khả dụng." : "Chưa thực hiện được yêu cầu. Vui lòng thử lại.");
    }

    private static ContentException ConnectionError() => new("Không kết nối được cửa hàng. Vui lòng kiểm tra kết nối và thử lại.");
    private static ContentException TimeoutError() => new("Kết nối mất quá nhiều thời gian. Vui lòng thử lại.");
    private static T Valid<T>(T value, Func<T, bool> isValid) => isValid(value)
        ? value : throw new ContentException("Dữ liệu cửa hàng chưa hợp lệ. Vui lòng thử lại.");
    private sealed record ContentProblem(string? Title, string? Detail);
}

public sealed class ContentException(string message) : Exception(message);
