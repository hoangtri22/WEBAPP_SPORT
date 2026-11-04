using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    // Điều hướng gốc: AuthViewModel.Entered mở HomePage, AuthViewModel.Left quay lại AuthPage.
    public App(IServiceProvider services, AuthViewModel auth)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
        _services = services;
        auth.Entered += () => Show(new NavigationPage(_services.GetRequiredService<HomePage>()));
        auth.Left += () => Show(_services.GetRequiredService<AuthPage>());
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(_services.GetRequiredService<AuthPage>());

    private void Show(Page page) => Windows[0].Page = page;
}
