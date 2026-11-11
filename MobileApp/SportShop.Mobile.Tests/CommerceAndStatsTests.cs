using SportShop.Contracts.Commerce;
using SportShop.Contracts.Common;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

// Test đơn hàng, tìm kiếm nâng cao, giỏ hàng và thanh toán của app.
public sealed class CommerceAndStatsTests
{
    // Kiểm tra gõ nhiều từ khóa trong 10 phút dùng lại bộ nhớ đệm, không gọi lại CSDL.
    [Fact]
    public async Task TypingMultipleKeywordsWithinTenMinutesReusesMemoryWithoutQueryingDatabaseAgain()
    {
        long fakeClockMs = 1_000;
        var cache = new AppMemoryCache(() => fakeClockMs);
        var fakeApi = new FakeCommerceApi();
        var vm = new OrdersViewModel(fakeApi, cache);

        await vm.LoadOrdersAsync();
        Assert.Equal(1, fakeApi.OrderListCalls);
        Assert.Equal(3, vm.Orders.Count);

        fakeClockMs += 15_000;
        vm.SearchKeyword = "Pegasus";
        Assert.Single(vm.Orders);
        Assert.Equal("DH00001", vm.Orders[0].Code);

        fakeClockMs += 15_000;
        vm.SearchKeyword = "Yonex";
        Assert.Single(vm.Orders);
        Assert.Equal("DH00002", vm.Orders[0].Code);

        fakeClockMs += 15_000;
        vm.SearchKeyword = "Nguyễn Minh An";
        Assert.Equal(2, vm.Orders.Count);

        fakeClockMs += 10_000;
        vm.SearchKeyword = "";
        Assert.Equal(3, vm.Orders.Count);
        Assert.True(vm.UsedMemoryCache);
        Assert.Equal(1, fakeApi.OrderListCalls);

        await vm.LoadOrdersAsync();
        Assert.Equal(1, fakeApi.OrderListCalls);

        fakeClockMs += (long)TimeSpan.FromMinutes(10).TotalMilliseconds + 1;
        await vm.LoadOrdersAsync();
        Assert.Equal(2, fakeApi.OrderListCalls);
    }

    // Kiểm tra danh sách đơn được tải lại ngay sau InvalidateCommerce(), là lệnh CommerceApi gọi khi đặt hoặc hủy đơn.
    [Fact]
    public async Task OrdersReloadAfterCommerceCacheIsInvalidated()
    {
        var cache = new AppMemoryCache();
        var fakeApi = new FakeCommerceApi();
        var vm = new OrdersViewModel(fakeApi, cache);

        await vm.LoadOrdersAsync();
        await vm.LoadOrdersAsync();
        Assert.Equal(1, fakeApi.OrderListCalls);

        cache.InvalidateCommerce();
        await vm.LoadOrdersAsync();
        Assert.Equal(2, fakeApi.OrderListCalls);
    }

    // Kiểm tra form Master-Sub đồng bộ dòng đơn và chỉ hủy được đơn đủ điều kiện.
    [Fact]
    public async Task MainSubFormSynchronizesOrderLinesAndSupportsConditionalOrderCancellation()
    {
        var cache = new AppMemoryCache();
        var fakeApi = new FakeCommerceApi();
        var vm = new OrdersViewModel(fakeApi, cache);

        await vm.LoadOrdersAsync();

        Assert.NotNull(vm.SelectedOrder);
        Assert.Equal("DH00001", vm.SelectedOrder!.Code);
        Assert.Equal(2, vm.SelectedOrderLines.Count);
        Assert.Equal("Giày chạy bộ Nike Pegasus 41", vm.SelectedOrderLines[0].Name);

        vm.SelectedOrder = vm.Orders[1];
        Assert.Single(vm.SelectedOrderLines);
        Assert.Equal("Vợt cầu lông Yonex Astrox 99", vm.SelectedOrderLines[0].Name);
        Assert.False(vm.SelectedOrder.CanCancel);

        var cancelDelivered = await vm.CancelSelectedOrderAsync();
        Assert.False(cancelDelivered);
        Assert.NotNull(vm.ErrorMessage);

        vm.SelectedOrder = vm.Orders[0];
        Assert.True(vm.SelectedOrder.CanCancel);
        var cancelPending = await vm.CancelSelectedOrderAsync();
        Assert.True(cancelPending);
        Assert.Equal("da_huy", vm.SelectedOrder.Status);
        Assert.False(vm.SelectedOrder.CanCancel);
    }

