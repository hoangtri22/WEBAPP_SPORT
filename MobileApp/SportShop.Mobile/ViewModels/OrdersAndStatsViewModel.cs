using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using SportShop.Contracts.Commerce;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

// Dòng sản phẩm trong đơn hàng (hiển thị trên từng thẻ đơn hàng và trong bảng chi tiết Main-Sub).
public sealed class OrderSubLineItem(OrderItemDto dto, int index)
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    public int Index => index;
    public int ProductId => dto.ProductId;
    public string Name => dto.Name;
    public string Variant => string.IsNullOrWhiteSpace(dto.Variant) ? "" : dto.Variant;
    public bool HasVariant => !string.IsNullOrWhiteSpace(dto.Variant);
    public string NameAndVariantText => HasVariant ? $"{dto.Name} · {dto.Variant}" : dto.Name;
    public int Quantity => dto.Quantity;
    public decimal UnitPrice => dto.UnitPrice;
    public string UnitPriceText => dto.UnitPrice.ToString("N0", Vi) + " ₫";
    public string PriceTimesQtyText => $"{dto.UnitPrice.ToString("N0", Vi)} ₫ × {dto.Quantity}";
    public decimal Amount => dto.Amount;
    public string AmountText => dto.Amount.ToString("N0", Vi) + " ₫";
    public string? ImageUrl => dto.ImageUrl;
    public bool HasImage => !string.IsNullOrWhiteSpace(dto.ImageUrl);
}

