using SportShop.Contracts.Catalog;
using SportShop.Contracts.Commerce;
using SportShop.Contracts.Common;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

public sealed class QuickPurchaseTests
{
    [Fact]
    public async Task MultipleVariantsRequireExplicitChoiceAndUseChosenVariantAndQuantity()
    {
        var catalog = new CatalogStub();
        var commerce = new CommerceStub();
        var vm = new QuickPurchaseViewModel(catalog, commerce, 1, false);
        await vm.LoadAsync();
        Assert.Null(vm.SelectedVariant);
        Assert.False(vm.CanSubmit);
        Assert.False(await vm.SubmitAsync());
        Assert.Empty(commerce.Added);
        Assert.Contains("chọn size", vm.ErrorMessage);
        vm.SelectedVariant = vm.Variants.Single(option => option.Id == 12);
        vm.Quantity = 2;
        Assert.Equal("black.jpg", vm.ImageUrl);
        Assert.Contains("250.000", vm.PriceText);
        Assert.True(vm.CanSubmit);
        Assert.True(await vm.SubmitAsync());
        Assert.Equal((12, 2), Assert.Single(commerce.Added));
        Assert.True(vm.HasSucceeded);
        Assert.False(vm.CanSubmit);
        Assert.Equal(new[] { 1 }, catalog.Requested);
    }

    [Fact]
    public async Task OneVariantIsPreselectedButNeverAddedWithoutConfirmation()
    {
        var commerce = new CommerceStub();
        var vm = new QuickPurchaseViewModel(new CatalogStub { Product = Product(4) with { Variants = [new(41, null, null, 100_000, 3, null)] } }, commerce, 4, true);
        await vm.LoadAsync();
        Assert.Equal(41, vm.SelectedVariant!.Id);
        Assert.True(vm.CanSubmit);
        Assert.Empty(commerce.Added);
        Assert.Equal("Mua ngay", vm.ActionTitle);
        Assert.True(await vm.SubmitAsync());
        Assert.Equal((41, 1), Assert.Single(commerce.Added));
        // Every commerce operation other than AddCartItem throws in the stub.
    }

    [Fact]
    public async Task EmptyOrSoldOutVariantsCannotBeAdded()
    {
        foreach (var variants in new IReadOnlyList<ProductVariantDto>[] { [], [new(11, "M", "Đỏ", 100_000, 0, null)] })
        {
            var commerce = new CommerceStub();
            var vm = new QuickPurchaseViewModel(new CatalogStub { Product = Product(1) with { Variants = variants } }, commerce, 1, false);
            await vm.LoadAsync();
            Assert.True(vm.IsOutOfStock);
            Assert.False(vm.CanSubmit);
            if (vm.Variants.Count > 0) vm.SelectedVariant = vm.Variants[0];
            Assert.False(await vm.SubmitAsync());
            Assert.Empty(commerce.Added);
            Assert.False(vm.HasSucceeded);
        }
    }

    [Fact]
    public async Task ForeignVariantCannotBeSubmittedAndQuantityRespectsStockAndBackendLimit()
    {
        var vm = new QuickPurchaseViewModel(new CatalogStub { Product = Product(1) with { Variants = [new(11, "M", "Đỏ", 100_000, 2_000, null), new(12, "L", "Đen", 250_000, 2, null)] } }, new CommerceStub(), 1, false);
        await vm.LoadAsync();
        vm.SelectedVariant = new ProductVariantOption(new(999, "M", "Đỏ", 1, 10, null));
        Assert.Null(vm.SelectedVariant);
        Assert.False(vm.CanSubmit);
        vm.SelectedVariant = vm.Variants[0];
        vm.Quantity = int.MaxValue;
        Assert.Equal(999, vm.Quantity);
        vm.SelectedVariant = vm.Variants[1];
        Assert.Equal(2, vm.Quantity);
        vm.Quantity = -3;
        Assert.Equal(1, vm.Quantity);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("5")]
    [InlineData("1000")]
    [InlineData("không hợp lệ")]
    public async Task InvalidTypedQuantityRemainsEditableAndNeverSendsCartMutation(string text)
    {
        var commerce = new CommerceStub();
        var vm = new QuickPurchaseViewModel(new CatalogStub(), commerce, 1, false);
        await vm.LoadAsync();
        vm.SelectedVariant = vm.Variants[0];
        vm.QuantityText = text;
        Assert.Equal(text, vm.QuantityText);
        Assert.True(vm.CanEditQuantity);
        Assert.False(vm.HasValidQuantity);
        Assert.True(vm.HasQuantityError);
        Assert.False(vm.CanSubmit);
        Assert.False(await vm.SubmitAsync());
        Assert.Equal(vm.QuantityValidationMessage, vm.ErrorMessage);
        Assert.Empty(commerce.Added);
        Assert.False(vm.HasSucceeded);
    }

