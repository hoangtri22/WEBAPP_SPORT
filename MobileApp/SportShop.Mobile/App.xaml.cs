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

    // Sự kiện vòng đời cửa sổ ứng dụng (Foreground / Background) để các màn hình tạm dừng hoặc tiếp tục tác vụ định kỳ.
    public static event Action? AppResumed;
    public static event Action? AppStopped;

    // Tạo cửa sổ đầu tiên với trang AuthPage và gắn theo dõi vòng đời ứng dụng.
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<AuthPage>());
        window.Resumed += (_, _) => AppResumed?.Invoke();
        window.Stopped += (_, _) => AppStopped?.Invoke();
        return window;
    }

    // Phiên có thể hết hạn trên luồng nền (AuthorizedHandler), nên đổi trang luôn qua luồng giao diện.
    private void Show(Page page) => Dispatcher.Dispatch(() => Windows[0].Page = page);
}
