using SportShop.Contracts.Catalog;
using SportShop.Contracts.Commerce;
using SportShop.Contracts.Common;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

public sealed class ProductDetailTests
{
    // Chọn đúng biến thể cập nhật giá, ảnh, tồn kho và số lượng gửi sang giỏ.
    [Fact]
    public async Task SelectingVariantUpdatesPriceImageStockAndCartPayload()
    {
        var catalog = new DetailApi();
        var cart = new CartApi();
        var vm = new ProductDetailViewModel(catalog, cart);
        await vm.LoadAsync(1);
        Assert.Equal(11, vm.SelectedVariant!.Id);
        Assert.Equal("small.jpg", vm.ImageUrl);
        vm.Quantity = 900;
        Assert.Equal(4, vm.Quantity);
        vm.SelectedVariant = vm.Variants.Single(option => option.Id == 12);
        Assert.Equal(2, vm.Quantity);
        Assert.Equal("large.jpg", vm.ImageUrl);
        Assert.Contains("250.000", vm.PriceText);
        Assert.Contains("Size L", vm.SelectedVariant.Label);
        Assert.Contains("Màu Đen", vm.SelectedVariant.Label);
        Assert.True(await vm.AddToCartAsync());
        Assert.Equal((12, 2), Assert.Single(cart.Added));
        Assert.True(vm.HasStatus);
        Assert.Equal("Đã thêm sản phẩm vào giỏ hàng thành công!", vm.StatusMessage);
        vm.SelectedVariant = vm.Variants.Single(option => option.Id == 10);
        Assert.False(vm.CanBuy);
        Assert.False(await vm.AddToCartAsync());
        Assert.Single(cart.Added);
    }

    // Dữ liệu tồn kho thay đổi thì refresh đúng sản phẩm, giữ lựa chọn và không tự gửi lại thao tác thêm.
    [Fact]
    public async Task StockConflictRefreshesSelectionWithoutRepeatingMutation()
    {
        var catalog = new DetailApi();
        var cart = new CartApi { Add = (_, _) => throw new CommerceException("Tồn kho đã thay đổi", statusCode: 409) };
        var vm = new ProductDetailViewModel(catalog, cart);
        await vm.LoadAsync(1);
        vm.Quantity = 4;
        catalog.SmallStock = 1;
        Assert.False(await vm.AddToCartAsync());
        Assert.Equal(2, catalog.Loads);
        Assert.Equal(11, vm.SelectedVariant!.Id);
        Assert.Equal(1, vm.Quantity);
        Assert.True(vm.HasError);
        Assert.Contains("Tồn kho", vm.ErrorMessage);
        Assert.Single(cart.Added);
    }

    // Hai lần bấm trong khi request chưa kết thúc không tạo hai thao tác giỏ.
    [Fact]
    public async Task ConcurrentAddDoesNotDuplicateCartRequest()
    {
        var pending = new TaskCompletionSource<CartDto>();
        var cart = new CartApi { Add = (_, _) => pending.Task };
        var vm = new ProductDetailViewModel(new DetailApi(), cart);
        await vm.LoadAsync(1);
        var adding = vm.AddToCartAsync();
        Assert.True(vm.IsBusy);
        Assert.False(vm.CanBuy);
        Assert.False(vm.HasStatus);
        Assert.False(await vm.AddToCartAsync());
        Assert.Single(cart.Added);
        pending.SetResult(EmptyCart);
        Assert.True(await adding);
        Assert.False(vm.IsBusy);
        Assert.Equal("Đã thêm sản phẩm vào giỏ hàng thành công!", vm.StatusMessage);
    }

