using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang chủ của app; chỉ nút sản phẩm, giỏ hàng và tài khoản có điều hướng.
public partial class HomePage : ContentPage
{
    private readonly AuthViewModel _auth;
    private readonly MainPage _catalog;
    private readonly CartPage _cartPage;
    private readonly IServiceProvider _services;

    // Nhận AuthViewModel, các trang liên quan và bộ dịch vụ.
    public HomePage(AuthViewModel auth, MainPage catalog, CartPage cartPage, IServiceProvider services)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _auth = auth;
        _catalog = catalog;
        _cartPage = cartPage;
        _services = services;
    }

    // Mở trang sản phẩm có sẵn (MainPage); các nút khác của Trang chủ chỉ là giao diện.
    private async void OnCatalogClicked(object? sender, EventArgs e)
    {
        if (_catalog.Parent is null) await Navigation.PushAsync(_catalog);
    }

    // Tab Giỏ hàng: Mở trang Giỏ hàng & Đơn hàng (phần của Phan Văn Thiện).
    private async void OnCartClicked(object? sender, EventArgs e)
    {
        if (_cartPage.Parent is null) await Navigation.PushAsync(_cartPage);
    }

    // Tab Tài khoản: đã đăng nhập thì mở hồ sơ; khách được mời đăng nhập hoặc đăng ký.
    private async void OnAccountClicked(object? sender, EventArgs e)
    {
        if (_auth.IsSignedIn) await Navigation.PushAsync(_services.GetRequiredService<ProfilePage>());
        else if (await DisplayActionSheetAsync("Tài khoản", "Đóng", null, "Đăng nhập / Đăng ký") == "Đăng nhập / Đăng ký")
            await _auth.LeaveAsync();
    }
}
