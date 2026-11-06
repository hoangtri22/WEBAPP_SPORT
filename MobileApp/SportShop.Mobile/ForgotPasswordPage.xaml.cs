using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class ForgotPasswordPage : ContentPage
{
    public ForgotPasswordPage(ForgotPasswordViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.Completed += OnCompleted;
    }

    private async void OnCompleted()
    {
        await DisplayAlertAsync("Đã đặt lại mật khẩu", "Bạn có thể đăng nhập bằng mật khẩu mới.", "Đăng nhập");
        await Navigation.PopModalAsync();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
}
