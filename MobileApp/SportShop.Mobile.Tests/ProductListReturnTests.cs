using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

public sealed class ProductListReturnTests
{
    private static ProductListReturnPoint Point() => new("catalog", 25, 24, 23, 22, 0, 2450,
        38, -20, 40, 3, "giày", "Danh mục Giày · Size 42", "price_asc");

    [Fact]
    public void WaitsForLayoutThenConsumesExactlyOnceWithoutLosingSourceState()
    {
        var state = new ProductListReturnState();
        var saved = Point();
        state.Capture(saved);
        Assert.False(state.TryConsume(3, false, out _));
        Assert.True(state.IsPending);
        Assert.True(state.TryConsume(3, true, out var restored));
        Assert.Same(saved, restored);
        Assert.Equal(25, restored!.ProductId);
        Assert.Equal(40, restored.LoadedCount);
        Assert.Equal("giày", restored.SearchText);
        Assert.Equal("price_asc", restored.SortCode);
        Assert.False(state.TryConsume(3, true, out _));
    }

    [Fact]
    public void NewResultsDiscardOldAnchorInsteadOfMovingAnotherFilter()
    {
        var state = new ProductListReturnState();
        state.Capture(Point());
        Assert.False(state.TryConsume(4, true, out _));
        Assert.False(state.IsPending);
    }

    [Fact]
    public void ExplicitContinuationCanRequestTheSameSourceAgain()
    {
        var state = new ProductListReturnState();
        state.Capture(Point());
        Assert.True(state.TryConsume(3, true, out _));
        state.RequestRestore();
        Assert.True(state.TryConsume(3, true, out var point));
        Assert.Equal(2450, point!.ScrollY);
    }

    [Fact]
    public async Task SuccessfulReplacementInvalidatesTheOldAnchorOnlyAfterNewResultsArrive()
    {
        var replacement = new TaskCompletionSource<PagedResponse<ProductSummaryDto>>();
        var api = new SourceApi(call => call.Query == "áo" ? replacement.Task
            : Task.FromResult(Page(1, Product(1), Product(2))));
        var vm = new CatalogViewModel(api) { SearchText = "giày" };
        Assert.Equal(0, vm.ResultVersion);
        await vm.LoadAsync();
        Assert.Equal(1, vm.ResultVersion);
        var previousProduct = vm.Products[1];
        var source = new ProductListReturnState();
        source.Capture(SourcePoint(vm));

        vm.SearchText = "áo";
        var replacing = vm.LoadAsync();
        Assert.True(vm.IsBusy);
        Assert.Equal(1, vm.ResultVersion);
        Assert.Same(previousProduct, vm.Products[1]);
        Assert.Equal("giày", source.Saved!.SearchText);

        replacement.SetResult(Page(1, Product(11), Product(12)));
        await replacing;
        Assert.Equal(2, vm.ResultVersion);
        Assert.Equal(new[] { 11, 12 }, vm.Products.Select(product => product.Id));
        Assert.DoesNotContain(previousProduct, vm.Products);
        Assert.Contains("áo", vm.AppliedFilterSummary);
        Assert.False(source.TryConsume(vm.ResultVersion, true, out _));
        Assert.False(source.IsPending);
    }