    // Kiểm tra tìm kiếm nâng cao với toán tử so sánh, logic và thứ tự ưu tiên.
    [Fact]
    public void AdvancedOrderSearchSupportsRelationalAndLogicalOperatorsWithPrecedence()
    {
        var orders = FakeCommerceApi.CreateSampleOrders();

        var conds = new List<OrderSearchCondition>
        {
            new("and", "status", "eq", "da_giao"),
            new("and", "total", "gt", "1000000"),
            new("or", "code", "eq", "DH00003")
        };

        var matched = AdvancedOrderSearch.Filter(orders, null, null, conds);
        Assert.Equal(2, matched.Count);
        Assert.Contains(matched, o => o.Code == "DH00002");
        Assert.Contains(matched, o => o.Code == "DH00003");
    }

    // Kiểm tra luồng giỏ, thanh toán, đặt đơn giống web khách.
    [Fact]
    public async Task CartCheckoutAndOrderPlacementFlowMatchesWebCustomerBehavior()
    {
        var fakeApi = new FakeCommerceApi();
        var cartVm = new CartViewModel(fakeApi);

        await cartVm.LoadCheckoutAsync();
        Assert.Equal(2, cartVm.Items.Count);
        Assert.Equal("Nguyễn Minh An", cartVm.ReceiverName);
        Assert.True(cartVm.HasVouchers);

        // Chọn mã giảm giá RUN15
        var promoOpt = cartVm.VoucherOptions.First(v => v.Code == "RUN15");
        await cartVm.SelectVoucherOptionAsync(promoOpt);
        Assert.True(cartVm.Discount > 0);

        // Đặt hàng kèm Idempotency-Key -> chuyển sang trạng thái Đặt hàng thành công (Done.cshtml)
        var order = await cartVm.PlaceOrderAsync();
        Assert.NotNull(order);
        Assert.True(cartVm.HasPlacedOrder);
        Assert.Equal("DH00001", cartVm.PlacedOrderCode);
    }

    // Kiểm tra tự áp dụng khuyến mãi theo giá trị đơn (OrderPromo) khi chưa nhập voucher và gợi ý mua thêm (Upsell).
    [Fact]
    public async Task AutoOrderPromotionAndCartUpsellWorkWhenNoVoucherIsSelected()
    {
        var fakeApi = new FakeCommerceApi();
        var cartVm = new CartViewModel(fakeApi);

        await cartVm.LoadCartAsync();
        Assert.True(cartVm.HasCartOrderPromo);
        Assert.Equal("Ưu đãi đơn từ 2 triệu", cartVm.CartOrderPromoName);
        Assert.Equal(170_000m, cartVm.CartOrderPromoDiscount);
        Assert.Equal(3_230_000m, cartVm.EstimatedTotal);

        Assert.True(cartVm.HasUpsell);
        Assert.Equal(2, cartVm.UpsellItems.Count);
        Assert.Contains("Mua thêm 1.600.000", cartVm.UpsellHeaderText);
        Assert.True(cartVm.UpsellItems[0].CanQuickAdd);
        Assert.Equal("Thêm vào giỏ", cartVm.UpsellItems[0].ActionButtonText);
        Assert.False(cartVm.UpsellItems[1].CanQuickAdd);
        Assert.Equal("Chọn mua", cartVm.UpsellItems[1].ActionButtonText);

        var quickAdded = await cartVm.AddUpsellItemAsync(cartVm.UpsellItems[0]);
        Assert.True(quickAdded);

        var multiVariantQuickAdd = await cartVm.AddUpsellItemAsync(cartVm.UpsellItems[1]);
        Assert.False(multiVariantQuickAdd);

        // Vào màn hình Thanh toán: chưa chọn mã thì tự áp dụng khuyến mãi theo đơn
        await cartVm.LoadCheckoutAsync();
        Assert.False(cartVm.HasAppliedVoucher);
        Assert.True(cartVm.HasCheckoutOrderPromo);
        Assert.Equal(170_000m, cartVm.Discount);
        Assert.Contains("Ưu đãi đơn từ 2 triệu", cartVm.CheckoutOrderPromoText);

        // Khi chọn mã giảm giá RUN15 thì mã thay cho khuyến mãi theo đơn
        var promoOpt = cartVm.VoucherOptions.First(v => v.Code == "RUN15");
        await cartVm.SelectVoucherOptionAsync(promoOpt);
        Assert.True(cartVm.HasAppliedVoucher);
        Assert.False(cartVm.HasCheckoutOrderPromo);
        Assert.Equal(150_000m, cartVm.Discount);
    }

