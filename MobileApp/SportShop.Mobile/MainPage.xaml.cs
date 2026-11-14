using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang danh sách sản phẩm, dùng CatalogViewModel.
public partial class MainPage : ContentPage
{
    private readonly CatalogViewModel _viewModel;
    private readonly IServiceProvider _services;
    private CancellationTokenSource? _lifetime;
    private bool _openingProduct;
    private bool _openingFilters;
    private bool _openingSort;
    private bool _navigatingTab;

    // Gắn CatalogViewModel vào trang.
    public MainPage(CatalogViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _services = services;
    }

    // Tải catalog qua CatalogViewModel.LoadAsync() khi mở trang; hủy request đầu tiên khi rời trang.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _lifetime?.Dispose();
        _lifetime = new CancellationTokenSource();
        if (!_viewModel.HasLoaded)
            await _viewModel.LoadAsync(cancellationToken: _lifetime.Token);
    }

    // Hủy request đang chạy khi rời trang.
    protected override void OnDisappearing()
    {
        _lifetime?.Cancel();
        base.OnDisappearing();
    }

    // Trang chủ có thể mở thẳng danh mục, vẫn dùng cùng bộ lọc và phân trang của catalog.
    public Task OpenCategoryAsync(int? categoryId) => _viewModel.OpenCategoryAsync(categoryId);

    // Bộ lọc cuộn riêng để danh sách lựa chọn dài vẫn dùng được trên màn hình nhỏ.
    private async void OnAdvancedFiltersClicked(object? sender, EventArgs e)
    {
        if (_openingFilters) return;
        _openingFilters = true;
        try { await Navigation.PushAsync(new CatalogFiltersPage(_viewModel)); }
        finally { _openingFilters = false; }
    }

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
        if (_openingProduct || sender is not BindableObject { BindingContext: ProductCard product }) return;
        _openingProduct = true;
        try
        {
            var page = _services.GetRequiredService<ProductDetailPage>();
            await Navigation.PushAsync(page);
            await page.LoadAsync(product.Id);
        }
        finally { _openingProduct = false; }
    }

    // Thanh tab giữ cách mở Trang chủ, Giỏ hàng và Tài khoản như các màn hình hiện có.
    private async void OnHomeNavClicked(object? sender, EventArgs e)
        => await NavigateTabAsync(async () =>
        {
            if (Navigation.NavigationStack.Count > 1) await Navigation.PopToRootAsync();
        });

    private async void OnCartNavClicked(object? sender, EventArgs e)
        => await NavigateTabAsync(() => Navigation.PushAsync(_services.GetRequiredService<CartPage>()));

    private async void OnAccountNavClicked(object? sender, EventArgs e)
        => await NavigateTabAsync(async () =>
        {
            var auth = _services.GetRequiredService<AuthViewModel>();
            if (auth.IsSignedIn) await Navigation.PushAsync(_services.GetRequiredService<ProfilePage>());
            else if (await DisplayActionSheetAsync("Tài khoản", "Đóng", null, "Đăng nhập / Đăng ký") == "Đăng nhập / Đăng ký")
                await auth.LeaveAsync();
        });

    private async Task NavigateTabAsync(Func<Task> navigate)
    {
        if (_navigatingTab) return;
        _navigatingTab = true;
        try { await navigate(); }
        finally { _navigatingTab = false; }
    }
}
