using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using SportShop.Contracts.Catalog;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

// Chọn đúng biến thể trước khi thêm nhanh; không sửa trạng thái danh sách sản phẩm.
public sealed class QuickPurchaseViewModel(ICatalogApi catalog, ICommerceApi commerce, int productId, bool buyNow)
    : INotifyPropertyChanged
{
    private ProductDetailDto? _product;
    private ProductVariantOption? _selectedVariant;
    private bool _isLoading;
    private bool _isSubmitting;
    private bool _hasSucceeded;
    private int _quantity = 1;
    private string _quantityText = "1";
    private string? _error;

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ProductVariantOption> Variants { get; } = [];
    public bool BuyNow => buyNow;
    public string ActionTitle => BuyNow ? "Mua ngay" : "Thêm vào giỏ hàng";
    public string ActionHint => BuyNow
        ? "Chọn phiên bản và số lượng để tiếp tục thanh toán."
        : "Chọn phiên bản và số lượng. Bạn vẫn ở danh sách sau khi thêm.";
    public int ProductId => _product?.Id ?? 0;
    public string Name => _product?.Name ?? "Đang tải sản phẩm…";
    public string? ImageUrl => SelectedVariant?.Variant.ImageUrl ?? _product?.ImageUrl;
    public string PriceText => (SelectedVariant?.Variant.Price ?? _product?.MinPrice) is { } price
        ? price.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " ₫" : "Liên hệ";
    public bool HasProduct => _product is not null;
    public bool IsLoading => _isLoading;
    public bool IsSubmitting => _isSubmitting;
    public bool IsBusy => _isLoading || _isSubmitting;
    public bool IsIdle => !IsBusy;
    public bool CanClose => !_isSubmitting;
    public bool CanEdit => HasProduct && !IsBusy && !HasSucceeded;
    public bool HasSucceeded => _hasSucceeded;
    public bool HasError => !string.IsNullOrWhiteSpace(_error);
    public string? ErrorMessage => _error;
    public bool CanRetry => !IsBusy && !HasProduct;
    public bool IsOutOfStock => HasProduct && !Variants.Any(option => option.Variant.Stock > 0);
    public int Stock => SelectedVariant?.Variant.Stock ?? 0;
    public int MaximumQuantity => Math.Max(1, Math.Min(Stock, 999));
    public string SelectionHint => IsOutOfStock ? "Sản phẩm hiện tạm hết hàng."
        : SelectedVariant is null ? "Vui lòng chọn size và màu phù hợp trước khi tiếp tục."
        : Stock > 0 ? $"Còn {Stock} {_product?.Unit} trong kho" : "Phiên bản này đã hết hàng. Hãy chọn phiên bản khác.";
    public bool CanEditQuantity => CanEdit && SelectedVariant is not null && Stock > 0;
    public bool HasValidQuantity => TryQuantity(out _);
    public bool HasQuantityError => CanEditQuantity && !HasValidQuantity;
    public string QuantityValidationMessage => $"Nhập số lượng từ 1 đến {MaximumQuantity}.";
    public bool CanSubmit => CanEditQuantity && HasValidQuantity;
    public bool CanIncrease => CanSubmit && Quantity < MaximumQuantity;
    public bool CanDecrease => CanSubmit && Quantity > 1;

    public ProductVariantOption? SelectedVariant
    {
        get => _selectedVariant;
        set
        {
            if (IsBusy || HasSucceeded) return;
            var normalized = value is not null && Variants.Contains(value) ? value : null;
            if (ReferenceEquals(_selectedVariant, normalized)) return;
            _selectedVariant = normalized;
            _quantity = Math.Clamp(_quantity, 1, MaximumQuantity);
            _quantityText = _quantity.ToString(CultureInfo.InvariantCulture);
            _error = null;
            NotifyAll();
        }
    }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (IsBusy || HasSucceeded) return;
            var normalized = Math.Clamp(value, 1, MaximumQuantity);
            var normalizedText = normalized.ToString(CultureInfo.InvariantCulture);
            if (_quantity == normalized && _quantityText == normalizedText) return;
            _quantity = normalized;
            _quantityText = normalizedText;
            _error = null;
            NotifyAll();
        }
    }

    public string QuantityText
    {
        get => _quantityText;
        set
        {
            if (IsBusy || HasSucceeded) return;
            var raw = value ?? "";
            if (_quantityText == raw) return;
            _quantityText = raw;
            if (TryQuantity(out var quantity)) _quantity = quantity;
            _error = null;
            NotifyAll();
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || HasSucceeded) return;
        _error = null;
        if (productId <= 0)
        {
            _error = "Không tìm thấy sản phẩm.";
            NotifyAll();
            return;
        }
        _isLoading = true;
        NotifyAll();
        try
        {
            var product = await catalog.GetProductAsync(productId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (product.Id != productId || product.Variants is null
                || product.Variants.Any(variant => variant.Id <= 0 || variant.Stock < 0 || variant.Price < 0))
                throw new CatalogException("Dữ liệu sản phẩm chưa hợp lệ. Vui lòng thử lại.");
            Apply(product);
        }
        catch (CatalogException ex) when (!cancellationToken.IsCancellationRequested) { _error = ex.Message; }
        catch (CatalogException) { }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { _isLoading = false; NotifyAll(); }
    }

    // Chỉ thêm vào giỏ hiện hữu. Mở checkout do màn hình gọi quyết định sau khi đóng modal.
    public async Task<bool> SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || HasSucceeded) return false;
        if (!CanSubmit)
        {
            _error = !HasProduct ? "Chưa tải được sản phẩm. Vui lòng thử lại."
                : IsOutOfStock || (SelectedVariant is not null && Stock <= 0) ? "Phiên bản sản phẩm đã hết hàng."
                : SelectedVariant is null ? "Vui lòng chọn size và màu trước khi tiếp tục."
                : QuantityValidationMessage;
            NotifyAll();
            return false;
        }
        var variantId = SelectedVariant!.Id;
        var quantity = Math.Clamp(Quantity, 1, MaximumQuantity);
        _isSubmitting = true;
        _error = null;
        NotifyAll();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await commerce.AddCartItemAsync(variantId, quantity, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _hasSucceeded = true;
            return true;
        }
        catch (CommerceException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _error = ex.Message;
            if (ex.StatusCode == 409)
            {
                try
                {
                    var currentProduct = await catalog.GetProductAsync(productId, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    Apply(currentProduct);
                }
                catch (CatalogException) { }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            }
            return false;
        }
        catch (CommerceException) { return false; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
        finally { _isSubmitting = false; NotifyAll(); }
    }

    private void Apply(ProductDetailDto product)
    {
        var previousVariantId = _selectedVariant?.Id;
        _product = product;
        Variants.Clear();
        foreach (var variant in product.Variants) Variants.Add(new ProductVariantOption(variant));
        _selectedVariant = Variants.FirstOrDefault(option => option.Id == previousVariantId);
        // Nhiều phiên bản phải được người mua chọn rõ; không ngầm thêm size/màu đầu tiên.
        if (_selectedVariant is null && Variants.Count == 1 && Variants[0].Variant.Stock > 0)
            _selectedVariant = Variants[0];
        _quantity = Math.Clamp(_quantity, 1, MaximumQuantity);
        _quantityText = _quantity.ToString(CultureInfo.InvariantCulture);
    }

    private bool TryQuantity(out int quantity)
        => int.TryParse(_quantityText, NumberStyles.None, CultureInfo.InvariantCulture, out quantity)
            && quantity >= 1 && quantity <= MaximumQuantity;

    private void NotifyAll()
    {
        foreach (var name in new[] { nameof(ProductId), nameof(Name), nameof(ImageUrl), nameof(PriceText),
            nameof(HasProduct), nameof(IsLoading), nameof(IsSubmitting), nameof(IsBusy), nameof(IsIdle),
            nameof(CanClose), nameof(CanEdit), nameof(HasSucceeded), nameof(HasError), nameof(ErrorMessage),
            nameof(CanRetry), nameof(IsOutOfStock), nameof(Stock), nameof(MaximumQuantity), nameof(SelectionHint),
            nameof(CanEditQuantity), nameof(HasValidQuantity), nameof(HasQuantityError), nameof(QuantityValidationMessage),
            nameof(CanSubmit), nameof(CanIncrease), nameof(CanDecrease), nameof(SelectedVariant),
            nameof(Quantity), nameof(QuantityText) }) Notify(name);
    }

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
