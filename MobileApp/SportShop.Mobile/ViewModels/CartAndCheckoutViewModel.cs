using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using SportShop.Contracts.Commerce;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

public sealed class CartLineItem(CartItemDto dto)
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    public int VariantId => dto.VariantId;
    public int ProductId => dto.ProductId;
    public string Name => dto.Name;
    public string? Size => dto.Size;
    public string? Color => dto.Color;
    public string VariantLabel => string.Join(" / ", new[] { dto.Size, dto.Color }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public bool HasVariantLabel => !string.IsNullOrWhiteSpace(VariantLabel);
    public string SummaryLineText => $"{dto.Name}{(HasVariantLabel ? " (" + VariantLabel + ")" : "")} × {dto.Quantity}";
    public string? ImageUrl => dto.ImageUrl;
    public bool HasImage => !string.IsNullOrWhiteSpace(dto.ImageUrl);
    public decimal UnitPrice => dto.UnitPrice;
    public string UnitPriceText => dto.UnitPrice.ToString("N0", Vi) + " ₫";
    public int Quantity => dto.Quantity;
    public int Stock => dto.Stock;
    public decimal Amount => dto.Amount;
    public string AmountText => dto.Amount.ToString("N0", Vi) + " ₫";
    public bool Available => dto.Available;
    public bool HasWarning => !dto.Available;
    public string StockWarningText => dto.Stock <= 0
        ? "Sản phẩm này hiện đã hết hàng"
        : $"Chỉ còn {dto.Stock} sản phẩm trong kho";
}

public sealed class PaymentMethodOption(CheckoutPaymentMethodDto dto, bool isSelected)
{
    public CheckoutPaymentMethodDto Dto => dto;
    public int Id => dto.Id;
    public string Name => dto.Name;
    public bool IsSelected => isSelected;
    public string RadioText => isSelected ? "◉" : "○";
    public string BorderColorHex => isSelected ? "#0F766E" : "#E2E8F0";
    public string BgColorHex => isSelected ? "#F0FDFA" : "#FFFFFF";
}

public sealed class VoucherOptionItem
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    public EligibleVoucherDto? Dto { get; }
    public string Code { get; }
    public bool IsNone => Dto is null;
    public bool HasCode => !IsNone;
    public string Title { get; }
    public string Subtitle { get; }
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
    public string DiscountText { get; }
    public bool HasDiscount => !IsNone;
    public bool IsSelected { get; }
    public string RadioText => IsSelected ? "◉" : "○";
    public string BorderColorHex => IsSelected ? "#0F766E" : "#E2E8F0";
    public string BgColorHex => IsSelected ? "#F0FDFA" : "#FFFFFF";

    public VoucherOptionItem(EligibleVoucherDto? dto, bool isSelected)
    {
        Dto = dto;
        IsSelected = isSelected;
        if (dto is null)
        {
            Code = "";
            Title = "Không dùng mã";
            Subtitle = "";
            DiscountText = "";
        }
        else
        {
            Code = dto.Code;
            Title = dto.Name;
            var maxPart = dto.MaximumDiscount is decimal m ? $", tối đa {m.ToString("N0", Vi)} ₫" : "";
            var tierPart = string.IsNullOrWhiteSpace(dto.MembershipTier) ? "" : $" · Hạng {dto.MembershipTier}";
            Subtitle = $"Giảm {dto.Percent:0.##}%{maxPart}{tierPart} · HSD {dto.ExpiresAt.LocalDateTime:dd/MM/yyyy}";
            DiscountText = "-" + dto.Discount.ToString("N0", Vi) + " ₫";
        }
    }
}

public sealed class PlacedOrderLineItem(OrderItemDto dto)
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    public string SummaryText => $"{dto.Name}{(string.IsNullOrWhiteSpace(dto.Variant) ? "" : " (" + dto.Variant + ")")} × {dto.Quantity}";
    public string AmountText => dto.Amount.ToString("N0", Vi) + " ₫";
}

