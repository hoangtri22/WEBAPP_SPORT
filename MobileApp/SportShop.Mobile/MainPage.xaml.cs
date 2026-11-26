using SportShop.Mobile.ViewModels;
using SportShop.Mobile.Services;

namespace SportShop.Mobile;

// Tab Sản phẩm giống Views/Shop/Index.cshtml: tìm, chip danh mục, bộ lọc nâng cao, sắp xếp, thêm nhanh vào giỏ; dùng CatalogViewModel.
public partial class MainPage : ContentPage
{
    private readonly CatalogViewModel _viewModel;
    private readonly IServiceProvider _services;
    private readonly ICommerceApi _commerce;
    private readonly ProductListReturnState _returnState = new();
    private CancellationTokenSource? _lifetime;
    private bool _openingProduct;
    private bool _openingFilters;
    private bool _openingSort;
    private bool _purchasing;
    private bool _isVisible;
    private int _firstVisibleIndex;
    private double _scrollX;
    private double _scrollY;
#if ANDROID
    private RestoreBeforeDraw? _restoreListener;
#endif

    // Gắn CatalogViewModel vào trang.
    public MainPage(CatalogViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _services = services;
        _commerce = services.GetRequiredService<ICommerceApi>();
        _viewModel.SearchFocusRequested += () => Dispatcher.Dispatch(() => ProductSearchBar.Focus());
    }