    [Fact]
    public async Task FailedAddClearsPreviousSuccessAndKeepsErrorVisible()
    {
        var cart = new CartApi();
        var vm = new ProductDetailViewModel(new DetailApi(), cart);
        await vm.LoadAsync(1);
        Assert.True(await vm.AddToCartAsync());
        Assert.True(vm.HasStatus);

        cart.Add = (_, _) => throw new CommerceException("Không thể thêm sản phẩm", statusCode: 503);
        Assert.False(await vm.AddToCartAsync());

        Assert.False(vm.HasStatus);
        Assert.Null(vm.StatusMessage);
        Assert.True(vm.HasError);
        Assert.Equal("Không thể thêm sản phẩm", vm.ErrorMessage);
        Assert.Equal(2, cart.Added.Count);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task CanceledAddDoesNotReportSuccessWhenTransportReturnsLate()
    {
        var pending = new TaskCompletionSource<CartDto>();
        var cart = new CartApi { Add = (_, _) => pending.Task };
        var vm = new ProductDetailViewModel(new DetailApi(), cart);
        await vm.LoadAsync(1);
        using var lifetime = new CancellationTokenSource();
        var adding = vm.AddToCartAsync(lifetime.Token);

        lifetime.Cancel();
        pending.SetResult(EmptyCart);

        Assert.False(await adding);
        Assert.Single(cart.Added);
        Assert.False(vm.HasStatus);
        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task AlreadyCanceledAddDoesNotSendCartMutation()
    {
        var cart = new CartApi();
        var vm = new ProductDetailViewModel(new DetailApi(), cart);
        await vm.LoadAsync(1);
        using var lifetime = new CancellationTokenSource();
        lifetime.Cancel();

        Assert.False(await vm.AddToCartAsync(lifetime.Token));

        Assert.Empty(cart.Added);
        Assert.False(vm.HasStatus);
        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    // Giới hạn backend 999 vẫn được giữ khi kho lớn; đổi sản phẩm không giữ lựa chọn cũ.
    [Fact]
    public async Task QuantityNeverExceedsStockOrBackendLimitAndNewProductResetsState()
    {
        var catalog = new DetailApi { SmallStock = 2_000 };
        var vm = new ProductDetailViewModel(catalog, new CartApi());
        await vm.LoadAsync(1);
        vm.Quantity = int.MaxValue;
        Assert.Equal(999, vm.Quantity);
        vm.Quantity = -5;
        Assert.Equal(1, vm.Quantity);
        vm.QuantityText = "không hợp lệ";
        Assert.Equal(1, vm.Quantity);
        vm.SelectedVariant = vm.Variants.Single(option => option.Id == 12);
        await vm.LoadAsync(2);
        Assert.Equal(2, vm.ProductId);
        Assert.Equal(11, vm.SelectedVariant!.Id);
        Assert.Equal(1, vm.Quantity);
    }

    [Fact]
    public async Task ProductWithoutVariantsCannotBeAdded()
    {
        var catalog = new DetailApi { NoVariants = true };
        var cart = new CartApi();
        var vm = new ProductDetailViewModel(catalog, cart);
        await vm.LoadAsync(1);
        Assert.True(vm.HasProduct);
        Assert.False(vm.CanBuy);
        Assert.False(await vm.AddToCartAsync());
        Assert.Empty(cart.Added);
    }

    // Gợi ý dùng card hiện hữu, chỉ giữ ID khác, cùng danh mục và còn hàng.
    [Fact]
    public async Task RelatedProductsExcludeCurrentDuplicatesOtherCategoriesAndUnavailableItems()
    {
        var catalog = new DetailApi
        {
            Related = (_, _) => Task.FromResult<IReadOnlyList<ProductSummaryDto>>(
                [RelatedProduct(1), RelatedProduct(2), RelatedProduct(2),
                    RelatedProduct(3) with { CategoryId = 4 }, RelatedProduct(4) with { InStock = false }])
        };
        var vm = new ProductDetailViewModel(catalog, new CartApi());
        await vm.LoadAsync(1);
        var card = Assert.Single(vm.RelatedProducts);
        Assert.Equal(2, card.Id);
        Assert.Equal("related.jpg", card.ImageUrl);
        Assert.Contains("150.000", card.PriceText);
        Assert.Equal(new[] { 1 }, catalog.RelatedCalls);
        Assert.True(vm.HasRelatedProducts);
        Assert.False(vm.ShowRelatedEmpty);
    }

    [Fact]
    public async Task EmptyRelatedProductsHaveTheirOwnEmptyState()
    {
        var vm = new ProductDetailViewModel(new DetailApi(), new CartApi());
        await vm.LoadAsync(1);
        Assert.True(vm.HasProduct);
        Assert.True(vm.ShowRelatedEmpty);
        Assert.False(vm.HasRelatedProducts);
        Assert.False(vm.HasRelatedError);
    }

    // Lỗi gợi ý không ảnh hưởng chi tiết/giỏ; thử lại chỉ gọi lại danh sách gợi ý.
    [Fact]
    public async Task RelatedFailureCanRetryWithoutReloadingDetailOrBlockingCart()
    {
        var catalog = new DetailApi { Related = (_, _) => throw new CatalogException("offline") };
        var cart = new CartApi();
        var vm = new ProductDetailViewModel(catalog, cart);
        await vm.LoadAsync(1);
        Assert.True(vm.HasRelatedError);
        Assert.False(vm.HasError);
        Assert.False(vm.ShowRelatedEmpty);
        Assert.True(await vm.AddToCartAsync());
        Assert.Single(cart.Added);
        catalog.Related = (_, _) => Task.FromResult<IReadOnlyList<ProductSummaryDto>>([RelatedProduct(2)]);
        await vm.LoadRelatedAsync();
        Assert.False(vm.HasRelatedError);
        Assert.Single(vm.RelatedProducts);
        Assert.Equal(1, catalog.Loads);
    }

    // Đổi sản phẩm hủy lượt gợi ý cũ kể cả API bỏ qua cancellation.
    [Fact]
    public async Task OldRelatedCompletionCannotOverwriteNewProductSuggestions()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<ProductSummaryDto>>();
        CancellationToken firstToken = default;
        var catalog = new DetailApi
        {
            Related = (id, token) =>
            {
                if (id != 1) return Task.FromResult<IReadOnlyList<ProductSummaryDto>>([RelatedProduct(4)]);
                firstToken = token;
                return pending.Task;
            }
        };
        var vm = new ProductDetailViewModel(catalog, new CartApi());
        var first = vm.LoadAsync(1);
        Assert.True(vm.IsRelatedLoading);
        Assert.True(vm.CanBuy);
        await vm.LoadAsync(2);
        Assert.True(firstToken.IsCancellationRequested);
        pending.SetResult([RelatedProduct(3)]);
        await first;
        Assert.Equal(2, vm.ProductId);
        Assert.Equal(4, Assert.Single(vm.RelatedProducts).Id);
        Assert.False(vm.IsRelatedLoading);
    }

    // Trang sản phẩm mới phải thắng request chi tiết cũ dù phản hồi đến sai thứ tự.
    [Fact]
    public async Task OldDetailCompletionCannotOverwriteNewProduct()
    {
        var pending = new TaskCompletionSource<ProductDetailDto>();
        var catalog = new DetailApi { Detail = (id, _) => id == 1 ? pending.Task : Task.FromResult(DetailProduct(id)) };
        var vm = new ProductDetailViewModel(catalog, new CartApi());
        var first = vm.LoadAsync(1);
        await vm.LoadAsync(2);
        pending.SetResult(DetailProduct(1));
        await first;
        Assert.Equal(2, vm.ProductId);
        Assert.Equal(new[] { 2 }, catalog.RelatedCalls);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ConcurrentRelatedRetryDoesNotDuplicateRequests()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<ProductSummaryDto>>();
        var catalog = new DetailApi { Related = (_, _) => pending.Task };
        var vm = new ProductDetailViewModel(catalog, new CartApi());
        var loading = vm.LoadAsync(1);
        await vm.LoadRelatedAsync();
        Assert.Single(catalog.RelatedCalls);
        pending.SetResult([]);
        await loading;
        Assert.True(vm.ShowRelatedEmpty);
    }

    [Fact]
    public async Task CanceledDetailDoesNotApplyLateDataAndCanBeRetried()
    {
        var pending = new TaskCompletionSource<ProductDetailDto>();
        var catalog = new DetailApi { Detail = (_, _) => pending.Task };
        var vm = new ProductDetailViewModel(catalog, new CartApi());
        using var lifetime = new CancellationTokenSource();
        var loading = vm.LoadAsync(1, lifetime.Token);
        lifetime.Cancel();
        pending.SetResult(DetailProduct(1));
        await loading;
        Assert.False(vm.HasProduct);
        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
        Assert.Empty(catalog.RelatedCalls);
        catalog.Detail = null;
        await vm.LoadAsync(1);
        Assert.True(vm.HasProduct);
        Assert.True(vm.CanBuy);
    }

    [Fact]
    public async Task CanceledRelatedRequestDoesNotDisplayLateResults()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<ProductSummaryDto>>();
        var catalog = new DetailApi { Related = (_, _) => pending.Task };
        var vm = new ProductDetailViewModel(catalog, new CartApi());
        using var lifetime = new CancellationTokenSource();
        var loading = vm.LoadAsync(1, lifetime.Token);
        lifetime.Cancel();
        pending.SetResult([RelatedProduct(2)]);
        await loading;
        Assert.Empty(vm.RelatedProducts);
        Assert.False(vm.IsRelatedLoading);
        Assert.False(vm.HasRelatedError);
        Assert.True(vm.CanBuy);
    }

    // Trở lại cùng trang cập nhật tồn kho nhưng giữ biến thể và số lượng đã chọn.
    [Fact]
    public async Task ReloadingSameProductKeepsVariantAndQuantityAndRefreshesRelatedProducts()
    {
        var catalog = new DetailApi();
        var vm = new ProductDetailViewModel(catalog, new CartApi());
        await vm.LoadAsync(1);
        vm.SelectedVariant = vm.Variants.Single(option => option.Id == 12);
        vm.Quantity = 2;
        await vm.LoadAsync(1);
        Assert.Equal(12, vm.SelectedVariant!.Id);
        Assert.Equal(2, vm.Quantity);
        Assert.Equal(new[] { 1, 1 }, catalog.RelatedCalls);
    }

    // Mô phỏng Picker MAUI bỏ SelectedItem khi danh sách bị Clear trong lần tải lại sau Back.
    [Theory]
    [InlineData(4, 3)]
    [InlineData(2, 2)]
    [InlineData(0, 1)]
    public async Task ReloadPreservesQuantityDespitePickerResetAndClampsOnlyToRefreshedStock(int refreshedStock, int expectedQuantity)
    {
        var catalog = new DetailApi();
        var vm = new ProductDetailViewModel(catalog, new CartApi());
        await vm.LoadAsync(1);
        vm.Quantity = 3;
        var selectionResets = 0;
        vm.Variants.CollectionChanged += (_, args) =>
        {
            if (args.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Reset) return;
            selectionResets++;
            vm.SelectedVariant = null;
        };
        catalog.SmallStock = refreshedStock;
        await vm.LoadAsync(1);
        Assert.Equal(1, selectionResets);
        Assert.Equal(11, vm.SelectedVariant!.Id);
        Assert.Equal(expectedQuantity, vm.Quantity);
        Assert.Equal(refreshedStock > 0, vm.CanBuy);
    }

    [Fact]
    public async Task ReturningToPageReplacesCanceledDetailRequestBeforeItFinishes()
    {
        var old = new TaskCompletionSource<ProductDetailDto>();
        var current = new TaskCompletionSource<ProductDetailDto>();
        var catalog = new DetailApi { Detail = (_, _) => old.Task };
        var vm = new ProductDetailViewModel(catalog, new CartApi());
        using var lifetime = new CancellationTokenSource();
        var leaving = vm.LoadAsync(1, lifetime.Token);
        lifetime.Cancel();
        catalog.Detail = (_, _) => current.Task;
        var returning = vm.LoadAsync(1);
        old.SetException(new CatalogException("old failure"));
        await leaving;
        Assert.True(vm.IsBusy);
        Assert.False(vm.HasError);
        current.SetResult(DetailProduct(1));
        await returning;
        Assert.True(vm.HasProduct);
        Assert.False(vm.IsBusy);
        Assert.False(vm.HasError);
    }

    private static ProductSummaryDto RelatedProduct(int id)
        => new(id, "Áo cùng loại", 3, "Áo", "chiếc", "related.jpg", 150_000, 150_000, true);
    private static ProductDetailDto DetailProduct(int id)
        => new(id, "Sản phẩm " + id, 3, "Áo", "chiếc", null, null, 150_000, 150_000, true,
            [new(id * 10, "S", "Trắng", 150_000, 3, null)]);

    private static CartDto EmptyCart => new([], 0, 0);
    private sealed class DetailApi : ICatalogApi
    {
        public int Loads { get; private set; }
        public int SmallStock { get; set; } = 4;
        public bool NoVariants { get; set; }
        public List<int> RelatedCalls { get; } = [];
        public Func<int, CancellationToken, Task<ProductDetailDto>>? Detail { get; set; }
        public Func<int, CancellationToken, Task<IReadOnlyList<ProductSummaryDto>>> Related { get; set; }
            = (_, _) => Task.FromResult<IReadOnlyList<ProductSummaryDto>>([]);
        public Task<IReadOnlyList<ProductSummaryDto>> GetRelatedProductsAsync(int id, CancellationToken token = default)
        {
            RelatedCalls.Add(id);
            return Related(id, token);
        }
        public Task<ProductDetailDto> GetProductAsync(int id, CancellationToken token = default)
        {
            Loads++;
            if (Detail is not null) return Detail(id, token);
            return Task.FromResult(new ProductDetailDto(id, "Áo thể thao", 3, "Áo", "chiếc", "Mô tả", "product.jpg", 100_000, 250_000, true,
                NoVariants ? [] : [new(10, "XS", "Đỏ", 100_000, 0, "empty.jpg"),
                    new(11, "S", "Trắng", 150_000, SmallStock, "small.jpg"), new(12, "L", "Đen", 250_000, 2, "large.jpg")]));
        }
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, CancellationToken token)
            => throw new NotSupportedException();
    }
    private sealed class CartApi : ICommerceApi
    {
        public List<(int, int)> Added { get; } = [];
        public Func<int, int, Task<CartDto>> Add { get; set; } = (_, _) => Task.FromResult(EmptyCart);
        public Task<CartDto> AddCartItemAsync(int id, int quantity, CancellationToken token = default)
        {
            Added.Add((id, quantity));
            return Add(id, quantity);
        }
        public Task<CartDto> GetCartAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<CartDto> SetCartItemQuantityAsync(int id, int quantity, CancellationToken token = default) => throw new NotSupportedException();
        public Task<CartDto> RemoveCartItemAsync(int id, CancellationToken token = default) => throw new NotSupportedException();
        public Task ClearCartAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<CheckoutDto> GetCheckoutAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EligibleVoucherDto>> GetEligibleVouchersAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<CheckoutDto> QuoteCheckoutAsync(string? code, CancellationToken token = default) => throw new NotSupportedException();
        public Task<PagedResponse<OrderDto>> GetOrdersAsync(int page = 1, int pageSize = 20, string? status = null, CancellationToken token = default) => throw new NotSupportedException();
        public Task<OrderDto> GetOrderDetailAsync(int id, CancellationToken token = default) => throw new NotSupportedException();
        public Task<OrderDto> PlaceOrderAsync(PlaceOrderRequest request, string key, CancellationToken token = default) => throw new NotSupportedException();
        public Task<OrderDto> CancelOrderAsync(int id, CancellationToken token = default) => throw new NotSupportedException();
    }
}
