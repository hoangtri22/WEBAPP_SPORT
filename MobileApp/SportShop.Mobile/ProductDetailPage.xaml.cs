using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang chi tiết transient; giỏ và checkout tiếp tục dùng các service/trang hiện có.
public partial class ProductDetailPage : ContentPage
{
    private readonly ProductDetailViewModel _vm;
    private readonly IServiceProvider _services;
    private CancellationTokenSource? _lifetime;
    private int _productId;
    private bool _navigating;

    public ProductDetailPage(ProductDetailViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _services = services;
    }

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

    protected override void OnDisappearing()
    {
        _lifetime?.Cancel();
        base.OnDisappearing();
    }

    private async void OnRetryClicked(object? sender, EventArgs e) => await LoadAsync(_productId);
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
            await Navigation.PushAsync(page);
            await page.LoadAsync(product.Id);
        }
        finally { _navigating = false; }
    }

    private void OnDecreaseClicked(object? sender, EventArgs e) => _vm.Quantity--;
    private void OnIncreaseClicked(object? sender, EventArgs e) => _vm.Quantity++;
    private async void OnAddClicked(object? sender, EventArgs e)
        => await _vm.AddToCartAsync(_lifetime?.Token ?? default);

    // Mua ngay thêm lựa chọn vào giỏ hiện hữu rồi mở thanh toán, giữ các món đã có trong giỏ.
    private async void OnBuyNowClicked(object? sender, EventArgs e)
    {
        if (_navigating) return;
        _navigating = true;
        try
        {
            if (await _vm.AddToCartAsync(_lifetime?.Token ?? default))
                await Navigation.PushAsync(_services.GetRequiredService<CheckoutPage>());
        }
        finally { _navigating = false; }
    }

    private async void OnCartClicked(object? sender, EventArgs e)
        => await Navigation.PushAsync(_services.GetRequiredService<CartPage>());

    private async void OnReviewsClicked(object? sender, EventArgs e)
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

    private async void OnSizeGuideClicked(object? sender, EventArgs e)
    {
        var page = _services.GetRequiredService<InfoPagesPage>();
        await Navigation.PushAsync(page);
        await page.LoadAsync("chon-size");
    }
}
