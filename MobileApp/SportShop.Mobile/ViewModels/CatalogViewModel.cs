using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
    private bool _optionsLoaded;
    private string? _optionsError;
    private string? _validationError;
    private string _minPriceText = "";
    private string _maxPriceText = "";
    private CancellationTokenSource? _loadCancellation;
    private CatalogFilter? _loadingFilter;
    private bool _loadingAppend;
    private int _loadVersion;
    private bool _hasMore;
    private readonly SemaphoreSlim _metadataGate = new(1, 1);
    private bool _isPreparingFilters;
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
        SearchCommand = new AsyncCommand(() => LoadAsync(), () => true);
        RefreshCommand = new AsyncCommand(() => LoadAsync(), () => IsIdle);
        RetryCommand = new AsyncCommand(() => LoadAsync(_retryAppend), () => IsIdle);
        LoadMoreCommand = new AsyncCommand(() => LoadAsync(append: true), () => IsIdle && HasMore);
        ResetCommand = new AsyncCommand(ResetAsync, () => true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ProductCard> Products { get; } = [];
    public ObservableCollection<CategoryOption> Categories { get; } = [];
    public ObservableCollection<CatalogFilterOption> Sizes { get; } = [];
    public ObservableCollection<CatalogFilterOption> Colors { get; } = [];
    public string MinPriceText { get => _minPriceText; set { _minPriceText = value; Notify(); } }
    public string MaxPriceText { get => _maxPriceText; set { _maxPriceText = value; Notify(); } }
    public bool HasValidationError => !string.IsNullOrEmpty(_validationError);
    public string? ValidationError => _validationError;
    public bool IsPreparingFilters => _isPreparingFilters;
    public IReadOnlyList<CatalogSortOption> SortOptions => CatalogSortOption.All;
    public CategoryOption? SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; Notify(); } }
    public CatalogSortOption SelectedSort { get => _selectedSort; set { _selectedSort = value ?? CatalogSortOption.All[0]; Notify(); } }
    public bool InStockOnly { get => _inStockOnly; set { _inStockOnly = value; Notify(); } }
    public bool HasFilterError => !string.IsNullOrEmpty(FilterError);
    public string? FilterError => string.Join(" ", new[] { _filterError, _optionsError }.Where(value => !string.IsNullOrEmpty(value)));
    public AsyncCommand SearchCommand { get; }
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand RetryCommand { get; }
    public AsyncCommand LoadMoreCommand { get; }
    public AsyncCommand ResetCommand { get; }
    public bool HasLoaded { get; private set; }
    public bool IsBusy => _isBusy;
    public bool IsIdle => !_isBusy;
    public bool HasError => !string.IsNullOrEmpty(_errorMessage);
    public bool HasMore => HasLoaded && _hasMore;
    public bool HasProducts => Products.Count > 0;
    public bool ShowEmpty => HasLoaded && !IsBusy && !HasError && Products.Count == 0;
    public string? ErrorMessage => _errorMessage;
    public string ResultSummary => !HasLoaded ? (IsBusy ? "Đang tải sản phẩm…" : "Sản phẩm từ cửa hàng")
        : $"{Products.Count}/{_totalItems} sản phẩm · Kéo xuống để làm mới";
    public string AppliedFilterSummary => _activeFilter is not { } filter ? "Chưa áp dụng bộ lọc"
        : "Đang áp dụng: " + string.Join(" · ", new[]
        {
            string.IsNullOrEmpty(filter.Query) ? null : $"“{filter.Query}”",
            filter.CategoryId is { } id ? Categories.FirstOrDefault(category => category.Id == id)?.Name : null,
            filter.MinPrice is { } min ? $"Từ {min:N0} ₫" : null,
            filter.MaxPrice is { } max ? $"Đến {max:N0} ₫" : null,
            filter.Sizes.Length > 0 ? "Size " + string.Join(", ", filter.Sizes) : null,
            filter.Colors.Length > 0 ? "Màu " + string.Join(", ", filter.Colors) : null,
            filter.InStockOnly ? "Còn hàng" : null,
            SortOptions.First(option => option.Code == filter.Sort).Name
        }.Where(value => value is not null));

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
        CatalogFilter? filter;
        if (append) filter = _activeFilter;
        else if (!TryCreateFilter(out filter)) { IsRefreshing = false; return; }
        if (filter is null) return;
        if (_isBusy && (append || (!_loadingAppend && _loadingFilter?.Matches(filter) == true
            && _loadCancellation?.IsCancellationRequested == false))) { IsRefreshing = false; return; }
        if (append && !HasMore) return;
        _loadCancellation?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = request;
        var token = request.Token;
        var version = ++_loadVersion;
        _loadingFilter = filter;
        _loadingAppend = append;
        _isBusy = true;
        var completion = _loadCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _errorMessage = null;
        _retryAppend = append;
        UpdateState();
        var nextPage = append ? _page + 1 : 1;
        try
        {
            await PrepareFiltersAsync(token);
            token.ThrowIfCancellationRequested();
            var result = await _api.GetProductsAsync(filter.Query, nextPage, filter.CategoryId, filter.Sort,
                filter.InStockOnly, filter.MinPrice, filter.MaxPrice, filter.Sizes, filter.Colors, token);
            if (version != _loadVersion || token.IsCancellationRequested) return;
            if (!append) Products.Clear();
            foreach (var product in result.Items)
                if (!Products.Any(existing => existing.Id == product.Id)) Products.Add(new ProductCard(product));
            _activeFilter = filter;
            _page = result.Page;
            _totalItems = result.Items.Count == 0 && append ? Products.Count : result.TotalItems;
            _hasMore = result.Items.Count > 0 && (long)result.Page * result.PageSize < result.TotalItems;
            HasLoaded = true;
        }
        catch (CatalogException ex)
        {
            if (version == _loadVersion && !token.IsCancellationRequested) _errorMessage = ex.Message;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            if (version == _loadVersion)
            {
                _isBusy = false;
                _loadingFilter = null;
                _loadCancellation = null;
                IsRefreshing = false;
                UpdateState();
            }
            completion.TrySetResult();
            if (ReferenceEquals(_loadCompletion, completion)) _loadCompletion = null;
        }
    }

    // Màn hình bộ lọc có thể đợi request cùng điều kiện đang chạy mà không gửi request trùng.
    public async Task ApplyFiltersAsync()
    {
        await LoadAsync();
        if (!HasValidationError && _loadCompletion is { } pending) await pending.Task;
    }

    // Nạp lựa chọn độc lập với trang sản phẩm; tuần tự hóa để không xóa lựa chọn khi hai màn hình cùng nạp.
    public async Task PrepareFiltersAsync(CancellationToken cancellationToken = default)
    {
        await _metadataGate.WaitAsync(cancellationToken);
        _isPreparingFilters = true;
        Notify(nameof(IsPreparingFilters));
        try
        {
            if (!_categoriesLoaded) await LoadCategoriesAsync(cancellationToken);
            if (!_optionsLoaded) await LoadOptionsAsync(cancellationToken);
        }
        catch (CatalogException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        finally
        {
            _isPreparingFilters = false;
            Notify(nameof(IsPreparingFilters));
            _metadataGate.Release();
        }
    }

    // Xóa riêng các điều kiện nâng cao trong bản nháp; danh mục/từ khóa vẫn được giữ khi áp dụng.
    public void ClearAdvancedFilters()
    {
        MinPriceText = MaxPriceText = "";
        foreach (var option in Sizes.Concat(Colors)) option.IsSelected = false;
        _validationError = null;
        Notify(nameof(ValidationError));
        Notify(nameof(HasValidationError));
    }

    // Đặt lại toàn bộ bộ lọc về mặc định và bắt đầu từ trang đầu.
    public async Task ResetAsync()
    {
        ClearAdvancedFilters();
        SearchText = "";
        SelectedCategory = Categories[0];
        SelectedSort = SortOptions[0];
        InStockOnly = true;
        await LoadAsync();
    }

    // Chụp bộ lọc bất biến để tải thêm không đọc lại các lựa chọn chưa áp dụng.
    private bool TryCreateFilter(out CatalogFilter? filter)
    {
        filter = null;
        _validationError = null;
        if (!TryPrice(MinPriceText, out var minimum) || !TryPrice(MaxPriceText, out var maximum))
            _validationError = "Giá phải là số từ 0 đến 9.999.999.999,99; không nhập dấu phân cách hàng nghìn.";
        else if (minimum > maximum)
            _validationError = "Giá tối thiểu không được lớn hơn giá tối đa.";
        else
        {
            var sizes = Sizes.Where(option => option.IsSelected).Select(option => option.Value).ToArray();
            var colors = Colors.Where(option => option.IsSelected).Select(option => option.Value).ToArray();
            if (sizes.Length > 50 || colors.Length > 50)
                _validationError = "Vui lòng chọn tối đa 50 size và 50 màu.";
            else filter = new CatalogFilter(SearchText.Trim(), SelectedCategory?.Id, SelectedSort.Code,
                InStockOnly, minimum, maximum, sizes, colors);
        }
        Notify(nameof(ValidationError));
        Notify(nameof(HasValidationError));
        return filter is not null;
    }

    private static bool TryPrice(string? text, out decimal? price)
    {
        price = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        var normalized = text.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || value < 0 || value > 9999999999.99m || decimal.Round(value, 2) != value) return false;
        price = value;
        return true;
    }

    // Mở một danh mục từ Trang chủ và bắt đầu lại từ trang đầu.
    public async Task OpenCategoryAsync(int? categoryId, CancellationToken cancellationToken = default)
    {
        if (_loadCompletion is { } pending) await pending.Task.WaitAsync(cancellationToken);
        await PrepareFiltersAsync(cancellationToken);
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
            cancellationToken.ThrowIfCancellationRequested();
            Categories.Clear();
            Categories.Add(new CategoryOption(null, "Tất cả danh mục"));
            foreach (var category in categories) Categories.Add(new CategoryOption(category.Id, category.Name));
            SelectedCategory = Categories.FirstOrDefault(category => category.Id == selectedId) ?? Categories[0];
            _categoriesLoaded = true;
            _filterError = null;
        }
        catch (CatalogException ex) when (!cancellationToken.IsCancellationRequested)
        { _filterError = ex.Message + " Bấm Tìm / Áp dụng để tải lại danh mục."; }
        Notify(nameof(HasFilterError));
        Notify(nameof(FilterError));
    }

    // Size và màu từ API, không hardcode hoặc chỉ dùng sản phẩm ở trang đang xem.
    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var options = await _api.GetFilterOptionsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Sizes.Clear();
            Colors.Clear();
            foreach (var size in options.Sizes.Distinct()) Sizes.Add(new CatalogFilterOption(size));
            foreach (var color in options.Colors.Distinct()) Colors.Add(new CatalogFilterOption(color));
            _optionsLoaded = true;
            _optionsError = null;
        }
        catch (CatalogException ex) when (!cancellationToken.IsCancellationRequested)
        { _optionsError = ex.Message + " Bấm Tìm / Áp dụng để tải lại size và màu."; }
        Notify(nameof(HasFilterError));
        Notify(nameof(FilterError));
    }

    // Báo lại MainPage và các command sau mỗi lần đổi trạng thái tải.
    private void UpdateState()
    {
        foreach (var name in new[] { nameof(IsBusy), nameof(IsIdle), nameof(HasLoaded), nameof(HasError),
                     nameof(ErrorMessage), nameof(HasMore), nameof(HasProducts), nameof(ShowEmpty), nameof(ResultSummary),
                     nameof(AppliedFilterSummary) }) Notify(name);
        SearchCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
        LoadMoreCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
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
internal sealed record CatalogFilter(string? Query, int? CategoryId, string Sort, bool InStockOnly,
    decimal? MinPrice, decimal? MaxPrice, string[] Sizes, string[] Colors)
{
    public bool Matches(CatalogFilter other) => Query == other.Query && CategoryId == other.CategoryId
        && Sort == other.Sort && InStockOnly == other.InStockOnly && MinPrice == other.MinPrice
        && MaxPrice == other.MaxPrice && Sizes.SequenceEqual(other.Sizes) && Colors.SequenceEqual(other.Colors);
}

public sealed class CatalogFilterOption(string value) : INotifyPropertyChanged
{
    private bool _isSelected;
    public string Value => value;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

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
