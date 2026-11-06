using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class AuthPage : ContentPage
{
    private readonly AuthViewModel _viewModel;
    private readonly IServiceProvider _services;

    public AuthPage(AuthViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _services = services;
    }

    private async void OnForgotPasswordClicked(object? sender, EventArgs e)
        => await Navigation.PushModalAsync(_services.GetRequiredService<ForgotPasswordPage>());

    // Màn hình ngoài cùng: lần mở app đầu tiên thử khôi phục phiên qua AuthViewModel.RestoreAsync().
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RestoreAsync();
    }
}
