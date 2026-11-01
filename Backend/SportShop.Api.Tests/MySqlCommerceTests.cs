using Dapper;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using MySqlConnector;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;
using Xunit;

namespace SportShop.Api.Tests;

// Creates and removes only this test's records in an explicitly configured dedicated database.
[Trait("Category", "MySql")]
public sealed class MySqlCommerceTests
{
    [MySqlFact]
    public async Task HttpCommerceRoundTripAndCrossCustomerAccessUseRealDatabase()
    {
        using var fixture = new CommerceFixture();
        fixture.CreateVoucher();
        using var factory = new CommerceValidationFactory("Customer", fixture.ConnectionString, fixture.CustomerId);
        using var client = factory.CreateClient();
        using var empty = await client.GetAsync("/api/v1/cart");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        using var added = await client.PostAsJsonAsync("/api/v1/cart/items", new { variantId = fixture.VariantId, quantity = 2 });
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        using var updated = await client.PutAsJsonAsync($"/api/v1/cart/items/{fixture.VariantId}", new { quantity = 3 });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var removed = await client.DeleteAsync($"/api/v1/cart/items/{fixture.VariantId}");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        using var readded = await client.PostAsJsonAsync("/api/v1/cart/items", new { variantId = fixture.VariantId, quantity = 2 });
        Assert.Equal(HttpStatusCode.OK, readded.StatusCode);
        using var checkout = await client.GetAsync("/api/v1/checkout");
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        using var vouchers = await client.GetAsync("/api/v1/checkout/vouchers");
        Assert.Equal(HttpStatusCode.OK, vouchers.StatusCode);
        using var quote = await client.PostAsJsonAsync("/api/v1/checkout/quote", new { voucher = fixture.VoucherCode });
        Assert.Equal(HttpStatusCode.OK, quote.StatusCode);
        Assert.Equal(20000, (await CatalogApiTests.ReadJson(quote)).GetProperty("discount").GetDecimal());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new { name = fixture.Receiver.Name, phone = fixture.Receiver.Phone,
                address = fixture.Receiver.Address, paymentMethodId = fixture.PaymentId, voucher = fixture.VoucherCode })
        };
        request.Headers.Add("Idempotency-Key", "http-checkout-1");
        using var ordered = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, ordered.StatusCode);
        var order = await CatalogApiTests.ReadJson(ordered);
        int id = order.GetProperty("id").GetInt32();
        Assert.Equal(210000, order.GetProperty("total").GetDecimal());
        Assert.EndsWith("+07:00", order.GetProperty("orderedAt").GetString());
        using var details = await client.GetAsync($"/api/v1/orders/{id}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        using var list = await client.GetAsync("/api/v1/orders?pageSize=1&status=cho_xac_nhan");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(1, (await CatalogApiTests.ReadJson(list)).GetProperty("totalItems").GetInt64());
        using var otherFactory = new CommerceValidationFactory("Customer", fixture.ConnectionString, fixture.OtherCustomerId);
        using var other = otherFactory.CreateClient();
        using var foreignOrder = await other.GetAsync($"/api/v1/orders/{id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignOrder.StatusCode);
        using var foreignCancel = await other.PostAsync($"/api/v1/orders/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.NotFound, foreignCancel.StatusCode);
        using var canceled = await client.PostAsync($"/api/v1/orders/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, canceled.StatusCode);
        using var canceledTwice = await client.PostAsync($"/api/v1/orders/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, canceledTwice.StatusCode);
        using var cleared = await client.DeleteAsync("/api/v1/cart");
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        Assert.Equal(10, fixture.Stock());
        Assert.Equal(0, fixture.VoucherUses());
    }

    [MySqlFact]
    public void SharedCartCheckoutReplayOwnershipAndCancellationPreserveStock()
    {
        using var fixture = new CommerceFixture();
        var web = fixture.Shop();
        var mobile = fixture.Shop();
        Assert.Null(web.AddToCart(fixture.CustomerId, fixture.VariantId, 2));
        Assert.Equal(2, mobile.GetCart(fixture.CustomerId)[fixture.VariantId]);
        Assert.NotNull(mobile.AddToCart(fixture.CustomerId, fixture.VariantId, 999));
        Assert.NotNull(mobile.SetQty(fixture.CustomerId, fixture.VariantId, 999));
        Assert.Equal(2, web.CartLines(fixture.CustomerId).Single().Qty);

        var first = mobile.PlaceCartOrder(fixture.CustomerId, fixture.Receiver, fixture.PaymentId, null, "checkout-1");
        Assert.Null(first.Error);
        Assert.NotNull(first.OrderId);
        Assert.False(first.Replayed);
        Assert.Empty(web.GetCart(fixture.CustomerId));
        Assert.Equal(8, fixture.Stock());
        Assert.Empty(web.Orders(fixture.OtherCustomerId, first.OrderId));
        Assert.NotNull(web.CancelOrder(fixture.OtherCustomerId, first.OrderId!.Value));
        Assert.Equal(8, fixture.Stock());

        Assert.Null(web.AddToCart(fixture.CustomerId, fixture.VariantId, 1));
        var replay = mobile.PlaceCartOrder(fixture.CustomerId, fixture.Receiver, fixture.PaymentId, null, "checkout-1");
        Assert.Equal(first.OrderId, replay.OrderId);
        Assert.True(replay.Replayed);
        Assert.Equal(1, web.GetCart(fixture.CustomerId)[fixture.VariantId]);
        Assert.Equal(8, fixture.Stock());
        var mismatch = mobile.PlaceCartOrder(fixture.CustomerId, fixture.Receiver with { Address = "Different address" },
            fixture.PaymentId, null, "checkout-1");
        Assert.Null(mismatch.OrderId);
        Assert.NotNull(mismatch.Error);
        Assert.Equal(1, web.OrderCount(fixture.CustomerId));
        Assert.Null(web.CancelOrder(fixture.CustomerId, first.OrderId.Value));
        Assert.Equal(10, fixture.Stock());
        Assert.NotNull(web.CancelOrder(fixture.CustomerId, first.OrderId.Value));
        Assert.Equal(10, fixture.Stock());
        Assert.Equal("da_huy", web.Orders(fixture.CustomerId, first.OrderId).Single().Status);
        Assert.Empty(web.Orders(fixture.CustomerId, page: 2, pageSize: 1));
    }

    [MySqlFact]
    public async Task ConcurrentRetriesProduceOneOrderAndOneStockDeduction()
    {
        using var fixture = new CommerceFixture();
        Assert.Null(fixture.Shop().AddToCart(fixture.CustomerId, fixture.VariantId, 1));
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            fixture.Shop().PlaceCartOrder(fixture.CustomerId, fixture.Receiver, fixture.PaymentId, null, "same-request"))));
        Assert.All(results, result => Assert.Null(result.Error));
        Assert.Single(results.Select(result => result.OrderId).Distinct());
        Assert.Single(results, result => !result.Replayed);
        Assert.Equal(9, fixture.Stock());
        Assert.Equal(1, fixture.Shop().OrderCount(fixture.CustomerId));
    }

    [MySqlFact]
    public void VoucherEligibilityAndCancelRestoreUseWithoutAllowingSecondActiveFirstPurchase()
    {
        using var fixture = new CommerceFixture();
        fixture.CreateVoucher();
        var shop = fixture.Shop();
        Assert.Null(shop.AddToCart(fixture.CustomerId, fixture.VariantId, 2));
        var preview = shop.PreviewVoucher(fixture.CustomerId, fixture.VoucherCode, 200000);
        Assert.Null(preview.Error);
        Assert.Equal(20000, preview.Discount);
        var order = shop.PlaceCartOrder(fixture.CustomerId, fixture.Receiver, fixture.PaymentId, fixture.VoucherCode, "voucher-1");
        Assert.Null(order.Error);
        Assert.Equal(1, fixture.VoucherUses());
        Assert.NotNull(shop.PreviewVoucher(fixture.CustomerId, fixture.VoucherCode, 100000).Error);
        Assert.Null(shop.CancelOrder(fixture.CustomerId, order.OrderId!.Value));
        Assert.Equal(0, fixture.VoucherUses());
        Assert.Null(shop.PreviewVoucher(fixture.CustomerId, fixture.VoucherCode, 100000).Error);
        Assert.Equal(10, fixture.Stock());
    }

    [MySqlFact]
    public void LegacyCartImportsOnceAcrossHostsWithoutDestroyingBackup()
    {
        using var fixture = new CommerceFixture();
        File.WriteAllText(fixture.LegacyPath, System.Text.Json.JsonSerializer.Serialize(
            new Dictionary<int, Dictionary<int, int>> { [fixture.CustomerId] = new() { [fixture.VariantId] = 3 } }));
        var web = fixture.Shop();
        Assert.Equal(3, web.GetCart(fixture.CustomerId)[fixture.VariantId]);
        web.ClearCart(fixture.CustomerId);
        Assert.Empty(fixture.Shop().GetCart(fixture.CustomerId));
        Assert.Contains(fixture.VariantId.ToString(), File.ReadAllText(fixture.LegacyPath));
    }

    [MySqlFact]
    public void AdminCanDeleteAnUnorderedVariantEvenWhenItIsInACustomerCart()
    {
        using var fixture = new CommerceFixture();
        Assert.Null(fixture.Shop().AddToCart(fixture.CustomerId, fixture.VariantId, 1));
        fixture.DeleteVariant();
        Assert.Empty(fixture.Shop().GetCart(fixture.CustomerId));
    }

    [MySqlFact]
    public void PaidPendingOrdersRequireStoreRefundInsteadOfCustomerSelfCancellation()
    {
        using var fixture = new CommerceFixture();
        var shop = fixture.Shop();
        Assert.Null(shop.AddToCart(fixture.CustomerId, fixture.VariantId, 1));
        var order = shop.PlaceCartOrder(fixture.CustomerId, fixture.Receiver, fixture.PaymentId, null, "paid-order");
        Assert.NotNull(order.OrderId);
        fixture.MarkPaid(order.OrderId!.Value);
        Assert.NotNull(shop.CancelOrder(fixture.CustomerId, order.OrderId.Value));
        var detail = shop.Orders(fixture.CustomerId, order.OrderId).Single();
        Assert.False(detail.CanCancel);
        Assert.Equal("da_thanh_toan", detail.PayStatus);
        Assert.Equal("cho_xac_nhan", detail.Status);
        Assert.Equal(9, fixture.Stock());
    }

    private sealed class CommerceFixture : IDisposable
    {
        readonly string _connectionString = Environment.GetEnvironmentVariable("SPORTSHOP_TEST_MYSQL")!;
        public string ConnectionString => _connectionString;
        readonly string _token = Guid.NewGuid().ToString("N")[..20];
        readonly IConfiguration _configuration;
        readonly TestEnvironment _environment = new();
        int _categoryId, _productId, _promotionId;
        public int CustomerId { get; }
        public int OtherCustomerId { get; }
        public int VariantId { get; }
        public int PaymentId { get; }
        public string LegacyPath { get; }
        public string VoucherCode => "TEST" + _token;
        public ReceiverInfo Receiver { get; } = new("API Test Customer", "0901234567", "123 Test Street");

        public CommerceFixture()
        {
            LegacyPath = Path.Combine(Path.GetTempPath(), "sportshop-cart-" + _token + ".json");
            _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString, ["Commerce:LegacyCartPath"] = LegacyPath
            }).Build();
            using var connection = Open();
            CustomerId = connection.ExecuteScalar<int>("INSERT INTO KHACH_HANG(ho_lot,ten) VALUES ('API test',@name); SELECT LAST_INSERT_ID();", new { name = _token });
            OtherCustomerId = connection.ExecuteScalar<int>("INSERT INTO KHACH_HANG(ho_lot,ten) VALUES ('API other',@name); SELECT LAST_INSERT_ID();", new { name = _token });
            _categoryId = connection.ExecuteScalar<int>("INSERT INTO LOAI_SP(ten_loai) VALUES (@name); SELECT LAST_INSERT_ID();", new { name = "Test " + _token });
            _productId = connection.ExecuteScalar<int>("INSERT INTO SAN_PHAM(ma_loai,ten_san_pham,don_vi_tinh) VALUES (@category,'API Test Product','cái'); SELECT LAST_INSERT_ID();", new { category = _categoryId });
            VariantId = connection.ExecuteScalar<int>("INSERT INTO BIEN_THE_SP(ma_san_pham,gia_ban,so_luong_ton) VALUES (@product,100000,10); SELECT LAST_INSERT_ID();", new { product = _productId });
            PaymentId = connection.ExecuteScalar<int>("INSERT INTO PHUONG_THUC_THANH_TOAN(ten_phuong_thuc_thanh_toan) VALUES (@name); SELECT LAST_INSERT_ID();", new { name = "API Test " + _token });
        }

        MySqlConnection Open() { var connection = new MySqlConnection(_connectionString); connection.Open(); return connection; }
        public ShopStore Shop() => new(_configuration, new CartStore(_environment, _configuration));
        public int Stock() { using var connection = Open(); return connection.ExecuteScalar<int>("SELECT so_luong_ton FROM BIEN_THE_SP WHERE ma_bien_the=@VariantId", new { VariantId }); }
        public void DeleteVariant() { using var connection = Open(); connection.Execute("DELETE FROM BIEN_THE_SP WHERE ma_bien_the=@VariantId", new { VariantId }); }
        public void MarkPaid(int orderId) { using var connection = Open(); connection.Execute("UPDATE DON_HANG SET trang_thai_thanh_toan='da_thanh_toan' WHERE ma_don_hang=@orderId", new { orderId }); }
        public int VoucherUses() { using var connection = Open(); return connection.ExecuteScalar<int>("SELECT so_luot_da_dung FROM CTKM WHERE ma_ctkm=@id", new { id = _promotionId }); }
        public void CreateVoucher()
        {
            using var connection = Open();
            _promotionId = connection.ExecuteScalar<int>("""
                INSERT INTO CTKM(ten_ctkm,loai_khuyen_mai,ngay_bat_dau,ngay_ket_thuc,so_luong_gioi_han,gioi_han_khach_hang)
                VALUES ('Ưu đãi khách mới API test','theo_voucher',DATE_SUB(NOW(),INTERVAL 1 DAY),DATE_ADD(NOW(),INTERVAL 1 DAY),1,1);
                SELECT LAST_INSERT_ID();
                """);
            connection.Execute("INSERT INTO KM_THEO_VOUCHER(ma_ctkm,ma_voucher,phan_tram_giam) VALUES (@id,@code,10)", new { id = _promotionId, code = VoucherCode });
        }

        public void Dispose()
        {
            using var connection = Open();
            int[] customers = [CustomerId, OtherCustomerId];
            var orders = connection.Query<int>("SELECT ma_don_hang FROM DON_HANG WHERE ma_khach_hang IN @customers", new { customers }).ToArray();
            connection.Execute("DELETE FROM CUSTOMER_CART_ITEM WHERE customer_id IN @customers; DELETE FROM CUSTOMER_CART_STATE WHERE customer_id IN @customers; DELETE FROM MOBILE_ORDER_REQUEST WHERE customer_id IN @customers", new { customers });
            if (orders.Length > 0)
                connection.Execute("DELETE FROM LUOT_SU_DUNG_KHUYEN_MAI WHERE ma_don_hang IN @orders; DELETE FROM VAN_CHUYEN WHERE ma_don_hang IN @orders; DELETE FROM CHI_TIET_DON_HANG WHERE ma_don_hang IN @orders; DELETE FROM DON_HANG WHERE ma_don_hang IN @orders", new { orders });
            connection.Execute("DELETE FROM SO_DIA_CHI WHERE ma_khach_hang IN @customers; DELETE FROM KHACH_HANG WHERE ma_khach_hang IN @customers", new { customers });
            connection.Execute("DELETE FROM BIEN_THE_SP WHERE ma_bien_the=@VariantId; DELETE FROM SAN_PHAM WHERE ma_san_pham=@product; DELETE FROM LOAI_SP WHERE ma_loai=@category; DELETE FROM PHUONG_THUC_THANH_TOAN WHERE ma_phuong_thuc_thanh_toan=@PaymentId",
                new { VariantId, product = _productId, category = _categoryId, PaymentId });
            if (_promotionId != 0)
                connection.Execute("DELETE FROM KM_THEO_VOUCHER WHERE ma_ctkm=@id; DELETE FROM CTKM WHERE ma_ctkm=@id", new { id = _promotionId });
            if (File.Exists(LegacyPath)) File.Delete(LegacyPath);
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CommerceTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

