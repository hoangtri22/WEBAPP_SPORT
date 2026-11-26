using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace SportShop.Mobile;

// Activity chính của app trên Android.
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // Mỗi nấc lăn chuột cuộn khoảng 3 dòng chữ, giống bước cuộn mặc định của Windows.
    private const int WheelStepDp = 72;

    // Thanh trạng thái nền xanh đậm (colors.xml) nên dùng icon sáng.
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (Window is { } window) WindowCompat.GetInsetsController(window, window.DecorView).AppearanceLightStatusBars = false;
    }

    // Lăn chuột: tự cuộn khung dưới con trỏ một đoạn WheelStepDp, vì NestedScrollView và RecyclerView biến một nấc lăn thành cú vuốt tới cuối trang.
    public override bool DispatchGenericMotionEvent(MotionEvent? e)
    {
        if (e is { Action: MotionEventActions.Scroll } && e.IsFromSource(InputSourceType.ClassPointer))
        {
            var wheel = e.GetAxisValue(Axis.Vscroll);
            var direction = wheel < 0 ? 1 : -1;
            if (wheel != 0 && FindScrollable(Window?.DecorView, (int)e.RawX, (int)e.RawY, direction) is { } target)
            {
                var distance = (int)(-wheel * WheelStepDp * (Resources?.DisplayMetrics?.Density ?? 1));
                if (target is AndroidX.RecyclerView.Widget.RecyclerView list) list.SmoothScrollBy(0, distance);
                else if (target is AndroidX.Core.Widget.NestedScrollView scroll) scroll.SmoothScrollBy(0, distance);
                else target.ScrollBy(0, distance);
                return true;
            }
        }
        return base.DispatchGenericMotionEvent(e);
    }

    // Tìm khung cuộn sâu nhất chứa điểm (x, y) trên màn hình và còn cuộn được theo hướng direction (1 xuống, -1 lên).
    private static Android.Views.View? FindScrollable(Android.Views.View? view, int x, int y, int direction)
    {
        if (view is null || view.Visibility != ViewStates.Visible) return null;
        var location = new int[2];
        view.GetLocationOnScreen(location);
        if (x < location[0] || y < location[1] || x >= location[0] + view.Width || y >= location[1] + view.Height) return null;
        if (view is ViewGroup group)
            for (var i = group.ChildCount - 1; i >= 0; i--)
                if (FindScrollable(group.GetChildAt(i), x, y, direction) is { } child) return child;
        return view.CanScrollVertically(direction) ? view : null;
    }
}