// Thẻ đơn hàng (đồng bộ 100% với Frontend/SportShop.CustomerWeb/Views/Customer/Orders.cshtml).
public sealed class OrderMasterCard : INotifyPropertyChanged
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private bool _isSelected;

    public OrderDto Dto { get; }
    public IReadOnlyList<OrderSubLineItem> Lines { get; }

    // Tạo thẻ đơn hàng từ OrderDto.
    public OrderMasterCard(OrderDto dto)
    {
        Dto = dto;
        Lines = dto.Items.Select((item, idx) => new OrderSubLineItem(item, idx + 1)).ToList();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id => Dto.Id;
    public string Code => Dto.Code;
    public string DateText => "· " + Dto.OrderedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm");
    public string Status => Dto.Status;
    public string StatusLabel => Dto.StatusLabel;
    public string PaymentStatus => Dto.PaymentStatus;
    public string PaymentStatusLabel => Dto.PaymentStatus == "da_thanh_toan" ? "Đã thanh toán" : "Chưa thanh toán";
    public string PaymentBadgeBgHex => Dto.PaymentStatus == "da_thanh_toan" ? "#DCFCE7" : "#FEF3C7";
    public string PaymentBadgeTextHex => Dto.PaymentStatus == "da_thanh_toan" ? "#166534" : "#92400E";

    public string PaymentMethod => Dto.PaymentMethod;
    public string ReceiverName => Dto.Receiver.Name;
    public string ReceiverPhone => Dto.Receiver.Phone;
    public string ReceiverAddress => Dto.Receiver.Address;
    public string InvoiceNumber => string.IsNullOrWhiteSpace(Dto.InvoiceNumber) ? "Chưa phát hành" : Dto.InvoiceNumber!;
    public bool HasInvoice => !string.IsNullOrWhiteSpace(Dto.InvoiceNumber);
    public string InvoiceLineText => HasInvoice ? $"Hóa đơn: {Dto.InvoiceNumber}" : "";

    public string ReceiverLineText => $"Giao đến: {Dto.Receiver.Name} · {Dto.Receiver.Phone} · {Dto.Receiver.Address}";
    public bool HasShippingTracking => !string.IsNullOrWhiteSpace(Dto.Carrier) && (Dto.Status == "dang_giao" || Dto.Status == "da_giao");
    public string ShippingTrackingLineText => HasShippingTracking
        ? $"Vận chuyển: {Dto.Carrier}{(string.IsNullOrWhiteSpace(Dto.TrackingNumber) ? "" : " · Mã vận đơn " + Dto.TrackingNumber)}"
        : "";

    public string CostBreakdownText => $"Giảm {Dto.Discount.ToString("N0", Vi)} ₫ · Ship {Dto.ShippingFee.ToString("N0", Vi)} ₫ · {Dto.PaymentMethod}";
    public decimal Total => Dto.Total;
    public string TotalText => Dto.Total.ToString("N0", Vi) + " ₫";
    public string TotalLabelText => $"Tổng: {Dto.Total.ToString("N0", Vi)} ₫";
    public string SubtotalText => Dto.Subtotal.ToString("N0", Vi) + " ₫";
    public string DiscountText => Dto.Discount > 0 ? "-" + Dto.Discount.ToString("N0", Vi) + " ₫" : "0 ₫";
    public string ShippingFeeText => Dto.ShippingFee == 0 ? "Miễn phí" : Dto.ShippingFee.ToString("N0", Vi) + " ₫";
    public int ItemCount => Dto.Items.Sum(i => i.Quantity);
    public bool CanCancel => Dto.CanCancel;

    // 4 bước tiến trình giống hệt Views/Customer/Orders.cshtml (Chờ xác nhận -> Đang chuẩn bị -> Đang giao -> Đã giao)
    public int Step => Dto.Status switch
    {
        "cho_xac_nhan" => 0,
        "dang_chuan_bi" => 1,
        "dang_giao" => 2,
        "da_giao" => 3,
        _ => -1
    };

    public bool ShowProgress => Step >= 0;
    public bool ShowCancelledAlert => Step < 0;
    public string CancelledAlertText => string.IsNullOrWhiteSpace(Dto.CancellationReason)
        ? "Đơn đã hủy"
        : $"Đơn đã hủy: {Dto.CancellationReason}";

    public string Step1Icon => Step >= 0 ? "✓" : "1";
    public string Step2Icon => Step >= 1 ? "✓" : "2";
    public string Step3Icon => Step >= 2 ? "✓" : "3";
    public string Step4Icon => Step >= 3 ? "✓" : "4";

    public string Step1BgHex => Step >= 0 ? "#0F766E" : "#E2E8F0";
    public string Step2BgHex => Step >= 1 ? "#0F766E" : "#E2E8F0";
    public string Step3BgHex => Step >= 2 ? "#0F766E" : "#E2E8F0";
    public string Step4BgHex => Step >= 3 ? "#0F766E" : "#E2E8F0";

    public string Step1TextHex => Step >= 0 ? "#FFFFFF" : "#64748B";
    public string Step2TextHex => Step >= 1 ? "#FFFFFF" : "#64748B";
    public string Step3TextHex => Step >= 2 ? "#FFFFFF" : "#64748B";
    public string Step4TextHex => Step >= 3 ? "#FFFFFF" : "#64748B";

    public string Step1LabelHex => Step >= 0 ? "#0F766E" : "#64748B";
    public string Step2LabelHex => Step >= 1 ? "#0F766E" : "#64748B";
    public string Step3LabelHex => Step >= 2 ? "#0F766E" : "#64748B";
    public string Step4LabelHex => Step >= 3 ? "#0F766E" : "#64748B";

    public string StatusColorHex => Dto.Status switch
    {
        "cho_xac_nhan" => "#D97706",
        "dang_chuan_bi" => "#2563EB",
        "dang_giao" => "#7C3AED",
        "da_giao" => "#059669",
        "da_huy" => "#DC2626",
        _ => "#475569"
    };

    public string StatusBgHex => Dto.Status switch
    {
        "cho_xac_nhan" => "#FEF3C7",
        "dang_chuan_bi" => "#DBEAFE",
        "dang_giao" => "#EDE9FE",
        "da_giao" => "#D1FAE5",
        "da_huy" => "#FEE2E2",
        _ => "#F1F5F9"
    };

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderColorHex)));
        }
    }

    public string BorderColorHex => _isSelected ? "#0F766E" : "#E2E8F0";
}

// Dòng điều kiện Tìm kiếm nâng cao trên UI.
public sealed class SearchConditionRow : INotifyPropertyChanged
{
    private LogicalOperatorOption _logic = AdvancedOrderSearch.LogicalOperators[0];
    private SearchFieldOption _field = AdvancedOrderSearch.Fields[0];
    private RelationalOperatorOption _op = AdvancedOrderSearch.TextOperators[0];
    private string _value = "";
    private bool _isFirst;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<LogicalOperatorOption> LogicOptions => AdvancedOrderSearch.LogicalOperators;
    public IReadOnlyList<SearchFieldOption> FieldOptions => AdvancedOrderSearch.Fields;
    public IReadOnlyList<RelationalOperatorOption> OperatorOptions => AdvancedOrderSearch.OperatorsForField(_field.Key);

