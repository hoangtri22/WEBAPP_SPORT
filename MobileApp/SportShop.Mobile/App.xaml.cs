using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Ứng dụng MAUI: giao diện sáng và điều hướng gốc giữa AuthPage và HomePage.
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
        // Rời phiên thì xóa cache để tài khoản sau không thấy đơn hàng của tài khoản trước.
        auth.Left += () =>
        {
            _services.GetRequiredService<AppMemoryCache>().Clear();
            Show(_services.GetRequiredService<AuthPage>());
        };
    }

    // Tạo cửa sổ đầu tiên với trang AuthPage.
    protected override Window CreateWindow(IActivationState? activationState) => new(_services.GetRequiredService<AuthPage>());

    // Phiên có thể hết hạn trên luồng nền (AuthorizedHandler), nên đổi trang luôn qua luồng giao diện.
    private void Show(Page page) => Dispatcher.Dispatch(() => Windows[0].Page = page);
}
