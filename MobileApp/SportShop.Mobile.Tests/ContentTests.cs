using System.Net;
using System.Net.Http.Json;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

public sealed class ContentTests
{
    [Fact]
    public async Task PublicContentNeverUsesAuthorizedClientAndSlugsAreEncoded()
    {
        var publicHandler = new Handler(_ => Json(new ContentPageDetail("HỖ TRỢ", "Size", "Chọn size", [])));
        var privateHandler = new Handler(_ => throw new InvalidOperationException("Public content cannot require authentication."));
        var api = new ContentApi(Client(publicHandler), Client(privateHandler));
        var result = await api.GetPageAsync("chon size/?");
        Assert.Equal("Size", result.Title);
        Assert.EndsWith("api/v1/pages/chon%20size%2F%3F", publicHandler.Paths.Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(privateHandler.Paths);
    }

    [Fact]
    public async Task FeedbackUsesPrivateClientAndFiltersHistoryBeforePaging()
    {
        var publicHandler = new Handler(_ => throw new InvalidOperationException("Feedback must use authorized client."));
        var privateHandler = new Handler(_ => Json(new PagedResponse<FeedbackResponse>([], 2, 20, 35)));
        var api = new ContentApi(Client(publicHandler), Client(privateHandler));
        await api.GetMyFeedbackAsync(7, 2);
        Assert.Contains("productId=7&page=2&pageSize=20", privateHandler.Paths.Single());
    }

    [Fact]
    public async Task InvalidJsonAndUnavailableServerProduceRetryableContentErrors()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{bad-json") });
        var api = new ContentApi(Client(handler), Client(handler));
        Assert.Contains("chưa hợp lệ", (await Assert.ThrowsAsync<ContentException>(() => api.GetPromotionsAsync())).Message);
        handler.Response = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        Assert.Contains("thử lại", (await Assert.ThrowsAsync<ContentException>(() => api.GetPagesAsync())).Message);
    }

    [Theory]
    [InlineData("{\"items\":null,\"page\":1,\"pageSize\":20,\"totalItems\":0,\"averageStars\":null}")]
    [InlineData("{\"items\":[{\"id\":1,\"author\":\"Khách\",\"stars\":99,\"content\":\"Tốt\",\"sentAt\":\"2026-11-08T00:00:00Z\"}],\"page\":1,\"pageSize\":20,\"totalItems\":1,\"averageStars\":5}")]
    [InlineData("{\"items\":[null],\"page\":1,\"pageSize\":20,\"totalItems\":1,\"averageStars\":5}")]
    public async Task MalformedReviewPayloadIsReportedWithoutCrashingPage(string payload)
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json") });
        var api = new ContentApi(Client(handler), Client(handler));
        Assert.Contains("chưa hợp lệ", (await Assert.ThrowsAsync<ContentException>(() => api.GetReviewsAsync(7, 1))).Message);
    }