    [Fact]
    public async Task BackspacingQuantityAllowsEmptyThenTypingTwoWithoutForcingOne()
    {
        var commerce = new CommerceStub();
        var vm = new QuickPurchaseViewModel(new CatalogStub(), commerce, 1, false);
        await vm.LoadAsync();
        vm.SelectedVariant = vm.Variants[0];
        vm.QuantityText = "";
        Assert.Equal("", vm.QuantityText);
        Assert.False(vm.CanSubmit);
        vm.QuantityText = "2";
        Assert.Equal("2", vm.QuantityText);
        Assert.Equal(2, vm.Quantity);
        Assert.True(vm.HasValidQuantity);
        Assert.False(vm.HasQuantityError);
        Assert.True(await vm.SubmitAsync());
        Assert.Equal((11, 2), Assert.Single(commerce.Added));
    }

    [Fact]
    public async Task ValidTypedQuantityRecoversFromInvalidInputWithoutChangingRequestedAmountSilently()
    {
        var commerce = new CommerceStub();
        var vm = new QuickPurchaseViewModel(new CatalogStub(), commerce, 1, false);
        await vm.LoadAsync();
        vm.SelectedVariant = vm.Variants[0];
        vm.QuantityText = "0";
        Assert.False(await vm.SubmitAsync());
        Assert.Empty(commerce.Added);
        vm.QuantityText = "3";
        Assert.False(vm.HasError);
        Assert.False(vm.HasQuantityError);
        Assert.True(await vm.SubmitAsync());
        Assert.Equal((11, 3), Assert.Single(commerce.Added));
    }

    [Fact]
    public async Task SteppersAndVariantChangesRestoreCanonicalQuantityText()
    {
        var vm = new QuickPurchaseViewModel(new CatalogStub(), new CommerceStub(), 1, false);
        await vm.LoadAsync();
        vm.SelectedVariant = vm.Variants[0];
        vm.QuantityText = "";
        vm.Quantity = 1;
        Assert.Equal("1", vm.QuantityText);
        Assert.True(vm.HasValidQuantity);
        vm.QuantityText = "4";
        vm.SelectedVariant = vm.Variants[1];
        Assert.Equal(2, vm.Quantity);
        Assert.Equal("2", vm.QuantityText);
        Assert.True(vm.HasValidQuantity);
    }

    [Fact]
    public async Task RepeatedSubmitDuringAndAfterSuccessSendsOneMutationAndFreezesPayload()
    {
        var pending = new TaskCompletionSource<CartDto>();
        var commerce = new CommerceStub { Add = (_, _, _) => pending.Task };
        var vm = new QuickPurchaseViewModel(new CatalogStub(), commerce, 1, true);
        await vm.LoadAsync();
        vm.SelectedVariant = vm.Variants[0];
        vm.Quantity = 3;
        var submitting = vm.SubmitAsync();
        Assert.True(vm.IsSubmitting);
        Assert.False(vm.CanClose);
        vm.SelectedVariant = vm.Variants[1];
        vm.Quantity = 1;
        Assert.False(await vm.SubmitAsync());
        Assert.Equal(11, vm.SelectedVariant!.Id);
        Assert.Equal(3, vm.Quantity);
        pending.SetResult(new([], 0, 0));
        Assert.True(await submitting);
        Assert.False(await vm.SubmitAsync());
        Assert.Equal((11, 3), Assert.Single(commerce.Added));
    }

