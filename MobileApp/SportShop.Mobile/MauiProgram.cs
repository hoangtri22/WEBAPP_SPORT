using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

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
        builder.Services.AddSingleton<ICatalogApi, CatalogApi>();
        builder.Services.AddSingleton<IAuthApi, AuthApi>();
        builder.Services.AddSingleton<ISessionStore, SessionStore>();
        builder.Services.AddSingleton<IDeviceIdentity, DeviceIdentity>();
        builder.Services.AddSingleton<AuthViewModel>();
        builder.Services.AddTransient<AuthPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddSingleton<CatalogViewModel>();
        builder.Services.AddSingleton<MainPage>();
        return builder.Build();
    }
}
