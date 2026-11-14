using System.Runtime.CompilerServices;

namespace SportShop.Mobile;

// Tìm catalog trong phiên điều hướng hiện tại, giữ bộ lọc và tránh chồng thêm cùng một màn hình.
public static class AppNavigation
{
    private static readonly ConditionalWeakTable<INavigation, CatalogNavigationState> CatalogStates = new();

    public static async Task OpenDefaultCatalogAsync(INavigation navigation, IServiceProvider services)
    {
        await services.GetRequiredService<SportShop.Mobile.ViewModels.CatalogViewModel>().ResetAsync();
        await OpenCatalogAsync(navigation, services);
    }

    // Quay về chính danh sách đã mở chi tiết, kể cả khi đang xem sản phẩm liên quan hoặc giỏ.
    public static async Task<bool> ReturnToSourceAsync(INavigation navigation, Page source)
    {
        var pages = navigation.NavigationStack.ToArray();
        var sourceIndex = Array.IndexOf(pages, source);
        if (sourceIndex < 0) return false;
        if (sourceIndex == pages.Length - 1) return true;
        for (var index = pages.Length - 2; index > sourceIndex; index--)
            navigation.RemovePage(pages[index]);
        await navigation.PopAsync();
        return true;
    }

    public static async Task OpenCatalogAsync(INavigation navigation, IServiceProvider services)
    {
        var state = CatalogStates.GetValue(navigation, static _ => new CatalogNavigationState());
        if (Interlocked.CompareExchange(ref state.IsOpening, 1, 0) != 0) return;
        try
        {
            var pages = navigation.NavigationStack.ToArray();
            var catalogIndex = Array.FindLastIndex(pages, page => page is MainPage);
            if (catalogIndex < 0)
            {
                await navigation.PushAsync(services.GetRequiredService<MainPage>());
                return;
            }
            if (catalogIndex == pages.Length - 1) return;

            // Giữ trang hiện tại cho một lần chuyển màn hình; bỏ các trang nằm giữa và quay về catalog.
            for (var index = pages.Length - 2; index > catalogIndex; index--)
                navigation.RemovePage(pages[index]);
            await navigation.PopAsync();
        }
        finally { Volatile.Write(ref state.IsOpening, 0); }
    }

    private sealed class CatalogNavigationState
    {
        public int IsOpening;
    }
}
