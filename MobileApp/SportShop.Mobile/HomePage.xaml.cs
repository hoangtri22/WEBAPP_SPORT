using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Tab Trang chủ giống Views/Home/Index.cshtml: banner, lợi ích, danh mục, sản phẩm mới, ưu đãi và hỗ trợ; mở các tab khác qua AppNavigation.
public partial class HomePage : ContentPage
{
    private readonly IServiceProvider _services;
    private readonly HomeViewModel _viewModel;
    private CancellationTokenSource? _lifetime;
    private bool _navigating;
    private (double X, double Y)? _returnScroll;
    private bool _isVisible;

    // Nhận bộ dịch vụ để mở trang và HomeViewModel.
    public HomePage(IServiceProvider services, HomeViewModel viewModel)
    {
        InitializeComponent();
        _services = services;
        BindingContext = _viewModel = viewModel;
    }

    // Tải dữ liệu thật khi mở trang lần đầu; không ghi đè nội dung bằng dữ liệu mẫu khi mất mạng.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isVisible = true;
        _lifetime?.Dispose();
        _lifetime = new CancellationTokenSource();
        if (!_viewModel.HasLoaded) await _viewModel.LoadAsync(_lifetime.Token);
        await RestoreHomeScrollAsync();
    }

    // Rời Trang chủ thì hủy request đang chạy.
    protected override void OnDisappearing()
    {
        _isVisible = false;
        _lifetime?.Cancel();
        base.OnDisappearing();
    }

    // Tải lại danh mục và sản phẩm qua HomeViewModel.LoadAsync().
    private async void OnRefreshClicked(object? sender, EventArgs e)
        => await _viewModel.LoadAsync(_lifetime?.Token ?? default);

    // Chuyển sang tab Sản phẩm, giữ nguyên bộ lọc đang dùng.
    private async void OnCatalogClicked(object? sender, EventArgs e)
        => await NavigateAsync(() => AppNavigation.OpenCatalogAsync(Navigation, _services));

    // Ô tìm kiếm: chuyển sang tab Sản phẩm và đặt con trỏ vào ô tìm như nút searchToggle của web.
    private async void OnSearchTapped(object? sender, TappedEventArgs e)
        => await NavigateAsync(async () =>
        {
            await AppNavigation.OpenCatalogAsync(Navigation, _services);
            _services.GetRequiredService<CatalogViewModel>().RequestSearchFocus();
        });

    // Mở danh sách sản phẩm của danh mục đã bấm qua CatalogViewModel.OpenCategoryAsync().
    private async void OnCategoryTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not int categoryId) return;
        await NavigateAsync(async () =>
        {
            await AppNavigation.OpenCatalogAsync(Navigation, _services);
            await _services.GetRequiredService<CatalogViewModel>().OpenCategoryAsync(categoryId);
        });
    }

    // Mở chi tiết sản phẩm và nhớ vị trí cuộn để quay về đúng chỗ.
    private async void OnProductTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not int productId) return;
        await NavigateAsync(async () =>
        {
            var page = _services.GetRequiredService<ProductDetailPage>();
            _returnScroll = (HomeScroll.ScrollX, HomeScroll.ScrollY);
            page.SetContinueShopping(async () =>
            {
                if (await AppNavigation.ReturnToSourceAsync(Navigation, this)) await RestoreHomeScrollAsync();
                else await AppNavigation.OpenDefaultCatalogAsync(Navigation, _services);
            });
            await Navigation.PushAsync(page);
            await page.LoadAsync(productId);
        });
    }

    private async void OnHomeScrollReady(object? sender, EventArgs e) => await RestoreHomeScrollAsync();

    // Cuộn về vị trí đã nhớ một lần, chỉ khi trang đang hiện và đã có kích thước.
    private async Task RestoreHomeScrollAsync()
    {
        if (!_isVisible || _returnScroll is not { } point || HomeScroll.Height <= 0 || HomeScroll.Content.Height <= 0) return;
        _returnScroll = null;
        await HomeScroll.ScrollToAsync(point.X, point.Y, animated: false);
    }

    // Mở trang khuyến mãi (Views/Shop/Promotions.cshtml).
    private async void OnPromotionsClicked(object? sender, EventArgs e)
        => await NavigateAsync(() => Navigation.PushAsync(_services.GetRequiredService<PromotionsPage>()));

    // Mở trang thông tin và hỗ trợ (Views/Info/Page.cshtml).
    private async void OnInfoClicked(object? sender, EventArgs e)
        => await NavigateAsync(async () =>
        {
            var page = _services.GetRequiredService<InfoPagesPage>();
            await Navigation.PushAsync(page);
            await page.LoadAsync();
        });

    // Chặn chạm liên tiếp mở trùng trang trong lúc chuyển màn hình.
    private async Task NavigateAsync(Func<Task> navigate)
    {
        if (_navigating) return;
        _navigating = true;
        try { await navigate(); }
        finally { _navigating = false; }
    }
}