    // Tải catalog qua CatalogViewModel.LoadAsync() khi mở trang; hủy request đầu tiên khi rời trang.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isVisible = true;
        _lifetime?.Dispose();
        var lifetime = _lifetime = new CancellationTokenSource();
        var token = lifetime.Token;
        if (!_viewModel.HasLoaded)
            await _viewModel.LoadAsync(cancellationToken: token);
        if (token.IsCancellationRequested || !_isVisible) return;
        QueueScrollRestoration();
        try { await _viewModel.PrepareFiltersAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        try { await _commerce.GetCartAsync(token); }
        catch (CommerceException) { /* Không để lỗi giỏ chặn xem catalog. */ }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    // Hủy request đang chạy khi rời trang.
    protected override void OnDisappearing()
    {
        _isVisible = false;
        _lifetime?.Cancel();
#if ANDROID
        _restoreListener?.Detach();
        _restoreListener = null;
#endif
        base.OnDisappearing();
    }

    // Bấm chip danh mục: tải lại danh sách theo danh mục qua CatalogViewModel.OpenCategoryAsync(), xóa từ khóa như khi mở từ Trang chủ.
    private async void OnCategoryChipTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not CategoryChip chip || chip.IsSelected || _viewModel.IsBusy) return;
        ProductSearchBar.Unfocus();
        await _viewModel.OpenCategoryAsync(chip.Id);
    }

    // Bộ lọc cuộn riêng để danh sách lựa chọn dài vẫn dùng được trên màn hình nhỏ.
    private async void OnAdvancedFiltersClicked(object? sender, EventArgs e)
    {
        if (_openingFilters) return;
        _openingFilters = true;
        try { await Navigation.PushAsync(new CatalogFiltersPage(_viewModel)); }
        finally { _openingFilters = false; }
    }

    // Chọn cách sắp xếp rồi tải lại danh sách từ trang đầu.
    private async void OnSortClicked(object? sender, EventArgs e)
    {
        if (_openingSort) return;
        _openingSort = true;
        ProductSearchBar.Unfocus();
        try
        {
            var selected = await DisplayActionSheetAsync("Sắp xếp sản phẩm", "Đóng", null,
                _viewModel.SortOptions.Select(option => option.Name).ToArray());
            var sort = _viewModel.SortOptions.FirstOrDefault(option => option.Name == selected);
            if (sort is null) return;
            _viewModel.SelectedSort = sort;
            await _viewModel.LoadAsync();
        }
        finally { _openingSort = false; }
    }

    // Mỗi sản phẩm mở một trang chi tiết riêng để lựa chọn không bị lẫn giữa các sản phẩm.
    private async void OnProductTapped(object? sender, TappedEventArgs e)
    {
        if (_openingProduct || _purchasing || sender is not BindableObject { BindingContext: ProductCard product }) return;
        _openingProduct = true;
        try
        {
            var page = _services.GetRequiredService<ProductDetailPage>();
            CaptureReturnPoint(product.Id);
            page.SetContinueShopping(ReturnToCatalogAsync);
            QuickAddSuccess.IsVisible = false;
            await Navigation.PushAsync(page);
            await page.LoadAsync(product.Id);
        }
        finally { _openingProduct = false; }
    }

    // Quay về danh sách đã mở chi tiết và khôi phục vị trí cuộn; trang đó không còn thì mở danh sách mặc định.
    private async Task ReturnToCatalogAsync()
    {
        _returnState.RequestRestore();
        if (!await AppNavigation.ReturnToSourceAsync(Navigation, this))
            await AppNavigation.OpenDefaultCatalogAsync(Navigation, _services);
        else QueueScrollRestoration();
    }

    // Nút Thêm vào giỏ hàng trên thẻ sản phẩm.
    private async void OnCardAddClicked(object? sender, EventArgs e)
    {
        if (sender is ImageButton { CommandParameter: ProductCard product }) await PurchaseFromCardAsync(product, false);
    }

    // Nút Mua ngay trên thẻ sản phẩm.
    private async void OnCardBuyClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: ProductCard product }) await PurchaseFromCardAsync(product, true);
    }

    // Mở QuickPurchasePage để chọn biến thể; thêm xong thì báo thành công hoặc mở CheckoutPage nếu mua ngay.
    private async Task PurchaseFromCardAsync(ProductCard product, bool buyNow)
    {
        if (_purchasing || _openingProduct || _viewModel.IsBusy || !product.InStock) return;
        _purchasing = true;
        QuickAddSuccess.IsVisible = false;
        ProductSearchBar.Unfocus();
        CaptureReturnPoint(product.Id);
        try
        {
            var picker = new QuickPurchasePage(_services.GetRequiredService<ICatalogApi>(), _commerce, product.Id, buyNow);
            await Navigation.PushModalAsync(picker);
            if (!await picker.Completion) return;
            QueueScrollRestoration();
            if (buyNow)
                await Navigation.PushAsync(_services.GetRequiredService<CheckoutPage>());
            else
            {
                QuickAddProductName.Text = product.Name;
                QuickAddSuccess.IsVisible = true;
                SemanticScreenReader.Default.Announce("Đã thêm sản phẩm vào giỏ hàng thành công!");
            }
        }
        finally { _purchasing = false; }
    }

    private void OnDismissQuickSuccess(object? sender, EventArgs e) => QuickAddSuccess.IsVisible = false;

    // Ghi lại vị trí đang xem để khôi phục sau khi mở chi tiết.
    private void OnProductsScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        _firstVisibleIndex = Math.Max(0, e.FirstVisibleItemIndex);
        _scrollX = e.HorizontalOffset;
        _scrollY = e.VerticalOffset;
    }

    // Lưu sản phẩm, vị trí và bộ lọc hiện tại vào ProductListReturnState trước khi rời danh sách.
    private void CaptureReturnPoint(int productId)
    {
        var index = _viewModel.Products.ToList().FindIndex(product => product.Id == productId);
        if (index < 0 || _viewModel.Products.Count == 0) return;
        var first = Math.Clamp(_firstVisibleIndex, 0, _viewModel.Products.Count - 1);
        int? top = null, firstTop = null;
        var x = _scrollX;
        var y = _scrollY;
#if ANDROID
        if (ProductCollection.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView recycler
            && recycler.GetLayoutManager() is AndroidX.RecyclerView.Widget.LinearLayoutManager layout)
        {
            top = layout.FindViewByPosition(index)?.Top - recycler.PaddingTop;
            firstTop = layout.FindViewByPosition(first)?.Top - recycler.PaddingTop;
            x = recycler.ComputeHorizontalScrollOffset();
            y = recycler.ComputeVerticalScrollOffset();
        }
#endif
        _returnState.Capture(new ProductListReturnPoint("catalog", productId, index,
            _viewModel.Products[first].Id, first, x, y, top, firstTop, _viewModel.Products.Count,
            _viewModel.ResultVersion, _viewModel.SearchText, _viewModel.AppliedFilterSummary, _viewModel.SelectedSort.Code));
    }

    private void OnProductLayoutReady(object? sender, EventArgs e) => QueueScrollRestoration();

    // Hẹn khôi phục vị trí trước lần vẽ kế tiếp, tránh hiện danh sách ở đầu rồi mới nhảy.
    private void QueueScrollRestoration()
    {
        if (!_isVisible || !_returnState.IsPending) return;
#if ANDROID
        if (ProductCollection.Handler?.PlatformView is not AndroidX.RecyclerView.Widget.RecyclerView recycler) return;
        _restoreListener?.Detach();
        _restoreListener = new RestoreBeforeDraw(recycler, () => RestoreCatalogPosition(recycler));
        recycler.ViewTreeObserver?.AddOnPreDrawListener(_restoreListener);
        recycler.RequestLayout();
#else
        if (_returnState.TryConsume(_viewModel.ResultVersion, ProductCollection.Height > 0, out var point))
        {
            var product = _viewModel.Products.FirstOrDefault(item => item.Id == point!.ProductId);
            if (product is not null) ProductCollection.ScrollTo(product, position: ScrollToPosition.MakeVisible, animate: false);
            else if (_viewModel.Products.Count > 0)
                ProductCollection.ScrollTo(Math.Clamp(point!.FirstVisibleIndex, 0, _viewModel.Products.Count - 1), animate: false);
        }
#endif
    }

