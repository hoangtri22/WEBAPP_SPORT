using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using SportShop.Contracts.Catalog;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

// Một lựa chọn size/màu tương ứng chính xác một biến thể của backend.
public sealed class ProductVariantOption(ProductVariantDto variant)
{
    public ProductVariantDto Variant => variant;
    public int Id => variant.Id;
    public string Label => string.Join(" · ", new[]
    {
        string.IsNullOrWhiteSpace(variant.Size) ? null : "Size " + variant.Size,
        string.IsNullOrWhiteSpace(variant.Color) ? null : "Màu " + variant.Color,
        variant.Stock > 0 ? $"Còn {variant.Stock}" : "Hết hàng"
    }.Where(value => value is not null));
}

// Chi tiết sản phẩm, lựa chọn biến thể và thêm giỏ qua service giỏ hàng hiện hữu.
public sealed class ProductDetailViewModel(ICatalogApi catalog, ICommerceApi commerce) : INotifyPropertyChanged
{
    private ProductDetailDto? _product;
    private ProductVariantOption? _selectedVariant;
    private bool _isBusy;
    private int _quantity = 1;
    private string? _error;
    private string? _status;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ProductVariantOption> Variants { get; } = [];
    public int ProductId => _product?.Id ?? 0;
    public string Name => _product?.Name ?? "Chi tiết sản phẩm";
    public string CategoryName => _product?.CategoryName ?? "";
    public string Description => string.IsNullOrWhiteSpace(_product?.Description)
        ? "Cửa hàng đang cập nhật mô tả sản phẩm." : _product.Description;
    public string? ImageUrl => SelectedVariant?.Variant.ImageUrl ?? _product?.ImageUrl;
    public string PriceText => (SelectedVariant?.Variant.Price ?? _product?.MinPrice) is { } price
        ? price.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " ₫" : "Liên hệ";
    public int Stock => SelectedVariant?.Variant.Stock ?? 0;
    public int MaximumQuantity => Math.Max(1, Math.Min(Stock, 999));
    public string StockText => SelectedVariant is null ? "Sản phẩm chưa có biến thể để mua."
        : Stock > 0 ? $"Còn {Stock} {_product?.Unit} trong kho" : "Biến thể này tạm hết hàng.";
    public bool IsBusy => _isBusy;
    public bool IsIdle => !_isBusy;
    public bool HasProduct => _product is not null;
    public bool CanBuy => !_isBusy && SelectedVariant is not null && Stock > 0;
    public bool CanIncrease => CanBuy && Quantity < MaximumQuantity;
    public bool CanDecrease => CanBuy && Quantity > 1;
    public bool HasError => !string.IsNullOrWhiteSpace(_error);
    public string? ErrorMessage => _error;
    public bool HasStatus => !string.IsNullOrWhiteSpace(_status);
    public string? StatusMessage => _status;
    public ProductVariantOption? SelectedVariant
    {
        get => _selectedVariant;
        set
        {
            _selectedVariant = value is not null && Variants.Contains(value) ? value : null;
            _quantity = Math.Clamp(_quantity, 1, MaximumQuantity);
            _status = null;
            NotifyAll();
        }
    }
    public int Quantity
    {
        get => _quantity;
        set { _quantity = Math.Clamp(value, 1, MaximumQuantity); _status = null; NotifyAll(); }
    }
    public string QuantityText
    {
        get => Quantity.ToString(CultureInfo.InvariantCulture);
        set => Quantity = int.TryParse(value, out var quantity) ? quantity : 1;
    }

    // Tải mới chi tiết, giữ biến thể theo ID khi thử lại để cập nhật đúng tồn kho.
    public async Task LoadAsync(int productId, CancellationToken cancellationToken = default)
    {
        if (_isBusy) return;
        _isBusy = true;
        _error = _status = null;
        if (ProductId != productId) { _product = null; Variants.Clear(); _selectedVariant = null; _quantity = 1; }
        NotifyAll();
        try { Apply(await catalog.GetProductAsync(productId, cancellationToken)); }
        catch (CatalogException ex) { _error = ex.Message; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { _isBusy = false; NotifyAll(); }
    }

    // Chỉ gửi đúng ID biến thể và số lượng hợp lệ; service hiện hữu xử lý token/khách/giỏ chung.
    public async Task<bool> AddToCartAsync(CancellationToken cancellationToken = default)
    {
        if (!CanBuy) return false;
        var variantId = SelectedVariant!.Id;
        var quantity = Math.Clamp(Quantity, 1, MaximumQuantity);
        _isBusy = true;
        _error = _status = null;
        NotifyAll();
        try
        {
            await commerce.AddCartItemAsync(variantId, quantity, cancellationToken);
            _status = "Đã thêm sản phẩm vào giỏ hàng.";
            return true;
        }
        catch (CommerceException ex)
        {
            _error = ex.Message;
            if (ex.StatusCode == 409)
            {
                try { Apply(await catalog.GetProductAsync(ProductId, cancellationToken)); }
                catch (CatalogException) { }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            }
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
        finally { _isBusy = false; NotifyAll(); }
    }

    // Áp dụng tồn kho, giá và ảnh; ưu tiên biến thể còn hàng khi lần đầu mở sản phẩm.
    private void Apply(ProductDetailDto product)
    {
        var previousId = SelectedVariant?.Id;
        _product = product;
        Variants.Clear();
        foreach (var variant in product.Variants) Variants.Add(new ProductVariantOption(variant));
        _selectedVariant = Variants.FirstOrDefault(option => option.Id == previousId)
            ?? Variants.FirstOrDefault(option => option.Variant.Stock > 0) ?? Variants.FirstOrDefault();
        _quantity = Math.Clamp(_quantity, 1, MaximumQuantity);
    }

    // Cập nhật đồng bộ các thuộc tính phụ thuộc lựa chọn, tồn kho và trạng thái request.
    private void NotifyAll()
    {
        foreach (var property in new[] { nameof(ProductId), nameof(Name), nameof(CategoryName), nameof(Description),
            nameof(ImageUrl), nameof(PriceText), nameof(Stock), nameof(MaximumQuantity), nameof(StockText),
            nameof(IsBusy), nameof(IsIdle), nameof(HasProduct), nameof(CanBuy), nameof(CanIncrease), nameof(CanDecrease),
            nameof(HasError), nameof(ErrorMessage), nameof(HasStatus), nameof(StatusMessage),
            nameof(SelectedVariant), nameof(Quantity), nameof(QuantityText) }) Notify(property);
    }
    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
