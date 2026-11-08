using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SportShop.Contracts.Catalog;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

// Trang chủ đọc danh mục và sản phẩm thật từ cùng API với màn hình cửa hàng.
public sealed class HomeViewModel(ICatalogApi api) : INotifyPropertyChanged
{
    private bool _isBusy;
    private string? _error;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<ProductCard> Products { get; } = [];
    public bool IsBusy => _isBusy;
    public bool IsIdle => !_isBusy;
    public bool HasLoaded { get; private set; }
    public string? ErrorMessage => _error;
    public bool HasError => !string.IsNullOrWhiteSpace(_error);
    public bool ShowEmpty => HasLoaded && !IsBusy && !HasError && Products.Count == 0;

    // Tải lại khi khách yêu cầu; lỗi giữ danh sách cũ để vẫn mở được sản phẩm.
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_isBusy) return;
        _isBusy = true;
        _error = null;
        NotifyState();
        try
        {
            var categories = await api.GetCategoriesAsync(cancellationToken);
            var products = await api.GetProductsAsync(null, 1, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Categories.Clear();
            foreach (var category in categories) Categories.Add(category);
            Products.Clear();
            foreach (var product in products.Items.Take(8)) Products.Add(new ProductCard(product));
            HasLoaded = true;
        }
        catch (CatalogException ex) { _error = ex.Message; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { _isBusy = false; NotifyState(); }
    }

    // Báo trạng thái tải, rỗng và lỗi cho trang chủ.
    private void NotifyState()
    {
        foreach (var property in new[] { nameof(IsBusy), nameof(IsIdle), nameof(HasLoaded), nameof(ErrorMessage), nameof(HasError), nameof(ShowEmpty) })
            Notify(property);
    }
    private void Notify([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
}
