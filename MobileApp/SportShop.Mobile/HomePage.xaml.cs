using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class HomePage : ContentPage
{
    private readonly AuthViewModel _auth;
    private readonly MainPage _catalog;

    public HomePage(AuthViewModel auth, MainPage catalog)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _auth = auth;
        _catalog = catalog;
    }

    // Mở trang sản phẩm có sẵn (MainPage); các nút khác của Trang chủ chỉ là giao diện.
    private async void OnCatalogClicked(object? sender, EventArgs e)
    {
        if (_catalog.Parent is null) await Navigation.PushAsync(_catalog);
    }

    // Tab Tài khoản: hỏi rồi gọi AuthViewModel.LeaveAsync() để đăng xuất hoặc rời chế độ khách.
    private async void OnAccountClicked(object? sender, EventArgs e)
    {
        var action = _auth.IsSignedIn ? "Đăng xuất" : "Đăng nhập / Đăng ký";
        if (await DisplayActionSheetAsync("Tài khoản", "Đóng", null, action) == action)
            await _auth.LeaveAsync();
    }
}