    [Theory]
    [InlineData("{\"eyebrow\":\"Hỗ trợ\",\"title\":\"Size\",\"intro\":\"Nội dung\",\"sections\":null}")]
    [InlineData("{\"eyebrow\":\"Hỗ trợ\",\"title\":\"Size\",\"intro\":\"Nội dung\",\"sections\":[{\"heading\":\"Giày\",\"paragraphs\":null}]}")]
    public async Task MalformedPageSectionsAreReportedAsContentErrors(string payload)
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json") });
        var api = new ContentApi(Client(handler), Client(handler));
        Assert.Contains("chưa hợp lệ", (await Assert.ThrowsAsync<ContentException>(() => api.GetPageAsync("chon-size"))).Message);
    }

    [Fact]
    public async Task GuestCanReadAndPaginatePublicReviewsWithoutAccountRequests()
    {
        var api = new FakeContent();
        var vm = new ReviewsViewModel(api, new Session(false));
        await vm.LoadAsync(7, "Giày chạy");
        Assert.Single(vm.Reviews);
        Assert.True(vm.HasMore);
        Assert.True(vm.IsGuest);
        Assert.False(vm.CanSend);
        Assert.Equal(0, api.AccountCalls);
        await vm.LoadMoreAsync();
        Assert.Equal(2, vm.Reviews.Count);
        Assert.False(vm.HasMore);
        Assert.Equal([1, 2], api.ReviewPages);
    }

    [Fact]
    public async Task FailedAppendPreservesExistingReviewsAndRetriesTheSamePage()
    {
        var api = new FakeContent();
        var vm = new ReviewsViewModel(api, new Session(false));
        await vm.LoadAsync(7, "Giày");
        api.FailAppend = true;
        await vm.LoadMoreAsync();
        Assert.True(vm.HasError);
        Assert.Single(vm.Reviews);
        api.FailAppend = false;
        await vm.LoadMoreAsync();
        Assert.Equal([1, 2, 2], api.ReviewPages);
        Assert.Equal(2, vm.Reviews.Count);
    }

    [Fact]
    public async Task NotPurchasedCannotSendAndHistoryStillShowsPrivateReview()
    {
        var api = new FakeContent { Eligible = false };
        var vm = new ReviewsViewModel(api, new Session(true));
        await vm.LoadAsync(7, "Giày");
        Assert.False(vm.IsEligible);
        Assert.Single(vm.MyReviews);
        vm.Content = "Đánh giá";
        await vm.SendAsync();
        Assert.Null(api.Sent);
        Assert.True(vm.HasError);
    }

    [Theory]
    [InlineData(0, "Đánh giá")]
    [InlineData(6, "Đánh giá")]
    [InlineData(5, "   ")]
    public async Task InvalidReviewDoesNotReachServer(int stars, string content)
    {
        var api = new FakeContent();
        var vm = new ReviewsViewModel(api, new Session(true));
        await vm.LoadAsync(7, "Giày");
        (vm.Stars, vm.Content) = (stars, content);
        await vm.SendAsync();
        Assert.Null(api.Sent);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task SentReviewClearsFormAndDoesNotInviteDuplicateSubmissionWhenRefreshFails()
    {
        var api = new FakeContent { FailPublicAfterSend = true };
        var vm = new ReviewsViewModel(api, new Session(true));
        await vm.LoadAsync(7, "Giày");
        vm.Content = "  Sản phẩm tốt  ";
        vm.Stars = 4;
        await vm.SendAsync();
        Assert.Equal(new FeedbackRequest(7, 4, "Sản phẩm tốt"), api.Sent);
        Assert.Equal("", vm.Content);
        Assert.True(vm.HasStatus);
        Assert.False(vm.HasError);
        Assert.Single(vm.MyReviews);
    }

    [Fact]
    public async Task FailedSendKeepsDraftAndAccountNetworkErrorDoesNotHidePublicReviews()
    {
        var api = new FakeContent { SendFailure = true };
        var vm = new ReviewsViewModel(api, new Session(true));
        await vm.LoadAsync(7, "Giày");
        vm.Content = "Nội dung đang viết";
        await vm.SendAsync();
        Assert.Equal("Nội dung đang viết", vm.Content);
        Assert.True(vm.HasError);
        api.AccountFailure = true;
        await vm.RefreshAsync();
        Assert.Single(vm.Reviews);
        Assert.True(vm.HasAccountError);
        Assert.False(vm.HasError);
        Assert.Contains("Chưa kiểm tra", vm.EligibilityText);
    }

    [Fact]
    public async Task PublicReviewOutageDoesNotPreventReadingPrivateHistory()
    {
        var api = new FakeContent { PublicFailure = true };
        var vm = new ReviewsViewModel(api, new Session(true));
        await vm.LoadAsync(7, "Giày");
        Assert.True(vm.HasError);
        Assert.True(vm.IsEligible);
        Assert.Single(vm.MyReviews);
    }

    [Fact]
    public async Task InfoFailureDoesNotReplaceAnAlreadyVisiblePage()
    {
        var api = new FakeContent();
        var vm = new InfoPagesViewModel(api);
        await vm.LoadAsync("chon-size");
        api.PageFailure = true;
        await vm.LoadAsync("doi-tra");
        Assert.Equal("chon-size", vm.CurrentSlug);
        Assert.Equal("Size", vm.Current!.Title);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task PromotionsExposeEmptyAndRetryStatesWithoutDemoData()
    {
        var api = new FakeContent();
        var vm = new PromotionsViewModel(api);
        await vm.LoadAsync();
        Assert.True(vm.ShowEmpty);
        api.PromoFailure = true;
        await vm.LoadAsync();
        Assert.True(vm.HasError);
        Assert.False(vm.ShowEmpty);
        Assert.Empty(vm.Promotions);
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://shop.test/") };
    private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Response { get; set; } = response;
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(Response(request));
        }
    }

    private sealed class FakeContent : IContentApi
    {
        public List<int> ReviewPages { get; } = [];
        public int AccountCalls { get; private set; }
        public bool Eligible { get; set; } = true;
        public bool FailAppend { get; set; }
        public bool FailPublicAfterSend { get; set; }
        public bool PublicFailure { get; set; }
        public bool AccountFailure { get; set; }
        public bool SendFailure { get; set; }
        public bool PageFailure { get; set; }
        public bool PromoFailure { get; set; }
        public FeedbackRequest? Sent { get; private set; }
        public Task<IReadOnlyList<PromotionDto>> GetPromotionsAsync(CancellationToken cancellationToken = default)
            => PromoFailure ? throw new ContentException("Mất mạng") : Task.FromResult<IReadOnlyList<PromotionDto>>([]);
        public Task<IReadOnlyList<ContentPageSummary>> GetPagesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentPageSummary>>([]);
        public Task<ContentPageDetail> GetPageAsync(string slug, CancellationToken cancellationToken = default)
            => PageFailure ? throw new ContentException("Mất mạng") : Task.FromResult(new ContentPageDetail("HỖ TRỢ", "Size", "Chọn size", []));
        public Task<ProductReviewsResponse> GetReviewsAsync(int productId, int page, CancellationToken cancellationToken = default)
        {
            ReviewPages.Add(page);
            if (PublicFailure || FailAppend && page == 2 || FailPublicAfterSend && Sent is not null) throw new ContentException("Mất mạng");
            return Task.FromResult(new ProductReviewsResponse([new ProductReviewDto(page, "Khách hàng", 5, "Tốt", DateTimeOffset.UtcNow, null)], page, 1, 2, 5));
        }
        public Task<IReadOnlyList<EligibleProductResponse>> GetEligibleProductsAsync(CancellationToken cancellationToken = default)
        {
            AccountCalls++;
            if (AccountFailure) throw new ContentException("Mất mạng");
            return Task.FromResult<IReadOnlyList<EligibleProductResponse>>(Eligible ? [new(7, "Giày")] : []);
        }
        public Task<PagedResponse<FeedbackResponse>> GetMyFeedbackAsync(int productId, int page, CancellationToken cancellationToken = default)
        {
            AccountCalls++;
            return Task.FromResult(new PagedResponse<FeedbackResponse>([new(88, productId, "Giày", 5, "Riêng tư", DateTimeOffset.UtcNow, "moi", null)], page, 20, 1));
        }
        public Task SendFeedbackAsync(FeedbackRequest request, CancellationToken cancellationToken = default)
        {
            if (SendFailure) throw new ContentException("Gửi thất bại");
            Sent = request;
            return Task.CompletedTask;
        }
    }

    private sealed class Session(bool signedIn) : ISessionManager
    {
        public bool IsSignedIn => signedIn;
        public event Action? Expired { add { } remove { } }
        public Task<bool> RestoreAsync(CancellationToken cancellationToken) => Task.FromResult(signedIn);
        public Task StartAsync(TokenResponse tokens) => Task.CompletedTask;
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<string?> RenewAsync(string rejectedAccessToken, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task SignOutAsync() => Task.CompletedTask;
    }
}
