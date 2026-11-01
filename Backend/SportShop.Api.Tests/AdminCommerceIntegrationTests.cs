using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using MySqlConnector;
using SportShop.CustomerWeb.Services;
using SportShop.CustomerWeb.Models;
using Xunit;
using AdminCommands = SportShop.Admin.Models.ShopCommands;
using AdminContext = SportShop.Admin.Models.Ctx;
using AdminForm = SportShop.Admin.Models.Form;
using AdminCache = SportShop.Admin.Models.DataCache;

namespace SportShop.Api.Tests;

// Exercises the real Admin command model and shared mobile stores against owned fixture records.
[Trait("Category", "MySql")]
public sealed class AdminCommerceIntegrationTests
{
    [MySqlFact]
    public void MobileOrder_AdminProcessesAndDelivers_InvoiceAndMembershipAreVisibleToMobile()
    {
        using var fixture = new AdminCommerceFixture(initialSpent: 1900000, initialPoints: 190);
        var mobile = fixture.Mobile();
        Assert.Null(mobile.AddToCart(fixture.CustomerId, fixture.VariantId, 1));
        var placed = mobile.PlaceCartOrder(fixture.CustomerId, fixture.Receiver, fixture.PaymentId, null, "admin-delivery");
        Assert.Null(placed.Error);
        int orderId = placed.OrderId!.Value;

        var prepared = fixture.Admin().Save("order", orderId, fixture.Update("dang_chuan_bi", "chua_thanh_toan"), fixture.Employee);
        Assert.True(prepared.Ok, prepared.Message);
        Assert.Equal("dang_chuan_bi", mobile.Orders(fixture.CustomerId, orderId).Single().Status);
        var shipped = fixture.Admin().Save("order", orderId, fixture.Update("dang_giao", "chua_thanh_toan"), fixture.Employee);
        Assert.True(shipped.Ok, shipped.Message);
        Assert.Equal("dang_giao", mobile.Orders(fixture.CustomerId, orderId).Single().Status);
        var delivered = fixture.Admin().Save("order", orderId, fixture.Update("da_giao", "da_thanh_toan"), fixture.Employee);
        Assert.True(delivered.Ok, delivered.Message);

        var detail = fixture.Mobile().Orders(fixture.CustomerId, orderId).Single();
        Assert.Equal("da_giao", detail.Status);
        Assert.Equal("da_thanh_toan", detail.PayStatus);
        Assert.StartsWith("HD", detail.Invoice);
        Assert.False(detail.CanCancel);
        Assert.Equal(230000, detail.Total);
        var profile = fixture.Customers().GetProfile(fixture.CustomerId)!;
        Assert.Equal("Bạc", profile.Tier);
        Assert.Equal(213, profile.Points);
        Assert.Equal(1, profile.Orders);
        Assert.Equal((2130000m, 213), fixture.Loyalty());
        Assert.Equal(1, fixture.InvoiceCount(orderId));
        Assert.Equal(1, fixture.InvoiceLineCount(orderId));

        var repeated = fixture.Admin().Save("order", orderId, fixture.Update("da_giao", "da_thanh_toan"), fixture.Employee);
        Assert.False(repeated.Ok);
        Assert.Equal(1, fixture.InvoiceCount(orderId));
        Assert.Equal((2130000m, 213), fixture.Loyalty());
        Assert.Equal(9, fixture.Stock(fixture.VariantId));
    }

    [MySqlFact]
    public void AdminPaidPendingOrder_RequiresStoreCancellation_RefundsInvoiceLoyaltyStockAndVoucher()
    {
        using var fixture = new AdminCommerceFixture();
        fixture.CreateVoucher();
        var created = fixture.Admin().Save("order", 0, fixture.CreateOrder(paid: true, useVoucher: true), fixture.Employee);
        Assert.True(created.Ok, created.Message);
        int orderId = fixture.LatestOrder(fixture.CustomerId);
        var mobile = fixture.Mobile();
        var paid = mobile.Orders(fixture.CustomerId, orderId).Single();
        Assert.Equal("cho_xac_nhan", paid.Status);
        Assert.Equal("da_thanh_toan", paid.PayStatus);
        Assert.False(paid.CanCancel);
        Assert.NotNull(paid.Invoice);
        Assert.Equal((180000m, 18), fixture.Loyalty());
        Assert.Equal(1, fixture.VoucherUses());

        Assert.NotNull(mobile.CancelOrder(fixture.CustomerId, orderId));
        Assert.Equal(9, fixture.Stock(fixture.VariantId));
        Assert.Equal(1, fixture.InvoiceCount(orderId));
        var canceled = fixture.Admin().Save("order", orderId,
            fixture.Update("da_huy", "da_thanh_toan", "Khách yêu cầu cửa hàng hoàn tiền"), fixture.Employee);
        Assert.True(canceled.Ok, canceled.Message);
        var detail = fixture.Mobile().Orders(fixture.CustomerId, orderId).Single();
        Assert.Equal("da_huy", detail.Status);
        Assert.Equal("chua_thanh_toan", detail.PayStatus);
        Assert.Null(detail.Invoice);
        Assert.Equal(10, fixture.Stock(fixture.VariantId));
        Assert.Equal(0, fixture.InvoiceCount(orderId));
        Assert.Equal((0m, 0), fixture.Loyalty());
        Assert.Equal(0, fixture.VoucherUses());
        Assert.Equal("Thường", fixture.Customers().GetProfile(fixture.CustomerId)!.Tier);
    }

