using SportShop.Contracts.Commerce;
using SportShop.Contracts.Common;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

public sealed class CommerceAndStatsTests
{
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

    private sealed class FakeCommerceApi : ICommerceApi
    {
        private readonly List<OrderDto> _orders = CreateSampleOrders().ToList();
        public int OrderListCalls { get; private set; }

        public Task<CartDto> GetCartAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCart());

        public Task<CartDto> AddCartItemAsync(int variantId, int quantity, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCart());

        public Task<CartDto> SetCartItemQuantityAsync(int variantId, int quantity, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCart());

        public Task<CartDto> RemoveCartItemAsync(int variantId, int quantity, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCart());

        public Task<CartDto> RemoveCartItemAsync(int variantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCart());

        public Task ClearCartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CheckoutDto> GetCheckoutAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCheckout(0m));

        public Task<IReadOnlyList<EligibleVoucherDto>> GetEligibleVouchersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EligibleVoucherDto>>([
                new EligibleVoucherDto("RUN15", "Giảm 15% đơn chạy bộ", 15m, 200_000m, "Vàng", DateTimeOffset.UtcNow.AddDays(10), 150_000m)
            ]);

        public Task<CheckoutDto> QuoteCheckoutAsync(string? voucherCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateSampleCheckout(string.IsNullOrWhiteSpace(voucherCode) ? 0m : 150_000m));

        public Task<PagedResponse<OrderDto>> GetOrdersAsync(int page = 1, int pageSize = 20, string? status = null, CancellationToken cancellationToken = default)
        {
            OrderListCalls++;
            return Task.FromResult(new PagedResponse<OrderDto>(_orders, page, pageSize, _orders.Count));
        }

        public Task<OrderDto> GetOrderDetailAsync(int orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_orders.First(o => o.Id == orderId));

        public Task<OrderDto> PlaceOrderAsync(PlaceOrderRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(_orders[0]);

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

        private static CartDto CreateSampleCart() => new(
            [
                new CartItemDto(101, 1, "Giày chạy bộ Nike Pegasus 41", "42", "Đen", null, 2_500_000m, 1, 10, 2_500_000m, true),
                new CartItemDto(102, 2, "Áo thun thể thao Dri-FIT", "L", "Xanh", null, 450_000m, 2, 15, 900_000m, true)
            ],
            3,
            3_400_000m);

        private static CheckoutDto CreateSampleCheckout(decimal discount) => new(
            CreateSampleCart(),
            new ReceiverDto("Nguyễn Minh An", "0901234567", "123 Lê Lợi, Quận 1, TP.HCM"),
            [new CheckoutPaymentMethodDto(1, "Thanh toán khi nhận hàng (COD)", true)],
            [new EligibleVoucherDto("RUN15", "Giảm 15% đơn chạy bộ", 15m, 200_000m, "Vàng", DateTimeOffset.UtcNow.AddDays(10), 150_000m)],
            discount,
            0m,
            3_400_000m - discount,
            true);

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
