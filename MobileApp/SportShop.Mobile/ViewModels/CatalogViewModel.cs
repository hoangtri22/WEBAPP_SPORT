using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

public sealed class CatalogViewModel : INotifyPropertyChanged
{
    private readonly ICatalogApi _api;
    private bool _isBusy;
    private bool _isRefreshing;
    private string _searchText = "";
    private string? _activeQuery;
    private string? _errorMessage;
    private bool _retryAppend;
    private int _page;
    private long _totalItems;

    public CatalogViewModel(ICatalogApi api)
    {
        _api = api;
        SearchCommand = new AsyncCommand(() => LoadAsync(), () => IsIdle);
        RefreshCommand = new AsyncCommand(() => LoadAsync(), () => IsIdle);
        RetryCommand = new AsyncCommand(() => LoadAsync(_retryAppend), () => IsIdle);
        LoadMoreCommand = new AsyncCommand(() => LoadAsync(append: true), () => IsIdle && HasMore);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ProductCard> Products { get; } = [];
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

    // Lấy catalog theo từ khóa/phân trang; giữ trang cũ nếu tải thêm lỗi để retry đúng trang.
    public async Task LoadAsync(bool append = false, CancellationToken cancellationToken = default)
    {
        if (_isBusy) { IsRefreshing = false; return; }
        if (append && !HasMore) return;
        _isBusy = true;
        _errorMessage = null;
        _retryAppend = append;
        UpdateState();
        var query = append ? _activeQuery : SearchText.Trim();
        var nextPage = append ? _page + 1 : 1;
        try
        {
            var result = await _api.GetProductsAsync(query, nextPage, cancellationToken);
            if (!append) Products.Clear();
            foreach (var product in result.Items)
                if (!Products.Any(existing => existing.Id == product.Id)) Products.Add(new ProductCard(product));
            _activeQuery = query;
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
        }
    }

    private void UpdateState()
    {
        foreach (var name in new[] { nameof(IsBusy), nameof(IsIdle), nameof(HasLoaded), nameof(HasError),
                     nameof(ErrorMessage), nameof(HasMore), nameof(ShowEmpty), nameof(ResultSummary) }) Notify(name);
        SearchCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
        LoadMoreCommand.NotifyCanExecuteChanged();
    }

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

public sealed class AsyncCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute();
    public async void Execute(object? parameter)
    {
        if (CanExecute(parameter)) await execute();
    }
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