    [MySqlFact]
    public async Task AdminAndMobile_DifferentCustomersAndVariants_CannotExceedOneSharedVoucherUse()
    {
        using var fixture = new AdminCommerceFixture();
        fixture.CreateVoucher();
        Assert.Null(fixture.Mobile().AddToCart(fixture.OtherCustomerId, fixture.OtherVariantId, 1));
        using var start = new ManualResetEventSlim(false);
        var counterSale = Task.Run(() =>
        {
            start.Wait();
            return fixture.Admin().Save("order", 0, fixture.CreateOrder(paid: false, useVoucher: true), fixture.Employee);
        });
        var mobileSale = Task.Run(() =>
        {
            start.Wait();
            return fixture.Mobile().PlaceCartOrder(fixture.OtherCustomerId, fixture.Receiver, fixture.PaymentId, fixture.VoucherCode, "global-voucher-race");
        });
        start.Set();
        await Task.WhenAll(counterSale, mobileSale);
        var counter = await counterSale;
        var app = await mobileSale;
        Assert.NotEqual(counter.Ok, app.OrderId.HasValue);
        Assert.Equal(1, fixture.TotalOrders());
        Assert.Equal(1, fixture.VoucherUses());
        Assert.Equal(19, fixture.Stock(fixture.VariantId) + fixture.Stock(fixture.OtherVariantId));
        if (counter.Ok)
        {
            Assert.NotNull(app.Error);
            Assert.Single(fixture.Mobile().GetCart(fixture.OtherCustomerId));
        }
        else
        {
            Assert.Null(app.Error);
            Assert.Empty(fixture.Mobile().GetCart(fixture.OtherCustomerId));
        }
    }

    [MySqlFact]
    public async Task AdminCancellationAndMobileCheckout_ForSameCustomer_BothCompleteWithoutDeadlockOrLostLoyalty()
    {
        using var fixture = new AdminCommerceFixture();
        var created = fixture.Admin().Save("order", 0, fixture.CreateOrder(paid: true, useVoucher: false), fixture.Employee);
        Assert.True(created.Ok, created.Message);
        int oldOrder = fixture.LatestOrder(fixture.CustomerId);
        Assert.Null(fixture.Mobile().AddToCart(fixture.CustomerId, fixture.OtherVariantId, 1));
        using var start = new ManualResetEventSlim(false);
        var refund = Task.Run(() =>
        {
            start.Wait();
            return fixture.Admin().Save("order", oldOrder, fixture.Update("da_huy", "da_thanh_toan", "Hoàn tại quầy"), fixture.Employee);
        });
        var purchase = Task.Run(() =>
        {
            start.Wait();
            return fixture.Mobile().PlaceCartOrder(fixture.CustomerId, fixture.Receiver, fixture.PaymentId, null, "purchase-during-refund");
        });
        start.Set();
        await Task.WhenAll(refund, purchase);
        Assert.True((await refund).Ok, (await refund).Message);
        Assert.Null((await purchase).Error);
        Assert.NotNull((await purchase).OrderId);
        Assert.Equal(2, fixture.TotalOrders());
        Assert.Equal(10, fixture.Stock(fixture.VariantId));
        Assert.Equal(9, fixture.Stock(fixture.OtherVariantId));
        Assert.Equal((0m, 0), fixture.Loyalty());
        Assert.Equal(0, fixture.InvoiceCount(oldOrder));
    }

    private sealed class AdminCommerceFixture : IDisposable
    {
        readonly string _connectionString = Environment.GetEnvironmentVariable("SPORTSHOP_TEST_MYSQL")!;
        readonly string _suffix = Guid.NewGuid().ToString("N")[..18];
        readonly IConfiguration _configuration;
        readonly TestEnvironment _environment = new();
        readonly int _categoryId, _productId, _otherProductId;
        int _promotionId;
        public int CustomerId { get; }
        public int OtherCustomerId { get; }
        public int VariantId { get; }
        public int OtherVariantId { get; }
        public int PaymentId { get; }
        public string VoucherCode => "ADMIN" + _suffix;
        public ReceiverInfo Receiver { get; } = new("Admin Mobile Customer", "0901234567", "123 Integration Street");
        public AdminContext Employee { get; }

