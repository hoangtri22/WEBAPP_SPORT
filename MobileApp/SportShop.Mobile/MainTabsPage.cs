using System.ComponentModel;
using AndroidPlatform = Microsoft.Maui.Controls.PlatformConfiguration.Android;
using AndroidTabs = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage;
using ToolbarPlacement = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.ToolbarPlacement;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Các tab của thanh điều hướng đáy, theo thứ tự hiển thị.
public enum AppTab { Home, Catalog, Cart, Orders, Account }

// Khung chính sau khi vào app: thanh tab đáy với năm mục, mỗi tab giữ ngăn xếp trang riêng; tab Giỏ hàng có số lượng lấy từ CartBadgeState.
public sealed class MainTabsPage : Microsoft.Maui.Controls.TabbedPage
{
    private readonly CartBadgeState _badge;

    // Dựng năm tab từ các trang đã đăng ký trong MauiProgram; tắt vuốt ngang để không lẫn với cuộn danh sách.
    public MainTabsPage(IServiceProvider services, CartBadgeState badge)
    {
        _badge = badge;
        AndroidTabs.SetToolbarPlacement(On<AndroidPlatform>(), ToolbarPlacement.Bottom);
        AndroidTabs.SetIsSwipePagingEnabled(On<AndroidPlatform>(), false);
        AndroidTabs.SetIsSmoothScrollEnabled(On<AndroidPlatform>(), false);
        Children.Add(Tab(services.GetRequiredService<HomePage>(), "Trang chủ", "ic_house_ink.png"));
        Children.Add(Tab(services.GetRequiredService<MainPage>(), "Sản phẩm", "ic_layout_grid_ink.png"));
        Children.Add(Tab(services.GetRequiredService<CartPage>(), "Giỏ hàng", "ic_shopping_bag_ink.png"));
        Children.Add(Tab(services.GetRequiredService<OrdersPage>(), "Đơn hàng", "ic_package_ink.png"));
        Children.Add(Tab(services.GetRequiredService<AccountPage>(), "Tài khoản", "ic_user_ink.png"));
        _badge.PropertyChanged += OnBadgeChanged;
        HandlerChanged += (_, _) => Dispatcher.Dispatch(ShowCartBadge);
        CurrentPageChanged += (_, _) => Dispatcher.Dispatch(ShowCartBadge);
    }

    // Khung tab đang hiện trong cửa sổ, null khi đang ở màn hình đăng nhập.
    public static MainTabsPage? Current => Application.Current?.Windows.FirstOrDefault()?.Page as MainTabsPage;

    // Chuyển sang một tab; popToRoot = true thì đưa tab đó về trang gốc (ví dụ Tiếp tục mua sắm về danh sách sản phẩm).
    public async Task SelectAsync(AppTab tab, bool popToRoot = true)
    {
        if (Children[(int)tab] is not NavigationPage navigation) return;
        CurrentPage = navigation;
        if (popToRoot && navigation.Navigation.NavigationStack.Count > 1) await navigation.PopToRootAsync(false);
    }

    // Bọc trang gốc trong NavigationPage để mỗi tab có thanh tiêu đề và nút quay lại riêng.
    private static NavigationPage Tab(Page root, string title, string icon)
    {
        var page = new NavigationPage(root) { Title = title, IconImageSource = icon };
        return page;
    }

    private void OnBadgeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CartBadgeState.Count)) Dispatcher.Dispatch(ShowCartBadge);
    }

    // Hiện số lượng giỏ hàng trên tab Giỏ hàng bằng BadgeDrawable của BottomNavigationView (Android), giống .cart-count của web.
    private void ShowCartBadge()
    {
#if ANDROID
        if (FindBottomBar(Platform.CurrentActivity?.Window?.DecorView) is not { } bar) return;
        var itemId = bar.Menu.Size() > (int)AppTab.Cart ? bar.Menu.GetItem((int)AppTab.Cart)!.ItemId : (int)AppTab.Cart;
        if (_badge.Count <= 0) { bar.RemoveBadge(itemId); return; }
        var badge = bar.GetOrCreateBadge(itemId);
        badge.BackgroundColor = Android.Graphics.Color.ParseColor("#F97316");
        badge.BadgeTextColor = Android.Graphics.Color.White;
        badge.MaxCharacterCount = 3;
        badge.Number = _badge.Count;
        badge.SetVisible(true);
#endif
    }

#if ANDROID
    // Tìm BottomNavigationView mà TabbedPage tạo ra trong cây view của Activity.
    private static Google.Android.Material.BottomNavigation.BottomNavigationView? FindBottomBar(Android.Views.View? view)
    {
        if (view is Google.Android.Material.BottomNavigation.BottomNavigationView bar) return bar;
        if (view is not Android.Views.ViewGroup group) return null;
        for (var i = 0; i < group.ChildCount; i++)
            if (FindBottomBar(group.GetChildAt(i)) is { } found) return found;
        return null;
    }
#endif
}
