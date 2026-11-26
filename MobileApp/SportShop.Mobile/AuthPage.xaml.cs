using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang đăng nhập, đăng ký giống Views/Account/Auth.cshtml, thêm Google và mua hàng không cần tài khoản; dùng AuthViewModel.
public partial class AuthPage : ContentPage
{
    private readonly AuthViewModel _viewModel;
    private readonly IServiceProvider _services;

    // Nhận AuthViewModel và bộ dịch vụ để mở trang quên mật khẩu.
    public AuthPage(AuthViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _services = services;
    }

    // Hiện hoặc ẩn mật khẩu như nút #pwdToggle của web.
    private void OnTogglePasswordClicked(object? sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        PasswordToggle.Source = PasswordEntry.IsPassword ? "ic_eye_muted.png" : "ic_eye_off_muted.png";
    }

    // Mở trang quên mật khẩu.
    private async void OnForgotPasswordClicked(object? sender, EventArgs e)
        => await Navigation.PushModalAsync(_services.GetRequiredService<ForgotPasswordPage>());

    // Màn hình ngoài cùng: lần mở app đầu tiên thử khôi phục phiên qua AuthViewModel.RestoreAsync().
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RestoreAsync();
    }
}
