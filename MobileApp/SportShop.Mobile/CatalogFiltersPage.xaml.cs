using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Dùng chung bản nháp của catalog; Back giữ nguyên kết quả đã áp dụng.
public partial class CatalogFiltersPage : ContentPage
{
    private readonly CatalogViewModel _viewModel;
    private int _applyVersion;
    private CancellationTokenSource? _lifetime;

    public CatalogFiltersPage(CatalogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _lifetime?.Dispose();
        var lifetime = _lifetime = new CancellationTokenSource();
        try { await _viewModel.PrepareFiltersAsync(lifetime.Token); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    protected override void OnDisappearing()
    {
        _lifetime?.Cancel();
        base.OnDisappearing();
    }

    private void OnClearClicked(object? sender, EventArgs e) => _viewModel.ClearAdvancedFilters();

    private async void OnApplyClicked(object? sender, EventArgs e)
    {
        var version = ++_applyVersion;
        await _viewModel.ApplyFiltersAsync();
        if (version == _applyVersion && !_viewModel.HasValidationError && !_viewModel.HasError
            && Navigation.NavigationStack.LastOrDefault() == this)
            await Navigation.PopAsync();
    }
}
