using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class MainPage : ContentPage
{
    private readonly CatalogViewModel _viewModel;
    private CancellationTokenSource? _lifetime;

    public MainPage(CatalogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
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

    protected override void OnDisappearing()
    {
        _lifetime?.Cancel();
        base.OnDisappearing();
    }
}