        public AdminCommerceFixture(decimal initialSpent = 0, int initialPoints = 0)
        {
            _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
                ["Commerce:LegacyCartPath"] = Path.Combine(Path.GetTempPath(), "admin-commerce-" + _suffix + ".json")
            }).Build();
            using var connection = Open();
            int employeeId = connection.ExecuteScalar<int>("SELECT ma_nhan_vien FROM NHAN_VIEN WHERE trang_thai='dang_lam' ORDER BY ma_nhan_vien LIMIT 1");
            Assert.True(employeeId > 0, "The seeded dedicated test database needs an active employee.");
            Employee = new AdminContext(employeeId, "admin-api-integration", "Quản lý");
            CustomerId = connection.ExecuteScalar<int>("""
                INSERT INTO KHACH_HANG(ho_lot,ten,tong_chi_tieu,diem_tich_luy) VALUES ('Integration',@name,@initialSpent,@initialPoints);
                SELECT LAST_INSERT_ID();
                """, new { name = _suffix, initialSpent, initialPoints });
            OtherCustomerId = connection.ExecuteScalar<int>("INSERT INTO KHACH_HANG(ho_lot,ten) VALUES ('Other integration',@name); SELECT LAST_INSERT_ID();", new { name = _suffix });
            _categoryId = connection.ExecuteScalar<int>("INSERT INTO LOAI_SP(ten_loai) VALUES (@name); SELECT LAST_INSERT_ID();", new { name = "Admin integration " + _suffix });
            _productId = connection.ExecuteScalar<int>("INSERT INTO SAN_PHAM(ma_loai,ten_san_pham,don_vi_tinh) VALUES (@category,'Admin integration A','cái'); SELECT LAST_INSERT_ID();", new { category = _categoryId });
            _otherProductId = connection.ExecuteScalar<int>("INSERT INTO SAN_PHAM(ma_loai,ten_san_pham,don_vi_tinh) VALUES (@category,'Admin integration B','cái'); SELECT LAST_INSERT_ID();", new { category = _categoryId });
            VariantId = connection.ExecuteScalar<int>("INSERT INTO BIEN_THE_SP(ma_san_pham,gia_ban,gia_von,so_luong_ton) VALUES (@product,200000,100000,10); SELECT LAST_INSERT_ID();", new { product = _productId });
            OtherVariantId = connection.ExecuteScalar<int>("INSERT INTO BIEN_THE_SP(ma_san_pham,gia_ban,gia_von,so_luong_ton) VALUES (@product,200000,100000,10); SELECT LAST_INSERT_ID();", new { product = _otherProductId });
            PaymentId = connection.ExecuteScalar<int>("INSERT INTO PHUONG_THUC_THANH_TOAN(ten_phuong_thuc_thanh_toan) VALUES (@name); SELECT LAST_INSERT_ID();", new { name = "Integration " + _suffix });
        }

        MySqlConnection Open() { var connection = new MySqlConnection(_connectionString); connection.Open(); return connection; }
        public ShopStore Mobile() => new(_configuration, new CartStore(_environment, _configuration));
        public CustomerStore Customers() => new(_configuration);
        public AdminCommands Admin() => new(_configuration, _environment, new AdminCache());
        public int Stock(int variant) { using var c = Open(); return c.ExecuteScalar<int>("SELECT so_luong_ton FROM BIEN_THE_SP WHERE ma_bien_the=@variant", new { variant }); }
        public int LatestOrder(int customer) { using var c = Open(); return c.ExecuteScalar<int>("SELECT MAX(ma_don_hang) FROM DON_HANG WHERE ma_khach_hang=@customer", new { customer }); }
        public long TotalOrders() { using var c = Open(); return c.ExecuteScalar<long>("SELECT COUNT(*) FROM DON_HANG WHERE ma_khach_hang IN @customers", new { customers = new[] { CustomerId, OtherCustomerId } }); }
        public int InvoiceCount(int orderId) { using var c = Open(); return c.ExecuteScalar<int>("SELECT COUNT(*) FROM HOA_DON WHERE ma_don_hang=@orderId", new { orderId }); }
        public int InvoiceLineCount(int orderId) { using var c = Open(); return c.ExecuteScalar<int>("SELECT COUNT(*) FROM CHI_TIET_HOA_DON ct JOIN HOA_DON hd ON hd.ma_hoa_don=ct.ma_hoa_don WHERE hd.ma_don_hang=@orderId", new { orderId }); }
        public (decimal Spent, int Points) Loyalty() { using var c = Open(); return c.QuerySingle<(decimal, int)>("SELECT tong_chi_tieu,diem_tich_luy FROM KHACH_HANG WHERE ma_khach_hang=@CustomerId", new { CustomerId }); }
        public int VoucherUses() { using var c = Open(); return c.ExecuteScalar<int>("SELECT so_luot_da_dung FROM CTKM WHERE ma_ctkm=@id", new { id = _promotionId }); }

        public AdminForm CreateOrder(bool paid, bool useVoucher) => Form(new Dictionary<string, StringValues>
        {
            ["ma_khach_hang"] = CustomerId.ToString(), ["ten_nguoi_nhan"] = Receiver.Name,
            ["sdt_nguoi_nhan"] = Receiver.Phone, ["dia_chi_giao_hang"] = Receiver.Address,
            ["ma_phuong_thuc_thanh_toan"] = PaymentId.ToString(), ["phi_van_chuyen"] = "0",
            ["trang_thai_don_hang"] = "cho_xac_nhan", ["trang_thai_thanh_toan"] = paid ? "da_thanh_toan" : "chua_thanh_toan",
            ["dong_bien_the"] = VariantId.ToString(), ["dong_so_luong"] = "1", ["ma_voucher"] = useVoucher ? VoucherCode : ""
        });
        public AdminForm Update(string status, string paid, string? reason = null) => Form(new Dictionary<string, StringValues>
        {
            ["trang_thai_don_hang"] = status, ["trang_thai_thanh_toan"] = paid, ["ly_do_huy"] = reason ?? ""
        });
        static AdminForm Form(Dictionary<string, StringValues> values) => new(new FormCollection(values));

        public void CreateVoucher()
        {
            using var c = Open();
            _promotionId = c.ExecuteScalar<int>("""
                INSERT INTO CTKM(ten_ctkm,loai_khuyen_mai,ngay_bat_dau,ngay_ket_thuc,so_luong_gioi_han,gioi_han_khach_hang)
                VALUES ('Khách mới integration test','theo_voucher',DATE_SUB(NOW(),INTERVAL 1 DAY),DATE_ADD(NOW(),INTERVAL 1 DAY),1,1);
                SELECT LAST_INSERT_ID();
                """);
            c.Execute("INSERT INTO KM_THEO_VOUCHER(ma_ctkm,ma_voucher,phan_tram_giam) VALUES (@id,@code,10)", new { id = _promotionId, code = VoucherCode });
        }

        public void Dispose()
        {
            using var c = Open();
            int[] customers = [CustomerId, OtherCustomerId];
            var orders = c.Query<int>("SELECT ma_don_hang FROM DON_HANG WHERE ma_khach_hang IN @customers", new { customers }).ToArray();
            c.Execute("DELETE FROM CUSTOMER_CART_ITEM WHERE customer_id IN @customers; DELETE FROM CUSTOMER_CART_STATE WHERE customer_id IN @customers; DELETE FROM MOBILE_ORDER_REQUEST WHERE customer_id IN @customers", new { customers });
            if (orders.Length > 0)
            {
                c.Execute("DELETE ct FROM CHI_TIET_HOA_DON ct JOIN HOA_DON hd ON hd.ma_hoa_don=ct.ma_hoa_don WHERE hd.ma_don_hang IN @orders", new { orders });
                c.Execute("DELETE FROM HOA_DON WHERE ma_don_hang IN @orders; DELETE FROM LUOT_SU_DUNG_KHUYEN_MAI WHERE ma_don_hang IN @orders; DELETE FROM VAN_CHUYEN WHERE ma_don_hang IN @orders; DELETE FROM CHI_TIET_DON_HANG WHERE ma_don_hang IN @orders; DELETE FROM DON_HANG WHERE ma_don_hang IN @orders", new { orders });
            }
            c.Execute("DELETE FROM SO_DIA_CHI WHERE ma_khach_hang IN @customers; DELETE FROM KHACH_HANG WHERE ma_khach_hang IN @customers", new { customers });
            c.Execute("DELETE FROM BIEN_THE_SP WHERE ma_bien_the IN @variants; DELETE FROM SAN_PHAM WHERE ma_san_pham IN @products; DELETE FROM LOAI_SP WHERE ma_loai=@category; DELETE FROM PHUONG_THUC_THANH_TOAN WHERE ma_phuong_thuc_thanh_toan=@PaymentId",
                new { variants = new[] { VariantId, OtherVariantId }, products = new[] { _productId, _otherProductId }, category = _categoryId, PaymentId });
            if (_promotionId != 0)
                c.Execute("DELETE FROM KM_THEO_VOUCHER WHERE ma_ctkm=@id; DELETE FROM CTKM WHERE ma_ctkm=@id", new { id = _promotionId });
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AdminCommerceTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
