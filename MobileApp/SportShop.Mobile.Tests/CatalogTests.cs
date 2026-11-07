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
}