    [Fact]
    public async Task FailedAppendAndRetryPreserveLoadedObjectsSourceAnchorAndAppliedSearch()
    {
        var failAppend = true;
        var api = new SourceApi(call =>
        {
            if (call.Page == 2 && failAppend) throw new CatalogException("offline");
            return Task.FromResult(call.Page == 1 ? Page(1, Product(1), Product(2))
                : Page(2, Product(3), Product(4)));
        });
        var vm = new CatalogViewModel(api)
        {
            SearchText = "giày",
            SelectedCategory = new CategoryOption(3, "Giày"),
            SelectedSort = CatalogSortOption.All.Single(option => option.Code == "price_asc")
        };
        await vm.LoadAsync();
        var loadedProducts = vm.Products.ToArray();
        var version = vm.ResultVersion;
        var source = new ProductListReturnState();
        source.Capture(SourcePoint(vm));

        // Chưa áp dụng bản nháp mới; tải thêm phải tiếp tục đúng tập kết quả đã mở chi tiết.
        vm.SearchText = "từ khóa chưa áp dụng";
        vm.SelectedCategory = vm.Categories[0];
        vm.SelectedSort = vm.SortOptions[0];
        await vm.LoadAsync(append: true);
        Assert.True(vm.HasError);
        Assert.Equal(version, vm.ResultVersion);
        Assert.Equal(2, vm.Products.Count);
        Assert.Same(loadedProducts[0], vm.Products[0]);
        Assert.Same(loadedProducts[1], vm.Products[1]);
        Assert.True(source.IsPending);

        failAppend = false;
        await vm.LoadAsync(append: true);
        Assert.False(vm.HasError);
        Assert.Equal(version, vm.ResultVersion);
        Assert.Equal(new[] { 1, 2, 3, 4 }, vm.Products.Select(product => product.Id));
        Assert.Same(loadedProducts[0], vm.Products[0]);
        Assert.Same(loadedProducts[1], vm.Products[1]);
        Assert.Equal(new[]
        {
            new SourceCall("giày", 1, 3, "price_asc", true),
            new SourceCall("giày", 2, 3, "price_asc", true),
            new SourceCall("giày", 2, 3, "price_asc", true)
        }, api.Calls);
        Assert.Contains("giày", vm.AppliedFilterSummary);
        Assert.Contains("Giá tăng dần", vm.AppliedFilterSummary);
        Assert.Equal("từ khóa chưa áp dụng", vm.SearchText);
        Assert.True(source.TryConsume(vm.ResultVersion, true, out var restored));
        Assert.Equal(2, restored!.ProductId);
        Assert.Equal("giày", restored.SearchText);
        Assert.Equal("price_asc", restored.SortCode);
    }

    [Fact]
    public async Task FailedReplacementKeepsTheExistingResultVersionAndReturnPointUsable()
    {
        var api = new SourceApi(call => call.Query == "offline" ? throw new CatalogException("offline")
            : Task.FromResult(Page(1, Product(1), Product(2))));
        var vm = new CatalogViewModel(api) { SearchText = "giày" };
        await vm.LoadAsync();
        var version = vm.ResultVersion;
        var loadedProducts = vm.Products.ToArray();
        var source = new ProductListReturnState();
        source.Capture(SourcePoint(vm));
        vm.SearchText = "offline";
        await vm.LoadAsync();
        Assert.True(vm.HasError);
        Assert.Equal(version, vm.ResultVersion);
        Assert.Same(loadedProducts[0], vm.Products[0]);
        Assert.Same(loadedProducts[1], vm.Products[1]);
        Assert.Contains("giày", vm.AppliedFilterSummary);
        Assert.True(source.TryConsume(vm.ResultVersion, true, out var restored));
        Assert.Equal(2, restored!.ProductId);
        Assert.Equal("giày", restored.SearchText);
    }

    private static ProductListReturnPoint SourcePoint(CatalogViewModel vm) => new("catalog", 2, 1, 1, 0,
        0, 250, null, null, vm.Products.Count, vm.ResultVersion, vm.SearchText, vm.AppliedFilterSummary, vm.SelectedSort.Code);
    private static ProductSummaryDto Product(int id) => new(id, "Sản phẩm " + id, 3, "Giày", "đôi", null, 100_000, 100_000, true);
    private static PagedResponse<ProductSummaryDto> Page(int page, params ProductSummaryDto[] products)
        => new(products, page, 2, 4);

    private sealed record SourceCall(string? Query, int Page, int? CategoryId, string Sort, bool InStockOnly);
    private sealed class SourceApi(Func<SourceCall, Task<PagedResponse<ProductSummaryDto>>> respond) : ICatalogApi
    {
        public List<SourceCall> Calls { get; } = [];
        public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryDto>>([new(3, "Giày", 4)]);
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, CancellationToken cancellationToken)
            => GetProductsAsync(query, page, null, "new", true, cancellationToken);
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, int? categoryId,
            string sort, bool inStockOnly, CancellationToken cancellationToken)
        {
            var call = new SourceCall(query, page, categoryId, sort, inStockOnly);
            Calls.Add(call);
            return respond(call);
        }
    }
}
