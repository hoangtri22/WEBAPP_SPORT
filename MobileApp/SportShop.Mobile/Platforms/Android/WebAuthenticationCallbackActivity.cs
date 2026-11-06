using Android.App;
using Android.Content;
using Android.Content.PM;

namespace SportShop.Mobile;

// Nhận link sportshop://auth từ API sau khi đăng nhập Google rồi trả về WebAuthenticator.
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "sportshop", DataHost = "auth")]
public sealed class WebAuthenticationCallbackActivity : WebAuthenticatorCallbackActivity;
