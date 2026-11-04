using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace SportShop.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // Thanh trạng thái nền xanh đậm (colors.xml) nên dùng icon sáng.
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (Window is { } window) WindowCompat.GetInsetsController(window, window.DecorView).AppearanceLightStatusBars = false;
    }
}
