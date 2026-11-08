using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

public sealed class HomeTests
{
    [Fact]
    public async Task HomeUsesServerIdsAndKeepsLastProductsAfterOfflineRefresh()
    {
        var api = new HomeApi();
        var vm = new HomeViewModel(api);
        await vm.LoadAsync();
        Assert.Equal(74, Assert.Single(vm.Products).Id);
        Assert.Equal(9, Assert.Single(vm.Categories).Id);
        api.Fail = true;
        await vm.LoadAsync();
        Assert.True(vm.HasError);
        Assert.Equal(74, Assert.Single(vm.Products).Id);
        Assert.False(vm.ShowEmpty);
        api.Fail = false;
        await vm.LoadAsync();
        Assert.False(vm.HasError);
        Assert.Single(vm.Products);
    }

    [Fact]
    public async Task CanceledHomeLoadDoesNotPublishPartialDataOrPreventRetry()
    {
        var api = new HomeApi();
        var vm = new HomeViewModel(api);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await vm.LoadAsync(cts.Token);
        Assert.False(vm.HasLoaded);
        Assert.False(vm.IsBusy);
        Assert.Empty(vm.Products);
        await vm.LoadAsync();
        Assert.True(vm.HasLoaded);
    }

    private sealed class HomeApi : ICatalogApi
    {
        public bool Fail { get; set; }
        public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryDto>>([new(9, "Từ máy chủ", 1)]);
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, CancellationToken cancellationToken)
        {
            if (Fail) throw new CatalogException("Mất mạng");
            return Task.FromResult(new PagedResponse<ProductSummaryDto>(
                [new(74, "Sản phẩm thật", 9, "Từ máy chủ", "đôi", null, 200000, 200000, true)], 1, 20, 1));
        }
    }
}
