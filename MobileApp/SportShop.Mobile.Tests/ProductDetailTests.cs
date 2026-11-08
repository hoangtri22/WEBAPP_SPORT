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
        Assert.False(await vm.AddToCartAsync());
        Assert.Single(cart.Added);
        pending.SetResult(EmptyCart);
        Assert.True(await adding);
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

    private static CartDto EmptyCart => new([], 0, 0);
    private sealed class DetailApi : ICatalogApi
    {
        public int Loads { get; private set; }
        public int SmallStock { get; set; } = 4;
        public bool NoVariants { get; set; }
        public Task<ProductDetailDto> GetProductAsync(int id, CancellationToken token = default)
        {
            Loads++;
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
