using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class AuthPage : ContentPage
{
    private readonly AuthViewModel _viewModel;

    public AuthPage(AuthViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Màn hình ngoài cùng: lần mở app đầu tiên thử khôi phục phiên qua AuthViewModel.RestoreAsync().
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RestoreAsync();
    }
}
