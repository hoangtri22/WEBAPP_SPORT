using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Áp dụng xác nhận lựa chọn; quay lại khôi phục bộ lọc trước khi mở trang.
public partial class CatalogFiltersPage : ContentPage
{
    private readonly CatalogViewModel _viewModel;
    private readonly FilterDraft _initialFilters;
    private bool _applying;
    private bool _filtersApplied;
    private bool _isOnScreen;
    private CancellationTokenSource? _lifetime;

    public CatalogFiltersPage(CatalogViewModel viewModel)
    {
        _initialFilters = new FilterDraft(viewModel.SelectedCategory?.Id, viewModel.InStockOnly,
            viewModel.MinPriceText, viewModel.MaxPriceText,
            viewModel.Sizes.Where(option => option.IsSelected).Select(option => option.Value).ToHashSet(StringComparer.Ordinal),
            viewModel.Colors.Where(option => option.IsSelected).Select(option => option.Value).ToHashSet(StringComparer.Ordinal));
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isOnScreen = true;
        _lifetime?.Dispose();
        var lifetime = _lifetime = new CancellationTokenSource();
        try { await _viewModel.PrepareFiltersAsync(lifetime.Token); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    protected override void OnDisappearing()
    {
        _isOnScreen = false;
        _lifetime?.Cancel();
        if (!_filtersApplied && !_applying) RestoreInitialFilters();
        base.OnDisappearing();
    }

    private void RestoreInitialFilters()
    {
        _viewModel.ClearAdvancedFilters();
        _viewModel.SelectedCategory = _viewModel.Categories.FirstOrDefault(category => category.Id == _initialFilters.CategoryId)
            ?? _viewModel.Categories.FirstOrDefault();
        _viewModel.InStockOnly = _initialFilters.InStockOnly;
        _viewModel.MinPriceText = _initialFilters.MinPrice;
        _viewModel.MaxPriceText = _initialFilters.MaxPrice;
        foreach (var option in _viewModel.Sizes) option.IsSelected = _initialFilters.Sizes.Contains(option.Value);
        foreach (var option in _viewModel.Colors) option.IsSelected = _initialFilters.Colors.Contains(option.Value);
    }

    private void OnClearClicked(object? sender, EventArgs e) => _viewModel.ClearAdvancedFilters();

    private void OnOptionLabelTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: CatalogFilterOption option })
            option.IsSelected = !option.IsSelected;
    }

    private async void OnApplyClicked(object? sender, EventArgs e)
    {
        if (_applying || _viewModel.IsBusy || _viewModel.IsPreparingFilters) return;
        _applying = true;
        try
        {
            await _viewModel.ApplyFiltersAsync();
            if (!_viewModel.HasValidationError && !_viewModel.HasError)
            {
                _filtersApplied = true;
                if (Navigation.NavigationStack.LastOrDefault() == this) await Navigation.PopAsync();
            }
        }
        finally
        {
            _applying = false;
            // Một lần áp dụng đang chạy được hoàn tất trước khi khôi phục bản nháp đã hủy.
            if (!_filtersApplied && !_isOnScreen) RestoreInitialFilters();
        }
    }

    private sealed record FilterDraft(int? CategoryId, bool InStockOnly, string MinPrice, string MaxPrice,
        HashSet<string> Sizes, HashSet<string> Colors);
}