    // Kiểm tra tự cập nhật trạng thái đơn hàng định kỳ (RefreshOrdersSilentlyAsync & DetectStatusChanges).
    [Fact]
    public async Task SilentOrderPollingDetectsStatusChangesAndUpdatesListWithoutSettingIsBusy()
    {
        var cache = new AppMemoryCache();
        var fakeApi = new FakeCommerceApi();
        var vm = new OrdersViewModel(fakeApi, cache);

        await vm.LoadOrdersAsync();
        Assert.Equal("cho_xac_nhan", vm.Orders.First(o => o.Code == "DH00001").Status);

        // Cửa hàng cập nhật trạng thái đơn DH00001 sang Đang giao và đơn DH00003 sang Đã giao
        fakeApi.AdvanceOrderStatus(1, "dang_giao", "Đang giao");
        fakeApi.AdvanceOrderStatus(3, "da_giao", "Đã giao");

        var changes = await vm.RefreshOrdersSilentlyAsync();
        Assert.False(vm.IsBusy);
        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, msg => msg.Contains("DH00001") && msg.Contains("Đang giao"));
        Assert.Contains(changes, msg => msg.Contains("DH00003") && msg.Contains("đã được giao"));
        Assert.Equal("dang_giao", vm.Orders.First(o => o.Code == "DH00001").Status);
        Assert.Equal("da_giao", vm.Orders.First(o => o.Code == "DH00003").Status);
        Assert.True(vm.HasStatus);
    }

    // ICommerceApi giả dùng dữ liệu mẫu.
    private sealed class FakeCommerceApi : ICommerceApi
    {
        private readonly List<OrderDto> _orders = CreateSampleOrders().ToList();
        public int OrderListCalls { get; private set; }

        // Trả giỏ mẫu.
        public Task<CartDto> GetCartAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCart());

        // Trả giỏ mẫu khi thêm sản phẩm.
        public Task<CartDto> AddCartItemAsync(int variantId, int quantity, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCart());

        // Trả giỏ mẫu khi đổi số lượng.
        public Task<CartDto> SetCartItemQuantityAsync(int variantId, int quantity, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCart());

        // Trả giỏ mẫu khi xóa dòng.
        public Task<CartDto> RemoveCartItemAsync(int variantId, int quantity, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCart());

        // Trả giỏ mẫu khi xóa dòng.
        public Task<CartDto> RemoveCartItemAsync(int variantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCart());

        // Không làm gì khi xóa giỏ.
        public Task ClearCartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        // Trả dữ liệu thanh toán mẫu.
        public Task<CheckoutDto> GetCheckoutAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCheckout(null));

        // Trả voucher mẫu.
        public Task<IReadOnlyList<EligibleVoucherDto>> GetEligibleVouchersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EligibleVoucherDto>>([
                new EligibleVoucherDto("RUN15", "Giảm 15% đơn chạy bộ", 15m, 200_000m, "Vàng", DateTimeOffset.UtcNow.AddDays(10), 150_000m)
            ]);

        // Trả dữ liệu thanh toán mẫu có giảm giá.
        public Task<CheckoutDto> QuoteCheckoutAsync(string? voucherCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCheckout(voucherCode));

        // Cập nhật trạng thái đơn trong bộ nhớ giả lập để kiểm thử polling.
        public void AdvanceOrderStatus(int orderId, string nextStatus, string nextStatusLabel)
        {
            var idx = _orders.FindIndex(o => o.Id == orderId);
            if (idx < 0) return;
            var old = _orders[idx];
            _orders[idx] = old with
            {
                Status = nextStatus,
                StatusLabel = nextStatusLabel,
                PaymentStatus = nextStatus == "da_giao" ? "da_thanh_toan" : old.PaymentStatus,
                CanCancel = nextStatus == "cho_xac_nhan" && old.PaymentStatus == "chua_thanh_toan"
            };
        }

        // Trả danh sách đơn mẫu.
        public Task<PagedResponse<OrderDto>> GetOrdersAsync(int page = 1, int pageSize = 20, string? status = null, CancellationToken cancellationToken = default)
        {
            OrderListCalls++;
            return Task.FromResult(new PagedResponse<OrderDto>(_orders.ToList(), page, pageSize, _orders.Count));
        }

        // Trả chi tiết đơn mẫu.
        public Task<OrderDto> GetOrderDetailAsync(int orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_orders.First(o => o.Id == orderId));

        // Trả đơn mẫu khi đặt hàng.
        public Task<OrderDto> PlaceOrderAsync(PlaceOrderRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(_orders[0]);

        // Hủy đơn mẫu.
        public Task<OrderDto> CancelOrderAsync(int orderId, CancellationToken cancellationToken = default)
        {
            var idx = _orders.FindIndex(o => o.Id == orderId);
            var old = _orders[idx];
            var cancelled = old with
            {
                Status = "da_huy",
                StatusLabel = "Đã hủy",
                CanCancel = false,
                CancellationReason = "Khách hàng hủy trên ứng dụng"
            };
            _orders[idx] = cancelled;
            return Task.FromResult(cancelled);
        }

        // Tạo giỏ mẫu kèm khuyến mãi theo giá trị đơn và gợi ý mua thêm (upsell).
        private static CartDto CreateSampleCart() => new(
            [
                new CartItemDto(101, 1, "Giày chạy bộ Nike Pegasus 41", "42", "Đen", null, 2_500_000m, 1, 10, 2_500_000m, true),
                new CartItemDto(102, 2, "Áo thun thể thao Dri-FIT", "L", "Xanh", null, 450_000m, 2, 15, 900_000m, true)
            ],
            3,
            3_400_000m,
            new AppliedOrderPromoDto("Ưu đãi đơn từ 2 triệu", 170_000m),
            new CartUpsellDto(
                "Ưu đãi đơn từ 5 triệu",
                1_600_000m,
                10m,
                500_000m,
                [
                    new UpsellItemDto(10, "Bình nước thể thao Pro", null, 250_000m, 501),
                    new UpsellItemDto(11, "Quần short chạy bộ Aero", null, 550_000m, null)
                ]));

        // Tạo dữ liệu thanh toán mẫu: nếu chưa chọn mã thì tự áp dụng khuyến mãi theo giá trị đơn.
        private static CheckoutDto CreateSampleCheckout(string? voucherCode)
        {
            var hasVoucher = !string.IsNullOrWhiteSpace(voucherCode);
            var autoPromo = hasVoucher ? null : new AppliedOrderPromoDto("Ưu đãi đơn từ 2 triệu", 170_000m);
            var discount = hasVoucher ? 150_000m : 170_000m;
            return new CheckoutDto(
                CreateSampleCart(),
                new ReceiverDto("Nguyễn Minh An", "0901234567", "123 Lê Lợi, Quận 1, TP.HCM"),
                [new CheckoutPaymentMethodDto(1, "Thanh toán khi nhận hàng (COD)", true)],
                [new EligibleVoucherDto("RUN15", "Giảm 15% đơn chạy bộ", 15m, 200_000m, "Vàng", DateTimeOffset.UtcNow.AddDays(10), 150_000m)],
                discount,
                0m,
                3_400_000m - discount,
                true,
                autoPromo);
        }

        // Tạo danh sách đơn mẫu.
        public static IReadOnlyList<OrderDto> CreateSampleOrders() =>
        [
            new OrderDto(
                1, "DH00001", new DateTimeOffset(2026, 2, 10, 9, 30, 0, TimeSpan.FromHours(7)),
                "cho_xac_nhan", "Chờ xác nhận", "chua_thanh_toan", "Thanh toán khi nhận hàng (COD)",
                new ReceiverDto("Nguyễn Minh An", "0901234567", "123 Lê Lợi, Q.1, TP.HCM"),
                null, null, null, 2_950_000m, 150_000m, 0m, 2_800_000m, "HD2026001", true,
                [
                    new OrderItemDto(1, "Giày chạy bộ Nike Pegasus 41", "42 / Đen", 1, 2_500_000m, 2_500_000m, null),
                    new OrderItemDto(2, "Áo thun thể thao Dri-FIT", "L / Xanh", 1, 450_000m, 450_000m, null)
                ]),
            new OrderDto(
                2, "DH00002", new DateTimeOffset(2026, 5, 18, 14, 15, 0, TimeSpan.FromHours(7)),
                "da_giao", "Đã giao", "da_thanh_toan", "Chuyển khoản ngân hàng",
                new ReceiverDto("Nguyễn Minh An", "0901234567", "123 Lê Lợi, Q.1, TP.HCM"),
                null, "GHN", "GHN8899", 3_200_000m, 0m, 0m, 3_200_000m, "HD2026002", false,
                [
                    new OrderItemDto(3, "Vợt cầu lông Yonex Astrox 99", "4U / Đỏ", 1, 3_200_000m, 3_200_000m, null)
                ]),
            new OrderDto(
                3, "DH00003", new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.FromHours(7)),
                "dang_giao", "Đang giao", "chua_thanh_toan", "Thanh toán khi nhận hàng (COD)",
                new ReceiverDto("Trần Thị Bình", "0912345678", "45 Nguyễn Huệ, Q.1, TP.HCM"),
                null, "ViettelPost", "VTP1122", 420_000m, 0m, 30_000m, 450_000m, "HD2026003", false,
                [
                    new OrderItemDto(4, "Bình nước thể thao 1L", "1000ml / Xám", 2, 210_000m, 420_000m, null)
                ])
        ];
    }
}
