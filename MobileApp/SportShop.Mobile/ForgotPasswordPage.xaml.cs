using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang quên mật khẩu, dùng ForgotPasswordViewModel.
public partial class ForgotPasswordPage : ContentPage
{
    // Gắn ForgotPasswordViewModel và sự kiện hoàn tất.
    public ForgotPasswordPage(ForgotPasswordViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.Completed += OnCompleted;
    }

    // Báo đặt lại mật khẩu thành công rồi đóng trang.
    private async void OnCompleted()
    {
        await DisplayAlertAsync("Đã đặt lại mật khẩu", "Bạn có thể đăng nhập bằng mật khẩu mới.", "Đăng nhập");
        await Navigation.PopModalAsync();
    }

    // Đóng trang quên mật khẩu.
    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
}
