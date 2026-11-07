using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang đổi mật khẩu, dùng ChangePasswordViewModel.
public partial class ChangePasswordPage : ContentPage
{
    // Gắn ChangePasswordViewModel vào trang.
    public ChangePasswordPage(ChangePasswordViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
