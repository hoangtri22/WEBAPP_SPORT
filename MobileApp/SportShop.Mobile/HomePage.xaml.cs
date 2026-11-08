using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang chủ nối catalog, khuyến mãi, thông tin và các chức năng mua hàng hiện có.
public partial class HomePage : ContentPage
{
    private readonly AuthViewModel _auth;
    private readonly MainPage _catalog;
    private readonly CartPage _cartPage;
    private readonly IServiceProvider _services;
    private readonly HomeViewModel _viewModel;
    private CancellationTokenSource? _lifetime;
    private bool _navigating;

    // Nhận AuthViewModel, các trang liên quan và bộ dịch vụ.
    public HomePage(AuthViewModel auth, MainPage catalog, CartPage cartPage, IServiceProvider services, HomeViewModel viewModel)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _auth = auth;
        _catalog = catalog;
        _cartPage = cartPage;
        _services = services;
        BindingContext = _viewModel = viewModel;
    }

    // Tải dữ liệu thật khi mở trang; không ghi đè nội dung bằng dữ liệu mẫu khi mất mạng.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _lifetime?.Dispose();
        _lifetime = new CancellationTokenSource();
        if (!_viewModel.HasLoaded) await _viewModel.LoadAsync(_lifetime.Token);
    }

    protected override void OnDisappearing()
    {
        _lifetime?.Cancel();
        base.OnDisappearing();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
        => await _viewModel.LoadAsync(_lifetime?.Token ?? default);

    // Mở trang cửa hàng, giữ nguyên bộ lọc khi trở lại từ chi tiết.
    private async void OnCatalogClicked(object? sender, EventArgs e)
    {
        await NavigateAsync(async () => { if (_catalog.Parent is null) await Navigation.PushAsync(_catalog); });
    }

    private async void OnCategoryClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: int categoryId }) return;
        await NavigateAsync(async () =>
        {
            await _catalog.OpenCategoryAsync(categoryId);
            if (_catalog.Parent is null) await Navigation.PushAsync(_catalog);
        });
    }

    private async void OnProductTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not int productId) return;
        await NavigateAsync(async () =>
        {
            var page = _services.GetRequiredService<ProductDetailPage>();
            await Navigation.PushAsync(page);
            await page.LoadAsync(productId);
        });
    }

    private async void OnPromotionsClicked(object? sender, EventArgs e)
        => await NavigateAsync(() => Navigation.PushAsync(_services.GetRequiredService<PromotionsPage>()));

    private async void OnInfoClicked(object? sender, EventArgs e)
        => await NavigateAsync(async () =>
        {
            var page = _services.GetRequiredService<InfoPagesPage>();
            await Navigation.PushAsync(page);
            await page.LoadAsync();
        });

    // Tab Giỏ hàng: Mở trang Giỏ hàng & Đơn hàng (phần của Phan Văn Thiện).
    private async void OnCartClicked(object? sender, EventArgs e)
    {
        await NavigateAsync(async () => { if (_cartPage.Parent is null) await Navigation.PushAsync(_cartPage); });
    }

    // Tab Tài khoản: đã đăng nhập thì mở hồ sơ; khách được mời đăng nhập hoặc đăng ký.
    private async void OnAccountClicked(object? sender, EventArgs e)
    {
        await NavigateAsync(async () =>
        {
            if (_auth.IsSignedIn) await Navigation.PushAsync(_services.GetRequiredService<ProfilePage>());
            else if (await DisplayActionSheetAsync("Tài khoản", "Đóng", null, "Đăng nhập / Đăng ký") == "Đăng nhập / Đăng ký")
                await _auth.LeaveAsync();
        });
    }

    // Chặn chạm liên tiếp mở trùng trang trong lúc chuyển màn hình.
    private async Task NavigateAsync(Func<Task> navigate)
    {
        if (_navigating) return;
        _navigating = true;
        try { await navigate(); }
        finally { _navigating = false; }
    }
}
