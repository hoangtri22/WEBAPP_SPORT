using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

public sealed class AdvancedCatalogTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AndroidTransportFailuresBecomeRetryableErrors(bool brokenStream)
    {
        using var http = new HttpClient(new Handler((_, _) => throw (brokenStream
            ? new IOException("raw transport details") : new WebException("raw transport details"))))
        { BaseAddress = new Uri("http://localhost:5080/") };
        var api = new CatalogApi(http);
        var listError = await Assert.ThrowsAsync<CatalogException>(() => api.GetProductsAsync(null, 1, default));
        var relatedError = await Assert.ThrowsAsync<CatalogException>(() => api.GetRelatedProductsAsync(1));
        Assert.DoesNotContain("raw transport", listError.Message);
        Assert.Equal(listError.Message, relatedError.Message);
    }

    [Fact]
    public async Task CanceledAndroidTransportFailureRemainsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var http = new HttpClient(new Handler((_, _) =>
        {
            cancellation.Cancel();
            throw new WebException("request aborted");
        })) { BaseAddress = new Uri("http://localhost:5080/") };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CatalogApi(http)
            .GetProductsAsync(null, 1, cancellation.Token));
    }

    [Fact]
    public async Task OptionsComeFromDedicatedEndpointIncludingVisibleSoldOutChoices()
    {
        Uri? requested = null;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            requested = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(new ProductFilterOptionsDto(["XL"], ["Đỏ"])) });
        })) { BaseAddress = new Uri("http://localhost:5080/") };
        var options = await new CatalogApi(http).GetFilterOptionsAsync();
        Assert.Equal("/api/v1/products/filter-options?inStockOnly=false", requested!.PathAndQuery);
        Assert.Equal("XL", Assert.Single(options.Sizes));
        Assert.Equal("Đỏ", Assert.Single(options.Colors));
    }

    [Theory]
    [InlineData("{\"sizes\":null,\"colors\":[]}")]
    [InlineData("{\"sizes\":[],\"colors\":[null]}")]
    [InlineData("{\"sizes\":[\" \"],\"colors\":[]}")]
    public async Task InvalidOptionsBecomeRetryableErrors(string json)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(json) }))) { BaseAddress = new Uri("http://localhost:5080/") };
        await Assert.ThrowsAsync<CatalogException>(() => new CatalogApi(http).GetFilterOptionsAsync());
    }

    [Fact]
    public async Task AdvancedQueryUsesInvariantPricesAndRepeatedEncodedValues()
    {
        Uri? requested = null;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            requested = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Page(2, 3, Product(2))) });
        })) { BaseAddress = new Uri("http://localhost:5080/") };
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            await new CatalogApi(http).GetProductsAsync("áo & giày", 2, 3, "price_desc", false,
                10.5m, 200.25m, ["M", "L/XL"], ["Đỏ", "Đen & Trắng"], default);
        }
        finally { CultureInfo.CurrentCulture = original; }
        Assert.Contains("page=2", requested!.Query);
        Assert.Contains("categoryId=3", requested.Query);
        Assert.Contains("sort=price_desc", requested.Query);
        Assert.Contains("inStockOnly=false", requested.Query);
        Assert.Contains("minPrice=10.5&maxPrice=200.25", requested.Query);
        Assert.Contains("sizes=M&sizes=L%2FXL", requested.Query);
        Assert.Contains("colors=%C4%90%E1%BB%8F&colors=%C4%90en%20%26%20Tr%E1%BA%AFng", requested.AbsoluteUri);
        Assert.Contains("q=%C3%A1o%20%26%20gi%C3%A0y", requested.AbsoluteUri);
    }

    [Fact]
    public async Task AllAppliedFiltersSurvivePagingRetryAndDraftChanges()
    {
        var api = new Api();
        var vm = new CatalogViewModel(api);
        await vm.OpenCategoryAsync(3);
        vm.SearchText = "áo";
        vm.MinPriceText = "100,50";
        vm.MaxPriceText = "900000";
        vm.Sizes[0].IsSelected = vm.Sizes[1].IsSelected = true;
        vm.Colors[0].IsSelected = true;
        vm.SelectedSort = vm.SortOptions[2];
        await vm.LoadAsync();
        var applied = api.Calls[^1];
        vm.ClearAdvancedFilters();
        vm.SearchText = "chưa áp dụng";
        vm.SelectedCategory = vm.Categories[0];
        vm.SelectedSort = vm.SortOptions[0];
        vm.InStockOnly = false;
        api.FailNext = true;
        await vm.LoadAsync(append: true);
        Assert.True(vm.HasError);
        Assert.Single(vm.Products);
        await vm.LoadAsync(append: true);
        Assert.Equal(applied with { Page = 2 }, api.Calls[^1]);
        Assert.Equal(api.Calls[^1], api.Calls[^2]);
        Assert.Equal(new[] { 1, 2 }, vm.Products.Select(item => item.Id));
        Assert.Contains("Size M, L", vm.AppliedFilterSummary);
        Assert.False(vm.HasError);
    }

    [Theory]
    [InlineData("-1", "")]
    [InlineData("abc", "")]
    [InlineData("10000000000", "")]
    [InlineData("1.234", "")]
    [InlineData("1,000,000", "")]
    [InlineData("200", "100")]
    public async Task InvalidPriceNeverReplacesResultsOrCallsApi(string min, string max)
    {
        var api = new Api();
        var vm = new CatalogViewModel(api);
        await vm.LoadAsync();
        vm.MinPriceText = min;
        vm.MaxPriceText = max;
        await vm.LoadAsync();
        Assert.True(vm.HasValidationError);
        Assert.Single(api.Calls);
        Assert.Single(vm.Products);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ZeroBoundAndSortKeepCategoryAndResetToFirstPage()
    {
        var api = new Api();
        var vm = new CatalogViewModel(api);
        await vm.OpenCategoryAsync(3);
        await vm.LoadAsync(append: true);
        vm.MinPriceText = "0";
        vm.MaxPriceText = "0";
        vm.SelectedSort = vm.SortOptions[1];
        await vm.LoadAsync();
        Assert.Equal(new Call("", 1, 3, "price_asc", true, 0, 0, "", ""), api.Calls[^1]);
        Assert.Single(vm.Products);
    }

    [Fact]
    public async Task ClearAdvancedPreservesCategoryAndFullResetRestoresDefaults()
    {
        var api = new Api();
        var vm = new CatalogViewModel(api);
        await vm.OpenCategoryAsync(3);
        vm.SearchText = "áo";
        vm.MinPriceText = "100";
        vm.Sizes[0].IsSelected = true;
        vm.Colors[0].IsSelected = true;
        vm.ClearAdvancedFilters();
        await vm.LoadAsync();
        Assert.Equal(new Call("áo", 1, 3, "new", true, null, null, "", ""), api.Calls[^1]);
        await vm.ResetAsync();
        Assert.Equal(new Call("", 1, null, "new", true, null, null, "", ""), api.Calls[^1]);
        Assert.All(vm.Sizes.Concat(vm.Colors), option => Assert.False(option.IsSelected));
        Assert.False(vm.HasValidationError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateOldResponseOrErrorCannotOverwriteNewFilter(bool failOld)
    {
        var pending = new TaskCompletionSource<PagedResponse<ProductSummaryDto>>();
        var api = new Api { Respond = call => call.Query == "old" ? pending.Task : Task.FromResult(Page(1, 1, Product(9))) };
        var vm = new CatalogViewModel(api) { SearchText = "old" };
        var old = vm.LoadAsync();
        vm.SearchText = "new";
        vm.MinPriceText = "100";
        await vm.LoadAsync();
        Assert.True(api.Tokens[0].IsCancellationRequested);
        if (failOld) pending.SetException(new CatalogException("old error"));
        else pending.SetResult(Page(1, 1, Product(1)));
        await old;
        Assert.Equal(9, Assert.Single(vm.Products).Id);
        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
        Assert.Contains("new", vm.AppliedFilterSummary);
    }

    [Fact]
    public async Task OldAppendCannotJoinNewSearchAndDuplicateAppendIsIgnored()
    {
        var pending = new TaskCompletionSource<PagedResponse<ProductSummaryDto>>();
        var api = new Api { Respond = call => call.Page == 2 ? pending.Task
            : Task.FromResult(Page(1, 3, Product(call.Query == "new" ? 9 : 1))) };
        var vm = new CatalogViewModel(api);
        await vm.LoadAsync();
        var append = vm.LoadAsync(append: true);
        await vm.LoadAsync(append: true);
        Assert.Equal(2, api.Calls.Count);
        vm.SearchText = "new";
        await vm.LoadAsync();
        pending.SetResult(Page(2, 3, Product(2)));
        await append;
        Assert.Equal(9, Assert.Single(vm.Products).Id);
        Assert.Equal(3, api.Calls.Count);
    }

    [Fact]
    public async Task ResetDuringAppendReturnsToFirstPageEvenWhenFiltersAlreadyMatchDefaults()
    {
        var pending = new TaskCompletionSource<PagedResponse<ProductSummaryDto>>();
        var api = new Api { Respond = call => call.Page == 2 ? pending.Task : Task.FromResult(Page(1, 3, Product(1))) };
        var vm = new CatalogViewModel(api);
        await vm.LoadAsync();
        var append = vm.LoadAsync(append: true);
        await vm.ResetAsync();
        Assert.Equal(new[] { 1, 2, 1 }, api.Calls.Select(call => call.Page));
        pending.SetResult(Page(2, 3, Product(2)));
        await append;
        Assert.Equal(1, Assert.Single(vm.Products).Id);
    }

    [Fact]
    public async Task CanceledRequestIgnoringTokenDoesNotApplyData()
    {
        var pending = new TaskCompletionSource<PagedResponse<ProductSummaryDto>>();
        var vm = new CatalogViewModel(new Api { Respond = _ => pending.Task });
        using var cancellation = new CancellationTokenSource();
        var load = vm.LoadAsync(cancellationToken: cancellation.Token);
        cancellation.Cancel();
        pending.SetResult(Page(1, 1, Product(1)));
        await load;
        Assert.Empty(vm.Products);
        Assert.False(vm.HasLoaded);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task DuplicateOnlyLastPageStopsPagingAndEmptySearchShowsEmpty()
    {
        var api = new Api { Respond = call => Task.FromResult(call.Query == "empty"
            ? Page(1, 0) : Page(call.Page, 2, Product(1))) };
        var vm = new CatalogViewModel(api);
        await vm.LoadAsync();
        await vm.LoadAsync(append: true);
        Assert.Single(vm.Products);
        Assert.False(vm.HasMore);
        await vm.LoadAsync(append: true);
        Assert.Equal(2, api.Calls.Count);
        vm.SearchText = "empty";
        await vm.LoadAsync();
        Assert.True(vm.ShowEmpty);
        Assert.False(vm.HasProducts);
    }

    [Fact]
    public async Task OptionsFailureDoesNotBlockProductsAndApplyRetriesOptions()
    {
        var api = new Api { FailOptions = true };
        var vm = new CatalogViewModel(api);
        await vm.LoadAsync();
        Assert.True(vm.HasFilterError);
        Assert.Single(vm.Products);
        Assert.False(vm.HasError);
        api.FailOptions = false;
        await vm.LoadAsync();
        Assert.False(vm.HasFilterError);
        Assert.Equal(new[] { "M", "L" }, vm.Sizes.Select(option => option.Value));
    }

    [Fact]
    public async Task FilterPageCanLoadOptionsWithoutQueryingProductsAndKeepDraftOnReopen()
    {
        var api = new Api();
        var vm = new CatalogViewModel(api);
        await vm.PrepareFiltersAsync();
        Assert.Empty(api.Calls);
        Assert.Equal(2, vm.Sizes.Count);
        vm.Sizes[0].IsSelected = true;
        await vm.PrepareFiltersAsync();
        Assert.True(vm.Sizes[0].IsSelected);
        Assert.False(vm.IsPreparingFilters);
    }

    [Fact]
    public async Task ApplyingSameDraftAwaitsExistingRequestWithoutDuplicatingIt()
    {
        var pending = new TaskCompletionSource<PagedResponse<ProductSummaryDto>>();
        var api = new Api { Respond = _ => pending.Task };
        var vm = new CatalogViewModel(api);
        var first = vm.ApplyFiltersAsync();
        var second = vm.ApplyFiltersAsync();
        Assert.False(second.IsCompleted);
        Assert.Single(api.Calls);
        pending.SetResult(Page(1, 1, Product(1)));
        await Task.WhenAll(first, second);
        Assert.False(vm.IsBusy);
        Assert.Single(vm.Products);
    }

    [Fact]
    public async Task FilterPageRecoversOptionsAfterCatalogLifetimeIsCanceled()
    {
        var api = new DelayedOptionsApi();
        var vm = new CatalogViewModel(api);
        using var cancellation = new CancellationTokenSource();
        var catalog = vm.LoadAsync(cancellationToken: cancellation.Token);
        cancellation.Cancel();
        var filterPage = vm.PrepareFiltersAsync();
        await Task.WhenAll(catalog, filterPage);
        Assert.Equal(2, api.OptionCalls);
        Assert.Equal("XL", Assert.Single(vm.Sizes).Value);
        Assert.False(vm.IsPreparingFilters);
    }

    private sealed class DelayedOptionsApi : ICatalogApi
    {
        public int OptionCalls { get; private set; }
        public async Task<ProductFilterOptionsDto> GetFilterOptionsAsync(CancellationToken token = default)
        {
            if (++OptionCalls == 1) await Task.Delay(Timeout.Infinite, token);
            return new ProductFilterOptionsDto(["XL"], ["Đỏ"]);
        }
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, CancellationToken token)
            => Task.FromResult(Page(page, 0));
    }

    [Fact]
    public async Task CanceledMetadataErrorRemainsCancellationAndCanBeRetried()
    {
        var pending = new TaskCompletionSource<ProductFilterOptionsDto>();
        var api = new FailedCanceledOptionsApi(pending.Task);
        var vm = new CatalogViewModel(api);
        using var cancellation = new CancellationTokenSource();
        var preparing = vm.PrepareFiltersAsync(cancellation.Token);
        cancellation.Cancel();
        pending.SetException(new CatalogException("late network failure"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparing);
        Assert.False(vm.IsPreparingFilters);
        Assert.False(vm.HasFilterError);
        await vm.PrepareFiltersAsync();
        Assert.Single(vm.Sizes);
    }

    private sealed class FailedCanceledOptionsApi(Task<ProductFilterOptionsDto> first) : ICatalogApi
    {
        private int _calls;
        public Task<ProductFilterOptionsDto> GetFilterOptionsAsync(CancellationToken token = default)
            => ++_calls == 1 ? first : Task.FromResult(new ProductFilterOptionsDto(["M"], []));
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, CancellationToken token)
            => Task.FromResult(Page(page, 0));
    }

    private static ProductSummaryDto Product(int id) => new(id, "Áo", 3, "Áo", "cái", null, 100, 200, true);
    private static PagedResponse<ProductSummaryDto> Page(int page, int total, params ProductSummaryDto[] products)
        => new(products, page, 1, total);
    private sealed record Call(string? Query, int Page, int? Category, string Sort, bool Stock,
        decimal? Min, decimal? Max, string Sizes, string Colors);
    private sealed class Api : ICatalogApi
    {
        public List<Call> Calls { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public bool FailNext { get; set; }
        public bool FailOptions { get; set; }
        public Func<Call, Task<PagedResponse<ProductSummaryDto>>>? Respond { get; init; }
        public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken token = default)
            => Task.FromResult<IReadOnlyList<CategoryDto>>([new(3, "Áo", 3)]);
        public Task<ProductFilterOptionsDto> GetFilterOptionsAsync(CancellationToken token = default)
            => FailOptions ? throw new CatalogException("offline options")
                : Task.FromResult(new ProductFilterOptionsDto(["M", "L"], ["Đỏ", "Xanh"]));
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, CancellationToken token)
            => GetProductsAsync(query, page, null, "new", true, null, null, [], [], token);
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, int? category,
            string sort, bool stock, decimal? min, decimal? max, IReadOnlyList<string> sizes,
            IReadOnlyList<string> colors, CancellationToken token)
        {
            var call = new Call(query, page, category, sort, stock, min, max, string.Join("|", sizes), string.Join("|", colors));
            Calls.Add(call);
            Tokens.Add(token);
            if (FailNext) { FailNext = false; throw new CatalogException("offline"); }
            return Respond?.Invoke(call) ?? Task.FromResult(Page(page, 3, Product(page)));
        }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }
}
