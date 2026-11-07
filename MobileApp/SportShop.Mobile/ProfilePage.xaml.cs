using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang tài khoản, dùng ProfileViewModel.
public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;
    private readonly AuthViewModel _auth;
    private readonly IServiceProvider _services;

    // Nhận ProfileViewModel, AuthViewModel và bộ dịch vụ.
    public ProfilePage(ProfileViewModel vm, AuthViewModel auth, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _auth = auth;
        _services = services;
    }

    // Tải hồ sơ khi mở trang lần đầu.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_vm.IsLoaded) await _vm.LoadAsync();
    }

    // Mở trang đổi mật khẩu.
    private async void OnChangePasswordClicked(object? sender, EventArgs e)
        => await Navigation.PushAsync(_services.GetRequiredService<ChangePasswordPage>());

    // Hỏi xác nhận rồi đăng xuất qua AuthViewModel.LeaveAsync().
    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        if (await DisplayAlertAsync("Đăng xuất", "Bạn muốn đăng xuất khỏi tài khoản này?", "Đăng xuất", "Hủy"))
            await _auth.LeaveAsync();
    }
}