// ViewModel cho Giỏ hàng & Thanh toán (Checkout) — đồng bộ 100% chức năng và hiển thị với SportShop.CustomerWeb (Views/Cart/Index.cshtml, Checkout.cshtml, Done.cshtml).
public sealed class CartViewModel(ICommerceApi api) : INotifyPropertyChanged
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    public const decimal FreeShipFrom = 500_000m;
    public const decimal StandardShipFee = 30_000m;

    private bool _isBusy;
    private string? _errorMessage;
    private string? _statusMessage;
    private decimal _subtotal;
    private int _totalQuantity;

    // Trạng thái Checkout (Thanh toán)
    private string _receiverName = "";
    private string _receiverPhone = "";
    private string _receiverAddress = "";
    private CheckoutPaymentMethodDto? _selectedPaymentMethod;
    private EligibleVoucherDto? _selectedVoucher;
    private string _voucherCodeInput = "";
    private string? _voucherError;
    private decimal _discount;
    private decimal _shippingFee;
    private decimal _total;
    private bool _canPlaceOrder;
    private string? _pendingIdempotencyKey;
    private string? _pendingSignature;
    private OrderDto? _lastPlacedOrder;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CartLineItem> Items { get; } = [];
    public ObservableCollection<CheckoutPaymentMethodDto> PaymentMethods { get; } = [];
    public ObservableCollection<PaymentMethodOption> PaymentMethodOptions { get; } = [];
    public ObservableCollection<EligibleVoucherDto> EligibleVouchers { get; } = [];
    public ObservableCollection<VoucherOptionItem> VoucherOptions { get; } = [];
    public ObservableCollection<PlacedOrderLineItem> PlacedOrderLines { get; } = [];

    public bool IsBusy => _isBusy;
    public bool IsEmpty => !_isBusy && Items.Count == 0;
    public bool HasItems => Items.Count > 0;
    public bool HasStockWarning => Items.Any(i => !i.Available);
    public bool CanProceedToCheckout => HasItems && !HasStockWarning;
    public int TotalQuantity => _totalQuantity;
    public string OrderSummaryHeaderText => $"Đơn hàng ({_totalQuantity} sản phẩm)";
    public decimal Subtotal => _subtotal;
    public string SubtotalText => _subtotal.ToString("N0", Vi) + " ₫";

    public decimal EstimatedShippingFee => _subtotal <= 0 ? 0m : (_subtotal >= FreeShipFrom ? 0m : StandardShipFee);
    public string EstimatedShippingText => EstimatedShippingFee == 0 ? "Miễn phí" : EstimatedShippingFee.ToString("N0", Vi) + " ₫";
    public decimal EstimatedTotal => _subtotal + EstimatedShippingFee;
    public string EstimatedTotalText => EstimatedTotal.ToString("N0", Vi) + " ₫";

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);
    public string? ErrorMessage => _errorMessage;
    public bool HasStatus => !string.IsNullOrEmpty(_statusMessage);
    public string? StatusMessage => _statusMessage;

    // Các thuộc tính của màn hình Checkout.cshtml
    public string ReceiverName
    {
        get => _receiverName;
        set { _receiverName = value; ResetIdempotencyIfChanged(); NotifyAll(); }
    }

    public string ReceiverPhone
    {
        get => _receiverPhone;
        set { _receiverPhone = value; ResetIdempotencyIfChanged(); NotifyAll(); }
    }

    public string ReceiverAddress
    {
        get => _receiverAddress;
        set { _receiverAddress = value; ResetIdempotencyIfChanged(); NotifyAll(); }
    }

    public CheckoutPaymentMethodDto? SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set
        {
            _selectedPaymentMethod = value;
            RebuildPaymentMethodOptions();
            ResetIdempotencyIfChanged();
            NotifyAll();
        }
    }

    public EligibleVoucherDto? SelectedVoucher
    {
        get => _selectedVoucher;
        set
        {
            _selectedVoucher = value;
            _voucherCodeInput = value?.Code ?? "";
            RebuildVoucherOptions();
            ResetIdempotencyIfChanged();
            NotifyAll();
        }
    }

    public string VoucherCodeInput
    {
        get => _voucherCodeInput;
        set { _voucherCodeInput = value; ResetIdempotencyIfChanged(); NotifyAll(); }
    }

    public bool HasVouchers => EligibleVouchers.Count > 0;
    public bool HasNoVouchers => EligibleVouchers.Count == 0;
    public bool HasVoucherError => !string.IsNullOrWhiteSpace(_voucherError);
    public string? VoucherError => _voucherError;
    public bool HasAppliedVoucher => _discount > 0 && !string.IsNullOrWhiteSpace(_voucherCodeInput);
    public string AppliedVoucherText => HasAppliedVoucher
        ? $"Đã áp dụng mã {_voucherCodeInput.Trim()}, giảm {_discount.ToString("N0", Vi)} ₫."
        : "";

    public decimal Discount => _discount;
    public string DiscountText => "-" + _discount.ToString("N0", Vi) + " ₫";
    public decimal ShippingFee => _shippingFee;
    public string ShippingFeeText => _shippingFee == 0 ? "Miễn phí" : _shippingFee.ToString("N0", Vi) + " ₫";
    public decimal Total => _total;
    public string TotalText => _total.ToString("N0", Vi) + " ₫";
    public bool CanPlaceOrder => !_isBusy && _canPlaceOrder && Items.Count > 0 && !HasStockWarning;

    // Trạng thái màn hình Đặt hàng thành công (Done.cshtml)
    public OrderDto? LastPlacedOrder => _lastPlacedOrder;
    public bool HasPlacedOrder => _lastPlacedOrder is not null;
    public bool ShowCheckoutForm => _lastPlacedOrder is null;
    public string PlacedOrderCode => _lastPlacedOrder?.Code ?? "";
    public string PlacedOrderMessage => _lastPlacedOrder is null
        ? ""
        : $"Mã đơn hàng của bạn là {_lastPlacedOrder.Code}. Cửa hàng sẽ xác nhận và giao hàng cho {_lastPlacedOrder.Receiver.Name}, bạn theo dõi trạng thái trong mục \"Đơn hàng của tôi\".";
    public string PlacedOrderPaymentMethod => _lastPlacedOrder?.PaymentMethod ?? "";
    public string PlacedOrderTotalText => (_lastPlacedOrder?.Total ?? 0m).ToString("N0", Vi) + " ₫";

    // Tải giỏ hàng từ GET /api/v1/cart.
    public async Task LoadCartAsync(CancellationToken cancellationToken = default)
    {
        if (_isBusy) return;
        _isBusy = true;
        _errorMessage = null;
        NotifyAll();
        try
        {
            var cart = await api.GetCartAsync(cancellationToken);
            ApplyCartDto(cart);
        }
        catch (CommerceException ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            NotifyAll();
        }
    }

    // Thêm sản phẩm vào giỏ qua POST /api/v1/cart/items.
    public async Task<bool> AddItemAsync(int variantId, int quantity = 1, CancellationToken cancellationToken = default)
    {
        if (variantId <= 0 || quantity <= 0)
        {
            _errorMessage = "Thông tin sản phẩm hoặc số lượng không hợp lệ.";
            NotifyAll();
            return false;
        }
        _isBusy = true;
        _errorMessage = null;
        _statusMessage = null;
        NotifyAll();
        try
        {
            var cart = await api.AddCartItemAsync(variantId, Math.Clamp(quantity, 1, 999), cancellationToken);
            ApplyCartDto(cart);
            _statusMessage = "Đã thêm vào giỏ hàng.";
            return true;
        }
        catch (CommerceException ex)
        {
            _errorMessage = ex.Message;
            return false;
        }
        finally
        {
            _isBusy = false;
            NotifyAll();
        }
    }

    // Tăng/giảm số lượng dòng giỏ qua PUT /api/v1/cart/items/{variantId} hoặc xóa nếu <= 0.
    public async Task ChangeQuantityAsync(CartLineItem item, int delta, CancellationToken cancellationToken = default)
    {
        if (_isBusy) return;
        var nextQty = item.Quantity + delta;
        if (nextQty <= 0)
        {
            await RemoveItemAsync(item, cancellationToken);
            return;
        }
        if (item.Stock > 0 && nextQty > item.Stock)
        {
            _errorMessage = $"\"{item.Name}\" chỉ còn {item.Stock} sản phẩm trong kho.";
            nextQty = item.Stock;
        }

        _isBusy = true;
        _errorMessage = null;
        _statusMessage = null;
        NotifyAll();
        try
        {
            var cart = await api.SetCartItemQuantityAsync(item.VariantId, nextQty, cancellationToken);
            ApplyCartDto(cart);
        }
        catch (CommerceException ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            NotifyAll();
        }
    }

    // Xóa một sản phẩm khỏi giỏ qua DELETE /api/v1/cart/items/{variantId}.
    public async Task RemoveItemAsync(CartLineItem item, CancellationToken cancellationToken = default)
    {
        if (_isBusy) return;
        _isBusy = true;
        _errorMessage = null;
        _statusMessage = null;
        NotifyAll();
        try
        {
            var cart = await api.RemoveCartItemAsync(item.VariantId, cancellationToken);
            ApplyCartDto(cart);
        }
        catch (CommerceException ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            NotifyAll();
        }
    }

    // Tải dữ liệu màn hình Checkout từ GET /api/v1/checkout.
    public async Task LoadCheckoutAsync(CancellationToken cancellationToken = default)
    {
        if (_isBusy) return;
        _isBusy = true;
        _errorMessage = null;
        _statusMessage = null;
        _voucherError = null;
        _lastPlacedOrder = null;
        // Mỗi lần vào trang thanh toán bắt đầu "Không dùng mã" như web, tránh giữ lại mã của đơn trước
        // (vd. mã khách mới đã dùng ở lần mua đầu sẽ chặn đơn tiếp theo).
        _voucherCodeInput = "";
        _selectedVoucher = null;
        _discount = 0m;
        PlacedOrderLines.Clear();
        NotifyAll();
        try
        {
            var checkout = await api.GetCheckoutAsync(cancellationToken);
            ApplyCheckoutDto(checkout, overwriteReceiver: true);
        }
        catch (CommerceException ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            NotifyAll();
        }
    }

    // Chọn một phương thức thanh toán trên CheckoutPage.
    public void SelectPaymentMethod(PaymentMethodOption option)
    {
        SelectedPaymentMethod = option.Dto;
    }

    // Chọn một mã giảm giá (hoặc "Không dùng mã") và tự động tính lại tổng tiền giống hệt script trên Checkout.cshtml.
    public async Task SelectVoucherOptionAsync(VoucherOptionItem option, CancellationToken cancellationToken = default)
    {
        _selectedVoucher = option.Dto;
        _voucherCodeInput = option.Dto?.Code ?? "";
        RebuildVoucherOptions();
        await ApplyVoucherQuoteAsync(cancellationToken);
    }

    // Tính lại báo giá (tạm tính, giảm giá voucher, phí ship, tổng thanh toán) qua POST /api/v1/checkout/quote.
    public async Task ApplyVoucherQuoteAsync(CancellationToken cancellationToken = default)
    {
        if (_isBusy) return;
        _isBusy = true;
        _errorMessage = null;
        _voucherError = null;
        NotifyAll();
        try
        {
            var code = string.IsNullOrWhiteSpace(_voucherCodeInput) ? null : _voucherCodeInput.Trim();
            var quote = await api.QuoteCheckoutAsync(code, cancellationToken);
            ApplyCheckoutDto(quote, overwriteReceiver: false);
        }
        catch (CommerceException ex)
        {
            _voucherError = ex.Message;
            // Mã không áp dụng được: hiện lỗi dưới ô mã và quay về "Không dùng mã" để không chặn nút Đặt hàng.
            _voucherCodeInput = "";
            _selectedVoucher = null;
            _discount = 0m;
            _total = _subtotal + EstimatedShippingFee;
            RebuildVoucherOptions();
        }
        finally
        {
            _isBusy = false;
            NotifyAll();
        }
    }

    // Đặt hàng qua POST /api/v1/orders kèm Idempotency-Key; thành công chuyển sang trạng thái Done.cshtml.
    public async Task<OrderDto?> PlaceOrderAsync(CancellationToken cancellationToken = default)
    {
        if (_isBusy) return null;

        var name = _receiverName.Trim();
        var phone = _receiverPhone.Trim();
        var address = _receiverAddress.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            _errorMessage = "Vui lòng nhập họ và tên người nhận.";
            NotifyAll();
            return null;
        }
        if (!Regex.IsMatch(phone, @"^0\d{9}$"))
        {
            _errorMessage = "Số điện thoại người nhận phải gồm 10 chữ số và bắt đầu bằng số 0.";
            NotifyAll();
            return null;
        }
        if (string.IsNullOrWhiteSpace(address))
        {
            _errorMessage = "Vui lòng nhập địa chỉ giao hàng.";
            NotifyAll();
            return null;
        }
        if (_selectedPaymentMethod is null)
        {
            _errorMessage = "Vui lòng chọn phương thức thanh toán.";
            NotifyAll();
            return null;
        }

        var voucher = string.IsNullOrWhiteSpace(_voucherCodeInput) ? null : _voucherCodeInput.Trim();
        var signature = $"{name}|{phone}|{address}|{_selectedPaymentMethod.Id}|{voucher}";
        if (_pendingSignature != signature || string.IsNullOrEmpty(_pendingIdempotencyKey))
        {
            _pendingSignature = signature;
            _pendingIdempotencyKey = $"order-{Guid.NewGuid():N}";
        }

        _isBusy = true;
        _errorMessage = null;
        _statusMessage = null;
        NotifyAll();
        try
        {
            var request = new PlaceOrderRequest(name, phone, address, _selectedPaymentMethod.Id, voucher);
            var order = await api.PlaceOrderAsync(request, _pendingIdempotencyKey!, cancellationToken);
            _lastPlacedOrder = order;
            PlacedOrderLines.Clear();
            foreach (var line in order.Items)
                PlacedOrderLines.Add(new PlacedOrderLineItem(line));
            _pendingIdempotencyKey = null;
            _pendingSignature = null;
            _voucherCodeInput = "";
            _selectedVoucher = null;
            _discount = 0m;
            Items.Clear();
            _subtotal = 0m;
            _totalQuantity = 0;
            return order;
        }
        catch (CommerceException ex)
        {
            _errorMessage = ex.Message;
            return null;
        }
        finally
        {
            _isBusy = false;
            NotifyAll();
        }
    }

    private void ApplyCartDto(CartDto cart)
    {
        Items.Clear();
        foreach (var item in cart.Items)
            Items.Add(new CartLineItem(item));
        _totalQuantity = cart.TotalQuantity;
        _subtotal = cart.Subtotal;
        _shippingFee = EstimatedShippingFee;
        _total = Math.Max(0m, _subtotal - _discount) + _shippingFee;
        _canPlaceOrder = Items.Count > 0 && Items.All(i => i.Available);
    }

    private void ApplyCheckoutDto(CheckoutDto checkout, bool overwriteReceiver)
    {
        ApplyCartDto(checkout.Cart);
        if (overwriteReceiver)
        {
            _receiverName = checkout.Receiver.Name;
            _receiverPhone = checkout.Receiver.Phone;
            _receiverAddress = checkout.Receiver.Address;
        }

        var prevPaymentId = _selectedPaymentMethod?.Id;
        PaymentMethods.Clear();
        foreach (var pm in checkout.PaymentMethods)
            PaymentMethods.Add(pm);
        _selectedPaymentMethod = PaymentMethods.FirstOrDefault(p => p.Id == prevPaymentId) ?? PaymentMethods.FirstOrDefault();
        RebuildPaymentMethodOptions();

        var prevVoucherCode = _voucherCodeInput?.Trim();
        EligibleVouchers.Clear();
        foreach (var v in checkout.Vouchers)
            EligibleVouchers.Add(v);
        _selectedVoucher = !string.IsNullOrWhiteSpace(prevVoucherCode)
            ? EligibleVouchers.FirstOrDefault(v => string.Equals(v.Code, prevVoucherCode, StringComparison.OrdinalIgnoreCase))
            : null;
        // Mã cũ không còn nằm trong danh sách mã dùng được (vd. mã khách mới sau lần mua đầu) thì bỏ, không gửi lên khi đặt hàng.
        if (_selectedVoucher is null && checkout.Discount <= 0) _voucherCodeInput = "";
        RebuildVoucherOptions();

        _discount = checkout.Discount;
        _shippingFee = checkout.ShippingFee;
        _total = checkout.Total;
        _canPlaceOrder = checkout.CanPlaceOrder;
    }

    private void RebuildPaymentMethodOptions()
    {
        PaymentMethodOptions.Clear();
        foreach (var pm in PaymentMethods)
            PaymentMethodOptions.Add(new PaymentMethodOption(pm, _selectedPaymentMethod?.Id == pm.Id));
    }

    private void RebuildVoucherOptions()
    {
        VoucherOptions.Clear();
        if (EligibleVouchers.Count == 0) return;
        var chosen = _voucherCodeInput?.Trim() ?? "";
        VoucherOptions.Add(new VoucherOptionItem(null, string.IsNullOrEmpty(chosen)));
        foreach (var v in EligibleVouchers)
        {
            var isSelected = string.Equals(chosen, v.Code, StringComparison.OrdinalIgnoreCase);
            VoucherOptions.Add(new VoucherOptionItem(v, isSelected));
        }
    }

    private void ResetIdempotencyIfChanged()
    {
        _pendingIdempotencyKey = null;
        _pendingSignature = null;
    }

    private void NotifyAll()
    {
        foreach (var prop in new[]
        {
            nameof(IsBusy), nameof(IsEmpty), nameof(HasItems), nameof(HasStockWarning),
            nameof(CanProceedToCheckout), nameof(TotalQuantity), nameof(OrderSummaryHeaderText),
            nameof(Subtotal), nameof(SubtotalText), nameof(EstimatedShippingFee),
            nameof(EstimatedShippingText), nameof(EstimatedTotal), nameof(EstimatedTotalText),
            nameof(HasError), nameof(ErrorMessage), nameof(HasStatus), nameof(StatusMessage),
            nameof(ReceiverName), nameof(ReceiverPhone), nameof(ReceiverAddress),
            nameof(SelectedPaymentMethod), nameof(SelectedVoucher), nameof(VoucherCodeInput),
            nameof(HasVouchers), nameof(HasNoVouchers), nameof(HasVoucherError), nameof(VoucherError),
            nameof(HasAppliedVoucher), nameof(AppliedVoucherText),
            nameof(Discount), nameof(DiscountText), nameof(ShippingFee), nameof(ShippingFeeText),
            nameof(Total), nameof(TotalText), nameof(CanPlaceOrder),
            nameof(LastPlacedOrder), nameof(HasPlacedOrder), nameof(ShowCheckoutForm),
            nameof(PlacedOrderCode), nameof(PlacedOrderMessage),
            nameof(PlacedOrderPaymentMethod), nameof(PlacedOrderTotalText)
        })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
