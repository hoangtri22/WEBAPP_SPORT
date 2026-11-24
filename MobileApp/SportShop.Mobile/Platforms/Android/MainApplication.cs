using Android.App;
using Android.Runtime;

namespace SportShop.Mobile;

#if DEBUG
[Application(NetworkSecurityConfig = "@xml/network_security_config")]
#else
[Application]
#endif
// Ứng dụng Android; bản Debug cho HTTP tới localhost theo network_security_config.xml.
public class MainApplication : MauiApplication
{
	// Constructor bắt buộc của Android.
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
