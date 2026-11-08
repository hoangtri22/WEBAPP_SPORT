using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SportShop.Mobile.Services;
using SportShop.Contracts.Catalog;

namespace SportShop.Mobile.ViewModels;

// Trạng thái và thao tác của trang sản phẩm.
public sealed class CatalogViewModel : INotifyPropertyChanged
{
    private readonly ICatalogApi _api;
    private bool _isBusy;
    private bool _isRefreshing;
    private string _searchText = "";
    private CatalogFilter? _activeFilter;
    private CategoryOption? _selectedCategory;
    private CatalogSortOption _selectedSort = CatalogSortOption.All[0];
    private bool _inStockOnly = true;
    private bool _categoriesLoaded;
    private string? _filterError;
    private TaskCompletionSource? _loadCompletion;
    private string? _errorMessage;
    private bool _retryAppend;
    private int _page;
    private long _totalItems;

    // Nhận ICatalogApi.
    public CatalogViewModel(ICatalogApi api)
    {
        _api = api;
        Categories.Add(new CategoryOption(null, "Tất cả danh mục"));
        _selectedCategory = Categories[0];
        SearchCommand = new AsyncCommand(() => LoadAsync(), () => IsIdle);
        RefreshCommand = new AsyncCommand(() => LoadAsync(), () => IsIdle);
        RetryCommand = new AsyncCommand(() => LoadAsync(_retryAppend), () => IsIdle);
        LoadMoreCommand = new AsyncCommand(() => LoadAsync(append: true), () => IsIdle && HasMore);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ProductCard> Products { get; } = [];
    public ObservableCollection<CategoryOption> Categories { get; } = [];
    public IReadOnlyList<CatalogSortOption> SortOptions => CatalogSortOption.All;
    public CategoryOption? SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; Notify(); } }
    public CatalogSortOption SelectedSort { get => _selectedSort; set { _selectedSort = value ?? CatalogSortOption.All[0]; Notify(); } }
    public bool InStockOnly { get => _inStockOnly; set { _inStockOnly = value; Notify(); } }
    public bool HasFilterError => !string.IsNullOrEmpty(_filterError);
    public string? FilterError => _filterError;
    public AsyncCommand SearchCommand { get; }
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand RetryCommand { get; }
    public AsyncCommand LoadMoreCommand { get; }
    public bool HasLoaded { get; private set; }
    public bool IsBusy => _isBusy;
    public bool IsIdle => !_isBusy;
    public bool HasError => !string.IsNullOrEmpty(_errorMessage);
    public bool HasMore => HasLoaded && Products.Count < _totalItems;
    public bool ShowEmpty => HasLoaded && !IsBusy && !HasError && Products.Count == 0;
    public string? ErrorMessage => _errorMessage;
    public string ResultSummary => !HasLoaded ? (IsBusy ? "Đang tải sản phẩm…" : "Sản phẩm từ cửa hàng")
        : $"{Products.Count}/{_totalItems} sản phẩm · Kéo xuống để làm mới";

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; Notify(); }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set { _isRefreshing = value; Notify(); }
    }

    // Lấy catalog qua ICatalogApi.GetProductsAsync() theo từ khóa / trang; giữ trang cũ nếu tải thêm lỗi để thử lại đúng trang.
    public async Task LoadAsync(bool append = false, CancellationToken cancellationToken = default)
    {
        if (_isBusy) { IsRefreshing = false; return; }
        if (append && !HasMore) return;
        _isBusy = true;
        var completion = _loadCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _errorMessage = null;
        _retryAppend = append;
        UpdateState();
        var filter = append ? _activeFilter! : new CatalogFilter(SearchText.Trim(), SelectedCategory?.Id,
            SelectedSort.Code, InStockOnly);
        var nextPage = append ? _page + 1 : 1;
        try
        {
            if (!_categoriesLoaded) await LoadCategoriesAsync(cancellationToken);
            var result = await _api.GetProductsAsync(filter.Query, nextPage, filter.CategoryId, filter.Sort,
                filter.InStockOnly, cancellationToken);
            if (!append) Products.Clear();
            foreach (var product in result.Items)
                if (!Products.Any(existing => existing.Id == product.Id)) Products.Add(new ProductCard(product));
            _activeFilter = filter;
            _page = result.Page;
            _totalItems = result.Items.Count == 0 && append ? Products.Count : result.TotalItems;
            HasLoaded = true;
        }
        catch (CatalogException ex) { _errorMessage = ex.Message; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally
        {
            _isBusy = false;
            IsRefreshing = false;
            UpdateState();
            completion.TrySetResult();
            if (ReferenceEquals(_loadCompletion, completion)) _loadCompletion = null;
        }
    }

    // Mở một danh mục từ Trang chủ và bắt đầu lại từ trang đầu.
    public async Task OpenCategoryAsync(int? categoryId, CancellationToken cancellationToken = default)
    {
        if (_loadCompletion is { } pending) await pending.Task.WaitAsync(cancellationToken);
        await LoadCategoriesAsync(cancellationToken);
        SelectedCategory = Categories.FirstOrDefault(category => category.Id == categoryId) ?? Categories[0];
        SearchText = "";
        await LoadAsync(cancellationToken: cancellationToken);
    }

    // Tải danh mục riêng để khi lỗi bộ lọc người dùng vẫn xem được sản phẩm và thử lại.
    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        if (_categoriesLoaded) return;
        try
        {
            var selectedId = SelectedCategory?.Id;
            var categories = await _api.GetCategoriesAsync(cancellationToken);
            Categories.Clear();
            Categories.Add(new CategoryOption(null, "Tất cả danh mục"));
            foreach (var category in categories) Categories.Add(new CategoryOption(category.Id, category.Name));
            SelectedCategory = Categories.FirstOrDefault(category => category.Id == selectedId) ?? Categories[0];
            _categoriesLoaded = true;
            _filterError = null;
        }
        catch (CatalogException ex) { _filterError = ex.Message + " Bấm Tìm / Áp dụng để tải lại danh mục."; }
        Notify(nameof(HasFilterError));
        Notify(nameof(FilterError));
    }

    // Báo lại MainPage và các command sau mỗi lần đổi trạng thái tải.
    private void UpdateState()
    {
        foreach (var name in new[] { nameof(IsBusy), nameof(IsIdle), nameof(HasLoaded), nameof(HasError),
                     nameof(ErrorMessage), nameof(HasMore), nameof(ShowEmpty), nameof(ResultSummary) }) Notify(name);
        SearchCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
        LoadMoreCommand.NotifyCanExecuteChanged();
    }

    // Báo giao diện cập nhật thuộc tính.
    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

// Lựa chọn lọc/sắp xếp có nhãn tiếng Việt, chỉ mã được gửi lên API.
public sealed record CategoryOption(int? Id, string Name);
public sealed record CatalogSortOption(string Code, string Name)
{
    public static IReadOnlyList<CatalogSortOption> All { get; } =
    [new("new", "Mới nhất"), new("price_asc", "Giá tăng dần"), new("price_desc", "Giá giảm dần"), new("name", "Tên A–Z")];
}
internal sealed record CatalogFilter(string? Query, int? CategoryId, string Sort, bool InStockOnly);

// Command chạy hàm bất đồng bộ cho các nút trong XAML.
public sealed class AsyncCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    // Kiểm tra command có chạy được không.
    public bool CanExecute(object? parameter) => canExecute();
    // Chạy hàm bất đồng bộ.
    public async void Execute(object? parameter)
    {
        if (CanExecute(parameter)) await execute();
    }
    // Báo giao diện kiểm tra lại trạng thái nút.
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
