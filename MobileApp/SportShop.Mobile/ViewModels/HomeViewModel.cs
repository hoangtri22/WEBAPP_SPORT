using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SportShop.Contracts.Catalog;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

// Thẻ danh mục trên Trang chủ; icon và màu xoay vòng như .shop-category (running, men, women, gear) của Views/Home/Index.cshtml.
public sealed record HomeCategory(CategoryDto Category, int Index)
{
    private static readonly string[] Icons = ["ic_zap_teal.png", "ic_user_teal.png", "ic_sparkles_teal.png", "ic_activity_teal.png"];
    private static readonly string[] Tints = ["#E6F1EF", "#E3EFFF", "#FFF1E6", "#F1F5F9"];
    public int Id => Category.Id;
    public string Name => Category.Name;
    public string CountText => $"{Category.ProductCount} sản phẩm";
    public string Icon => Icons[Index % Icons.Length];
    public string Tint => Tints[Index % Tints.Length];
}

// Trang chủ đọc danh mục và sản phẩm thật từ cùng API với màn hình cửa hàng.
public sealed class HomeViewModel : INotifyPropertyChanged
{
    private readonly ICatalogApi api;
    private bool _isBusy;
    private bool _isRefreshing;
    private string? _error;

    // Nhận ICatalogApi; RefreshCommand dùng cho kéo xuống làm mới.
    public HomeViewModel(ICatalogApi api)
    {
        this.api = api;
        RefreshCommand = new AsyncCommand(async () =>
        {
            _isRefreshing = true;
            Notify(nameof(IsRefreshing));
            try { await LoadAsync(); }
            finally { _isRefreshing = false; Notify(nameof(IsRefreshing)); }
        }, () => true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public AsyncCommand RefreshCommand { get; }
    public bool IsRefreshing { get => _isRefreshing; set { _isRefreshing = value; Notify(); } }
    public bool IsInitialLoading => _isBusy && !_isRefreshing && Products.Count == 0;
    public ObservableCollection<HomeCategory> Categories { get; } = [];
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
            foreach (var category in categories) Categories.Add(new HomeCategory(category, Categories.Count));
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
        foreach (var property in new[] { nameof(IsBusy), nameof(IsIdle), nameof(HasLoaded), nameof(ErrorMessage), nameof(HasError), nameof(ShowEmpty), nameof(IsInitialLoading) })
            Notify(property);
    }
    private void Notify([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
}