    public bool IsFirst
    {
        get => _isFirst;
        set
        {
            _isFirst = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFirst)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowLogicPicker)));
        }
    }

    public bool ShowLogicPicker => !_isFirst;

    public LogicalOperatorOption SelectedLogic
    {
        get => _logic;
        set { _logic = value ?? AdvancedOrderSearch.LogicalOperators[0]; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLogic))); }
    }

    public SearchFieldOption SelectedField
    {
        get => _field;
        set
        {
            _field = value ?? AdvancedOrderSearch.Fields[0];
            _op = AdvancedOrderSearch.OperatorsForField(_field.Key)[0];
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedField)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OperatorOptions)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOperator)));
        }
    }

    public RelationalOperatorOption SelectedOperator
    {
        get => _op;
        set { _op = value ?? AdvancedOrderSearch.OperatorsForField(_field.Key)[0]; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOperator))); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
    }

    // Đổi dòng điều kiện thành OrderSearchCondition.
    public OrderSearchCondition ToCondition() =>
        new(_logic.Key, _field.Key, _op.Key, _value);
}

// Tab trạng thái đơn hàng (Tất cả, Chờ xác nhận, Đang chuẩn bị, Đang giao, Đã giao, Đã hủy)
public sealed class OrderStatusTabItem : INotifyPropertyChanged
{
    public string Key { get; }
    public string Label { get; }
    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BgHex)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextHex)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderHex)));
        }
    }

    public string BgHex => _isSelected ? "#0F766E" : "#FFFFFF";
    public string TextHex => _isSelected ? "#FFFFFF" : "#475569";
    public string BorderHex => _isSelected ? "#0F766E" : "#CBD5E1";

    public event PropertyChangedEventHandler? PropertyChanged;

    // Tạo một tab trạng thái.
    public OrderStatusTabItem(string key, string label, bool isSelected = false)
    {
        Key = key;
        Label = label;
        _isSelected = isSelected;
    }
}

// ViewModel cho Đơn hàng của tôi (Views/Customer/Orders.cshtml) — tích hợp bộ nhớ đệm 10 phút, Main-Sub Form, Tìm kiếm nâng cao và Hủy đơn đúng điều kiện.
public sealed class OrdersViewModel : INotifyPropertyChanged
{
    private readonly ICommerceApi _api;
    private readonly AppMemoryCache _cache;
    private IReadOnlyList<OrderDto> _allOrders = [];
    private OrderMasterCard? _selectedOrder;
    private string _searchKeyword = "";
    private string _selectedStatusFilter = "all";
    private bool _showAdvancedSearch;
    private bool _isBusy;
    private bool _usedMemoryCache;
    private string? _errorMessage;
    private string? _statusMessage;