    [Fact]
    public async Task FailedAddShowsErrorAndAllowsExplicitRetryWithoutReportingSuccess()
    {
        var commerce = new CommerceStub { Add = (_, _, _) => throw new CommerceException("Không thể thêm sản phẩm", statusCode: 503) };
        var vm = new QuickPurchaseViewModel(new CatalogStub(), commerce, 1, false);
        await vm.LoadAsync();
        vm.SelectedVariant = vm.Variants[0];
        Assert.False(await vm.SubmitAsync());
        Assert.False(vm.HasSucceeded);
        Assert.True(vm.HasError);
        Assert.True(vm.CanSubmit);
        Assert.Equal("Không thể thêm sản phẩm", vm.ErrorMessage);
        commerce.Add = (_, _, _) => Task.FromResult(new CartDto([], 0, 0));
        Assert.True(await vm.SubmitAsync());
        Assert.False(vm.HasError);
        Assert.Equal(2, commerce.Added.Count);
    }

    [Fact]
    public async Task StockConflictRefreshesInventoryWithoutAutomaticallyRepeatingAdd()
    {
        var catalog = new CatalogStub();
        var commerce = new CommerceStub { Add = (_, _, _) => throw new CommerceException("Tồn kho đã thay đổi", statusCode: 409) };
        var vm = new QuickPurchaseViewModel(catalog, commerce, 1, false);
        await vm.LoadAsync();
        vm.SelectedVariant = vm.Variants[0];
        vm.Quantity = 3;
        catalog.Product = Product(1) with { Variants = [new(11, "M", "Trắng", 150_000, 1, "white.jpg"), new(12, "L", "Đen", 250_000, 2, "black.jpg")] };
        Assert.False(await vm.SubmitAsync());
        Assert.Equal(1, vm.Quantity);
        Assert.Equal(11, vm.SelectedVariant!.Id);
        Assert.Equal(1, vm.Stock);
        Assert.Contains("Tồn kho", vm.ErrorMessage);
        Assert.False(vm.HasSucceeded);
        Assert.Single(commerce.Added);
        Assert.Equal(new[] { 1, 1 }, catalog.Requested);
    }

    [Fact]
    public async Task StockConflictErrorSurvivesUnchangedPickerAndQuantityBindingEchoes()
    {
        var catalog = new CatalogStub();
        var commerce = new CommerceStub { Add = (_, _, _) => throw new CommerceException("Tồn kho đã thay đổi", statusCode: 409) };
        var vm = new QuickPurchaseViewModel(catalog, commerce, 1, false);
        await vm.LoadAsync();
        vm.SelectedVariant = vm.Variants[0];
        vm.Quantity = 3;
        catalog.Product = Product(1) with { Variants = [new(11, "M", "Trắng", 150_000, 2, "white.jpg"), new(12, "L", "Đen", 250_000, 2, "black.jpg")] };
        Assert.False(await vm.SubmitAsync());
        var selected = vm.SelectedVariant;

        // MAUI ghi ngược giá trị hiện tại sau NotifyAll; đây không phải một chỉnh sửa của người mua.
        vm.SelectedVariant = selected;
        vm.Quantity = vm.Quantity;
        vm.QuantityText = vm.QuantityText;
        vm.Quantity = 999; // Cũng chuẩn hóa về số lượng hiện tại, không được xóa lỗi.

        Assert.Same(selected, vm.SelectedVariant);
        Assert.Equal(2, vm.Quantity);
        Assert.True(vm.HasError);
        Assert.Equal("Tồn kho đã thay đổi", vm.ErrorMessage);
        Assert.False(vm.HasSucceeded);
        Assert.Single(commerce.Added);
    }

    [Fact]
    public async Task TypedOneBindingEchoPreservesStockConflictUntilActualQuantityEdit()
    {
        var commerce = new CommerceStub { Add = (_, _, _) => throw new CommerceException("Tồn kho đã thay đổi", statusCode: 409) };
        var vm = new QuickPurchaseViewModel(new CatalogStub(), commerce, 1, false);
        await vm.LoadAsync();
        vm.SelectedVariant = vm.Variants[0];
        Assert.False(await vm.SubmitAsync());
        vm.QuantityText = "1";
        vm.Quantity = 1;
        vm.SelectedVariant = vm.SelectedVariant;
        Assert.True(vm.HasError);
        Assert.Equal("Tồn kho đã thay đổi", vm.ErrorMessage);
        Assert.False(vm.HasSucceeded);
        vm.QuantityText = "2";
        Assert.False(vm.HasError);
        Assert.True(vm.CanSubmit);
        Assert.Equal(2, vm.Quantity);
        Assert.Single(commerce.Added);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActualSelectionOrQuantityEditClearsStockConflictError(bool changeVariant)
    {
        var commerce = new CommerceStub { Add = (_, _, _) => throw new CommerceException("Tồn kho đã thay đổi", statusCode: 409) };
        var vm = new QuickPurchaseViewModel(new CatalogStub(), commerce, 1, false);
        await vm.LoadAsync();
        vm.SelectedVariant = vm.Variants[0];
        vm.Quantity = 2;
        Assert.False(await vm.SubmitAsync());
        Assert.True(vm.HasError);

        if (changeVariant) vm.SelectedVariant = vm.Variants[1];
        else vm.Quantity = 1;

        Assert.False(vm.HasError);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasSucceeded);
        Assert.Single(commerce.Added);
    }

