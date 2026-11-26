using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Chi tiết sản phẩm giống Views/Shop/Detail.cshtml: chọn biến thể, số lượng, thêm giỏ hoặc mua ngay; trang transient, giỏ và checkout dùng các trang hiện có.
public partial class ProductDetailPage : ContentPage
{
    private readonly ProductDetailViewModel _vm;
    private readonly IServiceProvider _services;
    private CancellationTokenSource? _lifetime;
    private int _productId;
    private bool _navigating;
    private Func<Task>? _continueShopping;

    // Gắn ProductDetailViewModel vào trang.
    public ProductDetailPage(ProductDetailViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _services = services;
    }

    // Danh sách nguồn giữ chính trang, dữ liệu và vị trí cuộn của nó trong phiên điều hướng.
    public void SetContinueShopping(Func<Task> callback) => _continueShopping = callback;

    // Gọi sau PushAsync để tải đúng sản phẩm với vòng đời màn hình.
    public async Task LoadAsync(int productId)
    {
        _productId = productId;
        await _vm.LoadAsync(productId, _lifetime?.Token ?? default);
    }

    // Quay lại từ giỏ/đánh giá thì cập nhật tồn kho và giữ biến thể đang chọn.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _lifetime?.Dispose();
        _lifetime = new CancellationTokenSource();
        if (_productId > 0) await _vm.LoadAsync(_productId, _lifetime.Token);
    }

    // Rời trang thì ẩn thông báo đã thêm và hủy request đang chạy.
    protected override void OnDisappearing()
    {
        AddSuccessOverlay.IsVisible = false;
        _lifetime?.Cancel();
        base.OnDisappearing();
    }

    // Nút Back đóng thông báo đã thêm trước, sau đó mới rời trang.
    protected override bool OnBackButtonPressed()
    {
        if (!AddSuccessOverlay.IsVisible) return base.OnBackButtonPressed();
        AddSuccessOverlay.IsVisible = false;
        return true;
    }

    private async void OnRetryClicked(object? sender, EventArgs e) => await LoadAsync(_productId);
    // Tải lại riêng danh sách sản phẩm cùng loại.
    private async void OnRetryRelatedClicked(object? sender, EventArgs e)
        => await _vm.LoadRelatedAsync(_lifetime?.Token ?? default);

    // Mỗi gợi ý mở một trang transient với đúng ID; Back giữ trang và lựa chọn trước đó.
    private async void OnRelatedProductTapped(object? sender, TappedEventArgs e)
    {
        if (_navigating || sender is not BindableObject { BindingContext: ProductCard product }
                || product.Id == _vm.ProductId) return;
        _navigating = true;
        try
        {
            var page = _services.GetRequiredService<ProductDetailPage>();
            if (_continueShopping is not null) page.SetContinueShopping(_continueShopping);
            await Navigation.PushAsync(page);
            await page.LoadAsync(product.Id);
        }
        finally { _navigating = false; }
    }

    // Bấm nút biến thể: chọn biến thể còn hàng, biến thể hết hàng không chọn được như nút disabled của web.
    private void OnVariantTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is ProductVariantOption { IsAvailable: true } option && _vm.IsIdle) _vm.SelectedVariant = option;
    }

    // Chạm vùng tối quanh thông báo đã thêm thì đóng thông báo.
    private void OnOverlayTapped(object? sender, TappedEventArgs e) => AddSuccessOverlay.IsVisible = false;

    private void OnDecreaseClicked(object? sender, EventArgs e) => _vm.Quantity--;
    private void OnIncreaseClicked(object? sender, EventArgs e) => _vm.Quantity++;
    // Thêm biến thể đang chọn vào giỏ, thành công thì hiện thông báo; _navigating chặn bấm nhiều lần.
    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (_navigating || !_vm.CanBuy) return;
        _navigating = true;
        AddSuccessOverlay.IsVisible = false;
        try
        {
            if (await _vm.AddToCartAsync(_lifetime?.Token ?? default)
                && Navigation.NavigationStack.LastOrDefault() == this)
                AddSuccessOverlay.IsVisible = true;
        }
        finally { _navigating = false; }
    }

    // Đóng thông báo đã thêm vào giỏ.
    private void OnDismissAddedClicked(object? sender, EventArgs e)
        => AddSuccessOverlay.IsVisible = false;

    // Quay về danh sách nguồn, không có thì mở danh sách mặc định.
    private async void OnContinueShoppingClicked(object? sender, EventArgs e)
    {
        if (_navigating) return;
        _navigating = true;
        AddSuccessOverlay.IsVisible = false;
        try
        {
            if (_continueShopping is not null) await _continueShopping();
            else await AppNavigation.OpenDefaultCatalogAsync(Navigation, _services);
        }
        finally { _navigating = false; }
    }

    // Mua ngay thêm lựa chọn vào giỏ hiện hữu rồi mở thanh toán, giữ các món đã có trong giỏ.
    private async void OnBuyNowClicked(object? sender, EventArgs e)
    {
        if (_navigating || !_vm.CanBuy) return;
        _navigating = true;
        try
        {
            if (await _vm.AddToCartAsync(_lifetime?.Token ?? default))
                await Navigation.PushAsync(_services.GetRequiredService<CheckoutPage>());
        }
        finally { _navigating = false; }
    }

    // Mở giỏ hàng: chuyển sang tab Giỏ hàng, ngoài khung tab thì mở CartPage.
    private async void OnCartClicked(object? sender, EventArgs e)
    {
        if (_navigating || _vm.IsBusy) return;
        _navigating = true;
        AddSuccessOverlay.IsVisible = false;
        try
        {
            if (!await AppNavigation.OpenTabAsync(AppTab.Cart))
                await Navigation.PushAsync(_services.GetRequiredService<CartPage>());
        }
        finally { _navigating = false; }
    }

    // Mở trang đánh giá của sản phẩm.
    private async void OnReviewsTapped(object? sender, TappedEventArgs e)
    {
        if (_navigating || _vm.ProductId <= 0) return;
        _navigating = true;
        try
        {
            var page = _services.GetRequiredService<ReviewsPage>();
            await Navigation.PushAsync(page);
            await page.LoadAsync(_vm.ProductId, _vm.Name);
        }
        finally { _navigating = false; }
    }

    // Mở hướng dẫn chọn size (slug chon-size).
    private async void OnSizeGuideClicked(object? sender, EventArgs e)
    {
        var page = _services.GetRequiredService<InfoPagesPage>();
        await Navigation.PushAsync(page);
        await page.LoadAsync("chon-size");
    }
}
