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

    // Mở các chương trình khuyến mãi đang hiệu lực.
    private async void OnPromotionsClicked(object? sender, EventArgs e)
        => await Navigation.PushAsync(_services.GetRequiredService<PromotionsPage>());

    // Mở danh sách trang hỗ trợ và giới thiệu.
    private async void OnInfoClicked(object? sender, EventArgs e)
    {
        var page = _services.GetRequiredService<InfoPagesPage>();
        await Navigation.PushAsync(page);
        await page.LoadAsync();
    }
}