#if ANDROID
    // Cuộn về sản phẩm đã bấm; trả 0 khi chưa sẵn sàng, 1 khi xong, 2 khi vừa yêu cầu layout mới.
    private int RestoreCatalogPosition(AndroidX.RecyclerView.Widget.RecyclerView recycler)
    {
        var ready = recycler.Width > 0 && recycler.Height > 0 && recycler.ChildCount > 0;
        if (!_returnState.TryConsume(_viewModel.ResultVersion, ready, out var point)) return _returnState.IsPending ? 0 : 1;
        if (recycler.GetLayoutManager() is not AndroidX.RecyclerView.Widget.LinearLayoutManager layout) return 1;
        var index = _viewModel.Products.ToList().FindIndex(product => product.Id == point!.ProductId);
        if (index >= 0 && point!.ProductTopPixels is { } top)
        {
            var current = layout.FindViewByPosition(index);
            if (current is null || Math.Abs(current.Top - recycler.PaddingTop - top) > 1)
            {
                layout.ScrollToPositionWithOffset(index, top);
                recycler.RequestLayout();
                return 2;
            }
        }
        else
        {
            var first = _viewModel.Products.ToList().FindIndex(product => product.Id == point!.FirstVisibleProductId);
            if (first >= 0 && point!.FirstTopPixels is { } firstTop)
            {
                layout.ScrollToPositionWithOffset(first, firstTop);
                recycler.RequestLayout();
                return 2;
            }
            else
                recycler.ScrollBy((int)(point!.ScrollX - recycler.ComputeHorizontalScrollOffset()),
                    (int)(point.ScrollY - recycler.ComputeVerticalScrollOffset()));
        }
        return 1;
    }

    // Listener của Android chạy khôi phục vị trí trước khi vẽ rồi tự gỡ.
    private sealed class RestoreBeforeDraw(AndroidX.RecyclerView.Widget.RecyclerView view, Func<int> restore)
        : Java.Lang.Object, Android.Views.ViewTreeObserver.IOnPreDrawListener
    {
        private bool _detached;
        // Chạy khôi phục; trả false để bỏ một frame khi vị trí chưa đúng.
        public bool OnPreDraw()
        {
            if (_detached) return true;
            var result = restore();
            if (result > 0) Detach();
            // ScrollToPositionWithOffset xin layout mới; không vẽ một frame ở vị trí cũ.
            return result != 2;
        }
        // Gỡ listener, gọi nhiều lần vẫn an toàn.
        public void Detach()
        {
            if (_detached) return;
            _detached = true;
            if (view.ViewTreeObserver is { IsAlive: true } observer) observer.RemoveOnPreDrawListener(this);
        }
    }
#endif

    // Nút Xem giỏ trên thông báo thêm thành công: chuyển sang tab Giỏ hàng.
    private async void OnCartNavClicked(object? sender, EventArgs e)
    {
        QuickAddSuccess.IsVisible = false;
        await AppNavigation.OpenTabAsync(AppTab.Cart);
    }
}