    [Fact]
    public async Task LateCanceledAddDoesNotReportSuccessEvenWhenTransportIgnoresCancellation()
    {
        var pending = new TaskCompletionSource<CartDto>();
        var vm = new QuickPurchaseViewModel(new CatalogStub(), new CommerceStub { Add = (_, _, _) => pending.Task }, 1, false);
        await vm.LoadAsync();
        vm.SelectedVariant = vm.Variants[0];
        using var lifetime = new CancellationTokenSource();
        var submitting = vm.SubmitAsync(lifetime.Token);
        lifetime.Cancel();
        pending.SetResult(new([], 0, 0));
        Assert.False(await submitting);
        Assert.False(vm.HasSucceeded);
        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task CanceledLoadDoesNotApplyLateDataAndCanBeRetried()
    {
        var pending = new TaskCompletionSource<ProductDetailDto>();
        var catalog = new CatalogStub { Load = (_, _) => pending.Task };
        var vm = new QuickPurchaseViewModel(catalog, new CommerceStub(), 1, false);
        using var lifetime = new CancellationTokenSource();
        var loading = vm.LoadAsync(lifetime.Token);
        await vm.LoadAsync(lifetime.Token);
        Assert.Single(catalog.Requested);
        lifetime.Cancel();
        pending.SetResult(Product(1));
        await loading;
        Assert.False(vm.HasProduct);
        Assert.False(vm.IsBusy);
        Assert.False(vm.HasError);
        catalog.Load = null;
        await vm.LoadAsync();
        Assert.True(vm.HasProduct);
        Assert.Equal(2, catalog.Requested.Count);
    }

    [Fact]
    public async Task InvalidProductIdDoesNotCallCatalogOrCart()
    {
        var catalog = new CatalogStub();
        var commerce = new CommerceStub();
        var vm = new QuickPurchaseViewModel(catalog, commerce, 0, false);
        await vm.LoadAsync();
        Assert.True(vm.HasError);
        Assert.False(await vm.SubmitAsync());
        Assert.Empty(catalog.Requested);
        Assert.Empty(commerce.Added);
    }

    private static ProductDetailDto Product(int id) => new(id, "Áo thể thao", 3, "Áo", "chiếc", null, "product.jpg", 150_000, 250_000, true,
        [new(11, "M", "Trắng", 150_000, 4, "white.jpg"), new(12, "L", "Đen", 250_000, 2, "black.jpg")]);

    private sealed class CatalogStub : ICatalogApi
    {
        public ProductDetailDto Product { get; set; } = QuickPurchaseTests.Product(1);
        public List<int> Requested { get; } = [];
        public Func<int, CancellationToken, Task<ProductDetailDto>>? Load { get; set; }
        public Task<ProductDetailDto> GetProductAsync(int id, CancellationToken cancellationToken = default)
        {
            Requested.Add(id);
            return Load?.Invoke(id, cancellationToken) ?? Task.FromResult(Product);
        }
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, CancellationToken token)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductSummaryDto>> GetRelatedProductsAsync(int productId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Quick purchase must not load related products.");
    }

    private sealed class CommerceStub : ICommerceApi
    {
        public List<(int, int)> Added { get; } = [];
        public Func<int, int, CancellationToken, Task<CartDto>> Add { get; set; }
            = (_, _, _) => Task.FromResult(new CartDto([], 0, 0));
        public Task<CartDto> AddCartItemAsync(int id, int quantity, CancellationToken cancellationToken = default)
        {
            Added.Add((id, quantity));
            return Add(id, quantity, cancellationToken);
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
