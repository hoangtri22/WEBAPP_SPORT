using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Khởi tạo app MAUI và đăng ký dịch vụ, ViewModel, trang.
public static class MauiProgram
{
    // Cấu hình app: font, HttpClient tới ApiSettings.BaseUri, đăng ký service, ViewModel và các trang.
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });
#if ANDROID
        // Bỏ gạch chân mặc định của Android; ô nhập đã có khung Border riêng.
        EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif
#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.Services.AddSingleton(_ => new HttpClient
        {
            BaseAddress = ApiSettings.BaseUri,
            Timeout = TimeSpan.FromSeconds(20)
        });
        builder.Services.AddSingleton<AppMemoryCache>();
        builder.Services.AddSingleton<CartBadgeState>();
        builder.Services.AddSingleton<ICatalogApi, CatalogApi>();
        builder.Services.AddSingleton<IAuthApi, AuthApi>();
        builder.Services.AddSingleton<ISessionStore, SessionStore>();
        builder.Services.AddSingleton<ISessionManager, SessionManager>();
        // Client riêng cho API cần đăng nhập; AuthApi giữ client thường để refresh không đi vòng qua AuthorizedHandler.
        builder.Services.AddSingleton<IAccountApi>(services => new AccountApi(new HttpClient(
            new AuthorizedHandler(services.GetRequiredService<ISessionManager>()) { InnerHandler = new HttpClientHandler() })
        {
            BaseAddress = ApiSettings.BaseUri,
            Timeout = TimeSpan.FromSeconds(20)
        }));
        builder.Services.AddSingleton<IDeviceIdentity, DeviceIdentity>();
        builder.Services.AddSingleton<ICommerceApi, CommerceApi>();
        builder.Services.AddSingleton<IContentApi>(services => new ContentApi(
            services.GetRequiredService<HttpClient>(),
            new HttpClient(new AuthorizedHandler(services.GetRequiredService<ISessionManager>())
            { InnerHandler = new HttpClientHandler() })
            {
                BaseAddress = ApiSettings.BaseUri,
                Timeout = TimeSpan.FromSeconds(20)
            }));

        builder.Services.AddSingleton<AuthViewModel>();
        builder.Services.AddSingleton<CatalogViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<ProductDetailViewModel>();
        builder.Services.AddTransient<PromotionsViewModel>();
        builder.Services.AddTransient<InfoPagesViewModel>();
        builder.Services.AddTransient<ReviewsViewModel>();
        builder.Services.AddSingleton<CartViewModel>();
        builder.Services.AddSingleton<OrdersViewModel>();

        builder.Services.AddTransient<AuthPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ProductDetailPage>();
        builder.Services.AddTransient<PromotionsPage>();
        builder.Services.AddTransient<InfoPagesPage>();
        builder.Services.AddTransient<ReviewsPage>();
        builder.Services.AddTransient<CartPage>();
        builder.Services.AddTransient<CheckoutPage>();
        builder.Services.AddTransient<OrdersPage>();
        builder.Services.AddSingleton<IPasswordResetApi, PasswordResetApi>();
        builder.Services.AddSingleton<IGoogleSignIn, GoogleSignIn>();
        builder.Services.AddTransient<ForgotPasswordViewModel>();
        builder.Services.AddTransient<ForgotPasswordPage>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<ChangePasswordViewModel>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<ChangePasswordPage>();
        return builder.Build();
    }
}
