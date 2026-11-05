using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class HomePage : ContentPage
{
    private readonly AuthViewModel _auth;
    private readonly MainPage _catalog;
    private readonly CartPage _cartPage;

    public HomePage(AuthViewModel auth, MainPage catalog, CartPage cartPage)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _auth = auth;
        _catalog = catalog;
        _cartPage = cartPage;
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

    // Tab Tài khoản: hỏi rồi gọi AuthViewModel.LeaveAsync() để đăng xuất hoặc rời chế độ khách.
    private async void OnAccountClicked(object? sender, EventArgs e)
    {
        var action = _auth.IsSignedIn ? "Đăng xuất" : "Đăng nhập / Đăng ký";
        if (await DisplayActionSheetAsync("Tài khoản", "Đóng", null, action) == action)
            await _auth.LeaveAsync();
    }
}
