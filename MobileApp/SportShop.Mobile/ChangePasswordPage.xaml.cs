using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage(ChangePasswordViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
