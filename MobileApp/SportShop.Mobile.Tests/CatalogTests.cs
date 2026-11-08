using System.Net;
using System.Net.Http.Json;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

// Test API và ViewModel của trang sản phẩm.
public sealed class CatalogTests
{
    // Kiểm tra từ khóa được mã hóa URL và giữ nguyên URL ảnh của backend.
    [Fact]
    public async Task QueryIsEscapedAndBackendImageUrlIsPreserved()
    {
        Uri? requested = null;
        using var http = Client((request, _) =>
        {
            requested = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Page(1, 1, Product(65)))
            });
        });
        var result = await new CatalogApi(http).GetProductsAsync("  giày & áo  ", 1, default);
        Assert.Contains("q=gi%C3%A0y%20%26%20%C3%A1o", requested!.AbsoluteUri);
        Assert.Contains("inStockOnly=true", requested.Query);
        Assert.Equal("http://localhost:5080/images/product.jpg", Assert.Single(result.Items).ImageUrl);
    }

    // Kiểm tra phản hồi sai thành lỗi có thể thử lại.
    [Theory]
    [InlineData("null")]
    [InlineData("{\"items\":null,\"page\":1,\"pageSize\":20,\"totalItems\":0}")]
    [InlineData("<html>gateway error</html>")]
    public async Task InvalidResponsesBecomeRetryableErrors(string json)
    {
        using var http = Client((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(json) }));
        await Assert.ThrowsAsync<CatalogException>(() => new CatalogApi(http).GetProductsAsync(null, 1, default));
    }

    // Kiểm tra lỗi máy chủ không lộ nội dung gốc.
    [Fact]
    public async Task ServerFailureDoesNotExposeRawErrorBody()
    {
        using var http = Client((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        { Content = new StringContent("internal database diagnostics") }));
        var error = await Assert.ThrowsAsync<CatalogException>(() => new CatalogApi(http).GetProductsAsync(null, 1, default));
        Assert.DoesNotContain("database diagnostics", error.Message);
    }

    // Kiểm tra hủy vẫn là hủy, còn timeout thì thử lại được.
    [Fact]
    public async Task CancellationRemainsCancellationAndTimeoutIsRetryable()
    {
        using var http = Client((_, token) => Task.FromCanceled<HttpResponseMessage>(token));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CatalogApi(http).GetProductsAsync(null, 1, canceled.Token));
        using var timeoutHttp = Client((_, _) => throw new TaskCanceledException());
        await Assert.ThrowsAsync<CatalogException>(() => new CatalogApi(timeoutHttp).GetProductsAsync(null, 1, default));
    }

    // Kiểm tra lỗi trang sau giữ sản phẩm và thử lại đúng trang, từ khóa.
    [Fact]
    public async Task FailedNextPageKeepsProductsAndRetriesSamePageAndQuery()
    {
        var calls = new List<(string?, int)>();
        var fail = true;
        var vm = new CatalogViewModel(new FakeApi((query, page, _) =>
        {
            calls.Add((query, page));
            if (page == 2 && fail) throw new CatalogException("offline");
            return Task.FromResult(Page(page, 2, Product(page)));
        }));
        vm.SearchText = "giày";
        await vm.LoadAsync();
        vm.SearchText = "áo chưa tìm";
        await vm.LoadAsync(append: true);
        Assert.True(vm.HasError);
        Assert.Single(vm.Products);
        fail = false;
        await vm.LoadAsync(append: true);
        Assert.Equal(new (string?, int)[] { ("giày", 1), ("giày", 2), ("giày", 2) }, calls);
        Assert.Equal(2, vm.Products.Count);
        Assert.False(vm.HasMore);
        Assert.False(vm.HasError);
    }

    // Kiểm tra làm mới đồng thời không gửi trùng request.
    [Fact]
    public async Task ConcurrentRefreshDoesNotSendDuplicateRequests()
    {
        var pending = new TaskCompletionSource<PagedResponse<ProductSummaryDto>>();
        var count = 0;
        var vm = new CatalogViewModel(new FakeApi((_, _, _) => { count++; return pending.Task; }));
        var load = vm.LoadAsync();
        await vm.LoadAsync();
        Assert.Equal(1, count);
        Assert.True(vm.IsBusy);
        pending.SetResult(Page(1, 0));
        await load;
        Assert.True(vm.ShowEmpty);
        Assert.False(vm.IsBusy);
    }

    // Kiểm tra tìm mới thay kết quả cũ.
    [Fact]
    public async Task NewSearchReplacesPreviousResults()
    {
        var vm = new CatalogViewModel(new FakeApi((query, _, _) => Task.FromResult(
            query == "không có" ? Page(1, 0) : Page(1, 1, Product(1)))));
        await vm.LoadAsync();
        vm.SearchText = "không có";
        await vm.LoadAsync();
        Assert.Empty(vm.Products);
        Assert.True(vm.ShowEmpty);
        Assert.False(vm.HasMore);
    }

    // Bộ lọc đang nhập không làm thay đổi điều kiện của trang tải thêm/retry.
    [Fact]
    public async Task PagingKeepsAppliedCategorySortAndStockFilterUntilNewSearch()
    {
        var api = new FilteringApi();
        var vm = new CatalogViewModel(api);
        await vm.OpenCategoryAsync(3);
        vm.SelectedSort = vm.SortOptions.Single(option => option.Code == "price_desc");
        vm.InStockOnly = false;
        vm.SearchText = "áo";
        await vm.LoadAsync();
        vm.SelectedCategory = vm.Categories[0];
        vm.SelectedSort = vm.SortOptions[0];
        vm.InStockOnly = true;
        vm.SearchText = "chưa áp dụng";
        await vm.LoadAsync(append: true);
        Assert.Equal(("áo", 2, (int?)3, "price_desc", false), api.Calls[^1]);
        await vm.LoadAsync();
        Assert.Equal(("chưa áp dụng", 1, (int?)null, "new", true), api.Calls[^1]);
    }

    // Lỗi danh mục không chặn sản phẩm; bấm áp dụng sẽ tải lại bộ lọc.
    [Fact]
    public async Task CategoryFailureKeepsProductsAvailableAndCanBeRetried()
    {
        var api = new FilteringApi { FailCategories = true };
        var vm = new CatalogViewModel(api);
        await vm.LoadAsync();
        Assert.True(vm.HasFilterError);
        Assert.False(vm.HasError);
        Assert.Single(vm.Products);
        api.FailCategories = false;
        await vm.LoadAsync();
        Assert.False(vm.HasFilterError);
        Assert.Equal(2, vm.Categories.Count);
    }

    // Khi tìm mới lỗi, tải thêm vẫn dùng điều kiện của kết quả đang hiện.
    [Fact]
    public async Task FailedNewSearchDoesNotMixPreviousResultsWithNewFilter()
    {
        var api = new FilteringApi();
        var vm = new CatalogViewModel(api);
        await vm.OpenCategoryAsync(3);
        api.FailQuery = "offline";
        vm.SearchText = "offline";
        vm.SelectedCategory = vm.Categories[0];
        await vm.LoadAsync();
        Assert.True(vm.HasError);
        Assert.Single(vm.Products);
        await vm.LoadAsync(append: true);
        Assert.Equal(("", 2, (int?)3, "new", true), api.Calls[^1]);
        Assert.Equal(2, vm.Products.Count);
    }

    // Chọn danh mục từ Trang chủ trong khi catalog còn tải không làm mất lựa chọn mới.
    [Fact]
    public async Task OpeningCategoryWaitsForPreviousRequestThenAppliesRequestedCategory()
    {
        var pending = new TaskCompletionSource<PagedResponse<ProductSummaryDto>>();
        var api = new FilteringApi { Pending = pending.Task };
        var vm = new CatalogViewModel(api);
        var initial = vm.LoadAsync();
        var opening = vm.OpenCategoryAsync(3);
        Assert.False(opening.IsCompleted);
        api.Pending = null;
        pending.SetResult(Page(1, 1, Product(1)));
        await initial;
        await opening;
        Assert.Equal((int?)3, vm.SelectedCategory!.Id);
        Assert.Equal(("", 1, (int?)3, "new", true), api.Calls[^1]);
    }

    // Query gửi API thực sự chứa đủ bộ lọc và không làm hỏng từ khóa tiếng Việt.
    [Fact]
    public async Task FilteredRequestUsesBackendCategoryAndSortParameters()
    {
        Uri? requested = null;
        using var http = Client((request, _) =>
        {
            requested = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Page(1, 0)) });
        });
        await new CatalogApi(http).GetProductsAsync("áo & giày", 1, 3, "price_asc", false, default);
        Assert.Contains("categoryId=3", requested!.Query);
        Assert.Contains("sort=price_asc", requested.Query);
        Assert.Contains("inStockOnly=false", requested.Query);
        Assert.Contains("q=%C3%A1o%20%26%20gi%C3%A0y", requested.AbsoluteUri);
    }

    // API chi tiết không chấp nhận nhầm ID sản phẩm hoặc dữ liệu biến thể thiếu.
    [Fact]
    public async Task DetailRejectsMismatchedProductOrMissingVariants()
    {
        using var http = Client((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ProductDetailDto(99, "Sai sản phẩm", 1, "Giày", "đôi", null, null, 10, 10, true, []))
        }));
        await Assert.ThrowsAsync<CatalogException>(() => new CatalogApi(http).GetProductAsync(1));
    }

    // Tạo HttpClient dùng Handler giả.
    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        => new(new Handler(respond)) { BaseAddress = new Uri("http://localhost:5080/") };
    // Tạo sản phẩm mẫu.
    private static ProductSummaryDto Product(int id) => new(id, "Sản phẩm", 1, "Giày", "đôi",
        "http://localhost:5080/images/product.jpg", 150000, 150000, true);
    // Tạo trang kết quả mẫu.
    private static PagedResponse<ProductSummaryDto> Page(int page, int total, params ProductSummaryDto[] products)
        => new(products, page, 1, total);
    // HttpMessageHandler giả trả phản hồi theo hàm respond.
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        // Trả phản hồi giả.
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }
    // ICatalogApi giả trả kết quả theo hàm get.
    private sealed class FakeApi(Func<string?, int, CancellationToken, Task<PagedResponse<ProductSummaryDto>>> get) : ICatalogApi
    {
        // Gọi hàm get đã cài.
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, CancellationToken token)
            => get(query, page, token);
    }

    private sealed class FilteringApi : ICatalogApi
    {
        public bool FailCategories { get; set; }
        public string? FailQuery { get; set; }
        public Task<PagedResponse<ProductSummaryDto>>? Pending { get; set; }
        public List<(string?, int, int?, string, bool)> Calls { get; } = [];
        public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken token = default)
            => FailCategories ? throw new CatalogException("offline")
                : Task.FromResult<IReadOnlyList<CategoryDto>>([new(3, "Áo", 2)]);
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, CancellationToken token)
            => GetProductsAsync(query, page, null, "new", true, token);
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, int? categoryId,
            string sort, bool inStockOnly, CancellationToken token)
        {
            Calls.Add((query, page, categoryId, sort, inStockOnly));
            if (query == FailQuery && FailQuery is not null) throw new CatalogException("offline");
            if (Pending is not null) return Pending;
            return Task.FromResult(Page(page, 2, Product(page)));
        }
    }
}
