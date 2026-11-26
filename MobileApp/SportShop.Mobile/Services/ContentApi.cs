using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;
using SportShop.Contracts.Customer;

namespace SportShop.Mobile.Services;

// Nội dung hỗ trợ trả từ API, không sao chép chính sách vào app.
public sealed record ContentPageSummary(string Slug, string Eyebrow, string Title, string Intro);
// Một mục của trang thông tin.
public sealed record ContentSection(string Heading, IReadOnlyList<string> Paragraphs);
// Nội dung đầy đủ của trang thông tin.
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
    Task<PagedResponse<FeedbackResponse>> GetFeedbackHistoryAsync(int page, CancellationToken cancellationToken = default)
        => Task.FromResult(new PagedResponse<FeedbackResponse>([], page, 20, 0));
    Task<IReadOnlyList<SurveyResponse>> GetSurveysAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SurveyResponse>>([]);
    Task<SurveyDetailResponse> GetSurveyAsync(int surveyId, CancellationToken cancellationToken = default)
        => Task.FromException<SurveyDetailResponse>(new ContentException("Không tìm thấy khảo sát."));
    Task SubmitSurveyAsync(int surveyId, SubmitSurveyRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

// Cài đặt IContentApi: nội dung công khai dùng publicHttp, dữ liệu cá nhân dùng authorizedHttp.
public sealed class ContentApi(HttpClient publicHttp, HttpClient authorizedHttp) : IContentApi
{
    // Lấy voucher đang hiệu lực (api/v1/promotions), dữ liệu sai thì báo lỗi.
    public async Task<IReadOnlyList<PromotionDto>> GetPromotionsAsync(CancellationToken cancellationToken = default)
        => Valid(await GetAsync<IReadOnlyList<PromotionDto>>(publicHttp, "api/v1/promotions", cancellationToken),
            items => items.All(p => p is not null && !string.IsNullOrWhiteSpace(p.Code) && p.Name is not null && p.Percent is >= 0 and <= 100));

    // Lấy danh sách trang thông tin (api/v1/pages).
    public async Task<IReadOnlyList<ContentPageSummary>> GetPagesAsync(CancellationToken cancellationToken = default)
        => Valid(await GetAsync<IReadOnlyList<ContentPageSummary>>(publicHttp, "api/v1/pages", cancellationToken),
            items => items.All(p => p is not null && !string.IsNullOrWhiteSpace(p.Slug) && p.Title is not null && p.Intro is not null));

    // Lấy một trang thông tin theo slug (api/v1/pages/{slug}).
    public async Task<ContentPageDetail> GetPageAsync(string slug, CancellationToken cancellationToken = default)
        => Valid(await GetAsync<ContentPageDetail>(publicHttp, "api/v1/pages/" + Uri.EscapeDataString(slug), cancellationToken),
            page => page.Title is not null && page.Intro is not null && page.Sections is not null
                && page.Sections.All(s => s is not null && s.Heading is not null && s.Paragraphs is not null && s.Paragraphs.All(p => p is not null)));

    // Lấy đánh giá công khai của sản phẩm, mỗi trang 20 đánh giá.
    public async Task<ProductReviewsResponse> GetReviewsAsync(int productId, int page, CancellationToken cancellationToken = default)
        => Valid(await GetAsync<ProductReviewsResponse>(publicHttp, $"api/v1/products/{productId}/reviews?page={page}&pageSize=20", cancellationToken),
            result => result.Items is not null && result.Page == page && result.PageSize is >= 1 and <= 100 && result.TotalItems >= 0
                && (result.AverageStars is null || result.AverageStars is >= 1 and <= 5)
                && result.Items.All(r => r is not null && r.Id > 0 && r.Stars is >= 1 and <= 5 && r.Author is not null && r.Content is not null));

    // Lấy sản phẩm khách đã nhận nên được đánh giá; cần đăng nhập.
    public async Task<IReadOnlyList<EligibleProductResponse>> GetEligibleProductsAsync(CancellationToken cancellationToken = default)
        => Valid(await GetAsync<IReadOnlyList<EligibleProductResponse>>(authorizedHttp, "api/v1/me/feedback/eligible-products", cancellationToken),
            items => items.All(p => p is not null && p.Id > 0 && p.Name is not null));

    // Lấy phản hồi của khách cho một sản phẩm, mỗi trang 20 phản hồi; cần đăng nhập.
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

    // Lấy mọi phản hồi khách đã gửi kèm trả lời mới nhất (api/v1/me/feedback không lọc sản phẩm), giống bảng "Phản hồi đã gửi" của Views/Customer/Feedback.cshtml.
    public async Task<PagedResponse<FeedbackResponse>> GetFeedbackHistoryAsync(int page, CancellationToken cancellationToken = default)
        => Valid(await GetAsync<PagedResponse<FeedbackResponse>>(authorizedHttp, $"api/v1/me/feedback?page={page}&pageSize=20", cancellationToken),
            result => result.Items is not null && result.Page == page && result.TotalItems >= 0
                && result.Items.All(r => r is not null && r.Id > 0 && r.Content is not null && r.Status is not null));

    // Lấy khảo sát đã giao cho khách (api/v1/me/surveys, tối đa 100 mục) như Views/Customer/Surveys.cshtml.
    public async Task<IReadOnlyList<SurveyResponse>> GetSurveysAsync(CancellationToken cancellationToken = default)
        => Valid(await GetAsync<PagedResponse<SurveyResponse>>(authorizedHttp, "api/v1/me/surveys?page=1&pageSize=100", cancellationToken),
            result => result.Items is not null && result.Items.All(s => s is not null && s.Id > 0 && s.Title is not null)).Items;

    // Lấy câu hỏi và lựa chọn của một khảo sát (api/v1/me/surveys/{id}).
    public async Task<SurveyDetailResponse> GetSurveyAsync(int surveyId, CancellationToken cancellationToken = default)
        => Valid(await GetAsync<SurveyDetailResponse>(authorizedHttp, $"api/v1/me/surveys/{surveyId}", cancellationToken),
            detail => detail.Survey is not null && detail.Questions is not null
                && detail.Questions.All(q => q is not null && q.Id > 0 && q.Title is not null && q.Options is not null));

    // Nộp câu trả lời một lần (api/v1/me/surveys/{id}/responses); API kiểm tra hạn, câu bắt buộc và trả 409 nếu đã nộp.
    public async Task SubmitSurveyAsync(int surveyId, SubmitSurveyRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await authorizedHttp.PostAsJsonAsync($"api/v1/me/surveys/{surveyId}/responses", request, cancellationToken);
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

    // Đổi phản hồi lỗi thành ContentException: 401, 503, 404 có câu riêng, còn lại lấy từ ProblemDetails.
    private static async Task<ContentException> FailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new ContentException("Vui lòng đăng nhập để tiếp tục.");
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
    // Trả dữ liệu hợp lệ, sai thì ném ContentException để màn hình không dùng dữ liệu hỏng.
    private static T Valid<T>(T value, Func<T, bool> isValid) => isValid(value)
        ? value : throw new ContentException("Dữ liệu cửa hàng chưa hợp lệ. Vui lòng thử lại.");
    // Phần ProblemDetails cần đọc.
    private sealed record ContentProblem(string? Title, string? Detail);
}

// Lỗi nội dung có câu thông báo hiện cho khách.
public sealed class ContentException(string message) : Exception(message);