    // Nhận ICommerceApi và AppMemoryCache.
    public OrdersViewModel(ICommerceApi api, AppMemoryCache cache)
    {
        _api = api;
        _cache = cache;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Danh sách tab lọc trạng thái
    public ObservableCollection<OrderStatusTabItem> StatusTabs { get; } =
    [
        new("all", "Tất cả", true),
        new("cho_xac_nhan", "Chờ xác nhận"),
        new("dang_chuan_bi", "Đang chuẩn bị"),
        new("dang_giao", "Đang giao"),
        new("da_giao", "Đã giao"),
        new("da_huy", "Đã hủy"),
    ];

    // Danh sách đơn hàng hiển thị (Main Form)
    public ObservableCollection<OrderMasterCard> Orders { get; } = [];

    // Danh sách dòng hàng CHI_TIET_DON_HANG của đơn đang chọn (Sub Form)
    public ObservableCollection<OrderSubLineItem> SelectedOrderLines { get; } = [];

    // Các dòng điều kiện Tìm kiếm nâng cao
    public ObservableCollection<SearchConditionRow> SearchConditions { get; } = [];

    public IReadOnlyList<OrderDto> AllOrders => _allOrders;

    public OrderMasterCard? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (_selectedOrder == value) return;
            if (_selectedOrder is not null) _selectedOrder.IsSelected = false;
            _selectedOrder = value;
            if (_selectedOrder is not null) _selectedOrder.IsSelected = true;
            SyncSubForm();
            NotifyAll();
        }
    }

    public bool HasSelectedOrder => _selectedOrder is not null;

    // Khi người dùng gõ n từ khóa trong 1 phút, lọc trực tiếp trên biến bộ nhớ _allOrders mà không chạy lại câu truy vấn SQL.
    public string SearchKeyword
    {
        get => _searchKeyword;
        set
        {
            if (_searchKeyword == value) return;
            _searchKeyword = value;
            _usedMemoryCache = true;
            ApplyFilter();
        }
    }

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? "all" : value;
            if (_selectedStatusFilter == next) return;
            _selectedStatusFilter = next;
            foreach (var tab in StatusTabs)
                tab.IsSelected = tab.Key == _selectedStatusFilter;
            _usedMemoryCache = true;
            ApplyFilter();
        }
    }

    public bool ShowAdvancedSearch
    {
        get => _showAdvancedSearch;
        set
        {
            _showAdvancedSearch = value;
            if (_showAdvancedSearch && SearchConditions.Count == 0)
                AddSearchCondition();
            NotifyAll();
        }
    }

    public bool IsBusy => _isBusy;
    public bool IsEmpty => !_isBusy && Orders.Count == 0;
    public bool HasOrders => Orders.Count > 0;
    public bool UsedMemoryCache => _usedMemoryCache;
    public bool HasError => !string.IsNullOrEmpty(_errorMessage);
    public string? ErrorMessage => _errorMessage;
    public bool HasStatus => !string.IsNullOrEmpty(_statusMessage);
    public string? StatusMessage => _statusMessage;

    // Tải lịch sử đơn hàng; nếu trong vòng 10 phút thì tái sử dụng biến bộ nhớ đệm mà không gọi lại API/DB.
    public async Task LoadOrdersAsync(bool forceReload = false, CancellationToken cancellationToken = default)
    {
        if (_isBusy) return;
        _isBusy = true;
        _errorMessage = null;
        if (forceReload) _statusMessage = null;
        NotifyAll();

        try
        {
            var cached = !forceReload ? _cache.Get<IReadOnlyList<OrderDto>>(AppMemoryCache.OrdersKey) : null;
            if (cached is not null)
            {
                _allOrders = cached;
                _usedMemoryCache = true;
            }
            else
            {
                var page = await _api.GetOrdersAsync(page: 1, pageSize: 100, status: null, cancellationToken);
                _allOrders = page.Items;
                _cache.Put(AppMemoryCache.OrdersKey, _allOrders);
                _usedMemoryCache = false;
            }

            ApplyFilter();
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

    // Làm mới ngầm danh sách đơn hàng (không bật IsBusy spinner) phục vụ tự động cập nhật trạng thái đơn định kỳ.
    public async Task<IReadOnlyList<string>> RefreshOrdersSilentlyAsync(CancellationToken cancellationToken = default)
    {
        if (_isBusy) return [];
        try
        {
            var page = await _api.GetOrdersAsync(page: 1, pageSize: 100, status: null, cancellationToken);
            var nextOrders = page.Items;
            var changes = DetectStatusChanges(_allOrders, nextOrders);
            var hasListChanged = changes.Count > 0 || HasOrderDataChanged(_allOrders, nextOrders);

            _allOrders = nextOrders;
            _cache.Put(AppMemoryCache.OrdersKey, _allOrders);
            _usedMemoryCache = false;

            if (changes.Count > 0)
                _statusMessage = string.Join(" ", changes);

            if (hasListChanged)
                ApplyFilter(keepOrderId: _selectedOrder?.Id);

            return changes;
        }
        catch
        {
            // Bỏ qua lỗi mạng tạm thời khi polling ngầm để không làm gián đoạn trải nghiệm người dùng.
            return [];
        }
    }

    // Phát hiện các đơn hàng vừa thay đổi trạng thái xử lý hoặc trạng thái thanh toán (đồng bộ với Views/Customer/Orders.cshtml).
    public static IReadOnlyList<string> DetectStatusChanges(IReadOnlyList<OrderDto> previous, IReadOnlyList<OrderDto> current)
    {
        if (previous.Count == 0 || current.Count == 0) return [];
        var prevById = previous.ToDictionary(o => o.Id);
        var notifications = new List<string>();

        foreach (var curr in current)
        {
            if (!prevById.TryGetValue(curr.Id, out var prev))
                continue;

            if (!string.Equals(prev.Status, curr.Status, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(curr.Status, "da_giao", StringComparison.OrdinalIgnoreCase))
                {
                    notifications.Add($"Đơn hàng {curr.Code} đã được giao. Cảm ơn bạn đã mua sắm tại SPORTSHOP!");
                }
                else
                {
                    notifications.Add($"Đơn hàng {curr.Code} đã chuyển sang trạng thái \"{curr.StatusLabel}\".");
                }
            }
            else if (!string.Equals(prev.PaymentStatus, curr.PaymentStatus, StringComparison.OrdinalIgnoreCase))
            {
                var payLabel = curr.PaymentStatus == "da_thanh_toan" ? "Đã thanh toán" : "Chưa thanh toán";
                notifications.Add($"Đơn hàng {curr.Code} cập nhật thanh toán: \"{payLabel}\".");
            }
        }

        return notifications;
    }

    private static bool HasOrderDataChanged(IReadOnlyList<OrderDto> previous, IReadOnlyList<OrderDto> current)
    {
        if (previous.Count != current.Count) return true;
        for (int i = 0; i < previous.Count; i++)
        {
            var a = previous[i];
            var b = current[i];
            if (a.Id != b.Id ||
                a.Status != b.Status ||
                a.PaymentStatus != b.PaymentStatus ||
                a.Carrier != b.Carrier ||
                a.TrackingNumber != b.TrackingNumber ||
                a.InvoiceNumber != b.InvoiceNumber)
            {
                return true;
            }
        }
        return false;
    }

    // Hủy đơn hàng đúng điều kiện (chỉ đơn cho_xac_nhan và chua_thanh_toan mới có CanCancel == true).
    public async Task<bool> CancelSelectedOrderAsync(CancellationToken cancellationToken = default) =>
        _selectedOrder is not null && await CancelOrderAsync(_selectedOrder, cancellationToken);

    // Hủy đơn nếu đơn còn cho phép và cập nhật lại danh sách.
    public async Task<bool> CancelOrderAsync(OrderMasterCard card, CancellationToken cancellationToken = default)
    {
        if (_isBusy) return false;
        if (!card.CanCancel)
        {
            _errorMessage = "Đơn hàng này không còn ở trạng thái Chờ xác nhận (hoặc đã thanh toán) nên không thể tự hủy.";
            NotifyAll();
            return false;
        }

        _isBusy = true;
        _errorMessage = null;
        _statusMessage = null;
        NotifyAll();
        try
        {
            var updated = await _api.CancelOrderAsync(card.Id, cancellationToken);
            _allOrders = _allOrders.Select(o => o.Id == updated.Id ? updated : o).ToList();
            _cache.Put(AppMemoryCache.OrdersKey, _allOrders);
            _statusMessage = $"Đã hủy đơn hàng {updated.Code}.";
            ApplyFilter(keepOrderId: updated.Id);
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

    // Thêm một dòng điều kiện tìm kiếm.
    public void AddSearchCondition()
    {
        var row = new SearchConditionRow { IsFirst = SearchConditions.Count == 0 };
        SearchConditions.Add(row);
        NotifyAll();
    }

    // Xóa một dòng điều kiện tìm kiếm.
    public void RemoveSearchCondition(SearchConditionRow row)
    {
        SearchConditions.Remove(row);
        for (int i = 0; i < SearchConditions.Count; i++)
            SearchConditions[i].IsFirst = i == 0;
        ApplyFilter();
    }

    // Xóa hết điều kiện tìm kiếm.
    public void ClearSearchConditions()
    {
        SearchConditions.Clear();
        _searchKeyword = "";
        SelectedStatusFilter = "all";
    }

    // Lọc danh sách đơn hàng ngay trên biến bộ nhớ _allOrders (không chạy lại câu truy vấn SQL).
    public void ApplyFilter(int? keepOrderId = null)
    {
        var targetId = keepOrderId ?? _selectedOrder?.Id;
        var conditions = SearchConditions.Select(r => r.ToCondition()).ToList();

        var filtered = AdvancedOrderSearch.Filter(_allOrders, _searchKeyword, _selectedStatusFilter, conditions);

        Orders.Clear();
        foreach (var dto in filtered)
            Orders.Add(new OrderMasterCard(dto));

        var match = Orders.FirstOrDefault(o => o.Id == targetId) ?? Orders.FirstOrDefault();
        SelectedOrder = match;
        NotifyAll();
    }

    // Dựng lại danh sách sản phẩm của đơn đang chọn.
    private void SyncSubForm()
    {
        SelectedOrderLines.Clear();
        if (_selectedOrder is null) return;
        foreach (var line in _selectedOrder.Lines)
            SelectedOrderLines.Add(line);
    }

    // Báo giao diện cập nhật các thuộc tính.
    private void NotifyAll()
    {
        foreach (var prop in new[]
        {
            nameof(SelectedOrder), nameof(HasSelectedOrder), nameof(SearchKeyword),
            nameof(SelectedStatusFilter), nameof(ShowAdvancedSearch), nameof(IsBusy),
            nameof(IsEmpty), nameof(HasOrders), nameof(UsedMemoryCache),
            nameof(HasError), nameof(ErrorMessage), nameof(HasStatus), nameof(StatusMessage)
        })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
