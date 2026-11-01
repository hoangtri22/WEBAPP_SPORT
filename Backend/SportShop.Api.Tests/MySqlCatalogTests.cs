using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using SportShop.Application;
using SportShop.Application.Catalog;
using SportShop.Application.Health;
using SportShop.Infrastructure;
using Xunit;

namespace SportShop.Api.Tests;

// These tests only SELECT from explicitly configured test databases. They never seed or repair them.
[Trait("Category", "MySql")]
public sealed class MySqlCatalogTests
{
    private static readonly DateTimeOffset FrozenNow = new(2026, 10, 15, 3, 0, 0, TimeSpan.Zero);

    [MySqlFact]
    public async Task Readiness_VerifiesCatalogSchemaWithoutChangingRowCounts()
    {
        using var context = new ReaderContext();
        var before = await LoadRawAsync(context.ConnectionString);
        await context.Readiness.CheckAsync(CancellationToken.None);
        var after = await LoadRawAsync(context.ConnectionString);
        Assert.Equal(before.RowCounts, after.RowCounts);
    }

    [MySqlFact]
    public async Task Categories_ExcludeArchivedAndCountOnlyPublicProductsWithAvailableVariants()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        var actual = await context.Reader.CategoriesAsync(CancellationToken.None);
        var expected = raw.Categories.Where(category => category.Name != "[Đã xóa]").OrderBy(category => category.Id).ToArray();

        Assert.Equal(expected.Select(category => category.Id), actual.Select(category => category.Id));
        foreach (var category in actual)
        {
            Assert.Equal(raw.PublicProducts(inStockOnly: true).Count(product => product.CategoryId == category.Id), category.ProductCount);
            Assert.Equal(raw.Categories.Single(item => item.Id == category.Id).Name, category.Name);
        }
    }

    [MySqlFact]
    public async Task Products_DefaultPageAndCountMatchIndependentRawCatalog()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        var expected = raw.PublicProducts(inStockOnly: true).OrderByDescending(product => product.Id).ToArray();
        var actual = await context.Reader.ProductsAsync(new ProductSearch(), CancellationToken.None);

        Assert.Equal(expected.LongLength, actual.TotalItems);
        Assert.Equal(expected.Take(20).Select(product => product.Id), actual.Items.Select(product => product.Id));
        Assert.Equal(1, actual.Page);
        Assert.Equal(20, actual.PageSize);
        Assert.All(actual.Items, product => Assert.True(product.Stock > 0));

        var categoryId = raw.Categories.First(category => category.Name != "[Đã xóa]").Id;
        var filtered = await context.Reader.ProductsAsync(new ProductSearch(CategoryId: categoryId), CancellationToken.None);
        Assert.Equal(expected.LongCount(product => product.CategoryId == categoryId), filtered.TotalItems);
        Assert.All(filtered.Items, product => Assert.Equal(categoryId, product.CategoryId));
    }

    [MySqlFact]
    public async Task Products_AllSortOrdersAreStableAcrossPagesAndNullPricesRemainLast()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        var publicProducts = raw.PublicProducts(inStockOnly: false).ToArray();
        var eligibleIds = publicProducts.Select(product => product.Id).ToHashSet();
        await using var connection = await OpenAsync(context.ConnectionString);
        var databaseNameOrder = (await connection.QueryAsync<int>(
            "SELECT ma_san_pham FROM SAN_PHAM ORDER BY ten_san_pham, ma_san_pham")).Where(eligibleIds.Contains).ToArray();

        foreach (var sort in new[] { "new", "price_asc", "price_desc", "name" })
        {
            int[] expected = sort switch
            {
                "price_asc" => publicProducts.OrderBy(product => raw.MinPrice(product.Id) is null)
                    .ThenBy(product => raw.MinPrice(product.Id)).ThenBy(product => product.Id).Select(product => product.Id).ToArray(),
                "price_desc" => publicProducts.OrderBy(product => raw.MaxPrice(product.Id) is null)
                    .ThenByDescending(product => raw.MaxPrice(product.Id)).ThenByDescending(product => product.Id).Select(product => product.Id).ToArray(),
                "name" => databaseNameOrder,
                _ => publicProducts.OrderByDescending(product => product.Id).Select(product => product.Id).ToArray()
            };
            var first = await context.Reader.ProductsAsync(new ProductSearch(Sort: sort, InStockOnly: false, PageSize: 7), CancellationToken.None);
            var second = await context.Reader.ProductsAsync(new ProductSearch(Sort: sort, InStockOnly: false, Page: 2, PageSize: 7), CancellationToken.None);
            Assert.Equal(expected.LongLength, first.TotalItems);
            Assert.Equal(expected.Take(7), first.Items.Select(product => product.Id));
            Assert.Equal(expected.Skip(7).Take(7), second.Items.Select(product => product.Id));
            Assert.Empty(first.Items.Select(product => product.Id).Intersect(second.Items.Select(product => product.Id)));
        }
    }

    [MySqlFact]
    public async Task Products_PercentUnderscoreEscapeAndInjectionTextAreLiteralSearches()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        var eligible = raw.PublicProducts(inStockOnly: false).Select(product => product.Id).ToHashSet();
        await using var connection = await OpenAsync(context.ConnectionString);

        foreach (var query in new[] { "%", "_", "!", "%_!", "%' OR 1=1 --", "Giày" })
        {
            // LOCATE has no wildcard syntax and provides an independent DB-collation-aware oracle for LIKE escaping.
            var matched = await connection.QueryAsync<int>("""
                SELECT sp.ma_san_pham FROM SAN_PHAM sp JOIN LOAI_SP l ON l.ma_loai = sp.ma_loai
                WHERE LOCATE(@query, sp.ten_san_pham) > 0 OR LOCATE(@query, l.ten_loai) > 0
                ORDER BY sp.ma_san_pham DESC
                """, new { query });
            var expected = matched.Where(eligible.Contains).ToArray();
            var actual = await context.Reader.ProductsAsync(new ProductSearch(Q: query, InStockOnly: false, PageSize: 100), CancellationToken.None);
            Assert.Equal(expected.LongLength, actual.TotalItems);
            Assert.Equal(expected, actual.Items.Select(product => product.Id));
        }
    }

    [MySqlFact]
    public async Task Details_MatchVisibleVariantsIncludingSoldOutAndNeverUseHiddenVariantImages()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        foreach (var expectedProduct in raw.PublicProducts(inStockOnly: false))
        {
            var actual = await context.Reader.ProductAsync(expectedProduct.Id, CancellationToken.None);
            Assert.NotNull(actual);
            var expectedVariants = raw.VisibleVariants(expectedProduct.Id).OrderBy(variant => variant.Price).ThenBy(variant => variant.Id).ToArray();
            Assert.Equal(expectedVariants.Select(variant => variant.Id), actual.Variants.Select(variant => variant.Id));
            Assert.Equal(expectedProduct.Name, actual.Product.Name);
            Assert.Equal(raw.MinPrice(expectedProduct.Id), actual.Product.MinPrice);
            Assert.Equal(raw.MaxPrice(expectedProduct.Id), actual.Product.MaxPrice);
            Assert.Equal(expectedVariants.Sum(variant => (long)variant.Stock), actual.Product.Stock);

            var expectedImage = expectedVariants.Where(variant => !string.IsNullOrWhiteSpace(variant.ImagePath))
                .OrderByDescending(variant => variant.Stock > 0).ThenBy(variant => variant.Id).FirstOrDefault()?.ImagePath;
            Assert.Equal(expectedImage, actual.Product.ImagePath);
            foreach (var variant in actual.Variants)
            {
                var expected = expectedVariants.Single(item => item.Id == variant.Id);
                Assert.Equal(expected.Stock, variant.Stock);
                Assert.Equal(expected.Price, variant.Price);
                Assert.Equal(expected.ImagePath, variant.ImagePath);
            }
        }
    }

    [MySqlFact]
    public async Task Details_RejectHiddenParentsAndArchivedCategories()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        var publicIds = raw.PublicProducts(inStockOnly: false).Select(product => product.Id).ToHashSet();
        foreach (var product in raw.Products.Where(product => !publicIds.Contains(product.Id)))
            Assert.Null(await context.Reader.ProductAsync(product.Id, CancellationToken.None));
        Assert.Null(await context.Reader.ProductAsync(int.MaxValue, CancellationToken.None));
    }

    [MySqlFact]
    public async Task Products_EmptyVariantSetsAndSoldOutItemsRequireStockFilterToBeDisabled()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        var expectedPublic = raw.PublicProducts(inStockOnly: false).OrderByDescending(product => product.Id).ToArray();
        var actual = await context.Reader.ProductsAsync(new ProductSearch(InStockOnly: false, PageSize: 100), CancellationToken.None);
        Assert.Equal(expectedPublic.LongLength, actual.TotalItems);
        Assert.Equal(expectedPublic.Take(100).Select(product => product.Id), actual.Items.Select(product => product.Id));

        foreach (var product in expectedPublic.Where(product => raw.VisibleVariants(product.Id).All(variant => variant.Stock == 0)))
        {
            var detail = await context.Reader.ProductAsync(product.Id, CancellationToken.None);
            Assert.NotNull(detail);
            Assert.Equal(0, detail.Product.Stock);
            if (!raw.VisibleVariants(product.Id).Any())
            {
                Assert.Empty(detail.Variants);
                Assert.Null(detail.Product.MinPrice);
                Assert.Null(detail.Product.MaxPrice);
            }
        }
    }

    [MySqlFact]
    public async Task Products_MaximumPageDoesNotOverflowAndUnknownSortCannotBecomeSql()
    {
        using var context = new ReaderContext();
        var actual = await context.Reader.ProductsAsync(new ProductSearch(Page: int.MaxValue, PageSize: 100), CancellationToken.None);
        Assert.Empty(actual.Items);
        Assert.Equal(int.MaxValue, actual.Page);
        await Assert.ThrowsAsync<ArgumentException>(() => context.Reader.ProductsAsync(
            new ProductSearch(Sort: "new; SELECT SLEEP(10)"), CancellationToken.None));
    }

    [MySqlFact]
    public async Task InterestsAndActivePaymentMethodsUseDatabaseIdsWithoutAddingPaymentRows()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        var interests = await context.Reader.InterestsAsync(CancellationToken.None);
        Assert.Equal(raw.Interests.OrderBy(interest => interest.Id).Select(interest => (interest.Id, interest.Name)),
            interests.Select(interest => (interest.Id, interest.Name)));

        var methods = await context.Reader.PaymentMethodsAsync(CancellationToken.None);
        var expected = raw.Payments.Where(method => method.Status == "hoat_dong").OrderBy(method => method.Id);
        Assert.Equal(expected.Select(method => (method.Id, method.Name)), methods.Select(method => (method.Id, method.Name)));
        Assert.All(methods, method => Assert.Equal(method.Name == "Thanh toán khi nhận hàng (COD)" ? "cod" : $"method_{method.Id}", method.Code));
        Assert.Equal(raw.Payments.Length, (await LoadRawAsync(context.ConnectionString)).Payments.Length);
    }

    [MySqlFact("SPORTSHOP_TEST_MYSQL_NO_COD")]
    public async Task PaymentMethods_AbsentActiveExactCodStillReturnsOtherActiveMethodsWithoutSeeding()
    {
        using var context = new ReaderContext("SPORTSHOP_TEST_MYSQL_NO_COD");
        await using var connection = await OpenAsync(context.ConnectionString);
        var before = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM PHUONG_THUC_THANH_TOAN");
        var methods = await context.Reader.PaymentMethodsAsync(CancellationToken.None);
        var expected = await connection.QueryAsync<int>("SELECT ma_phuong_thuc_thanh_toan FROM PHUONG_THUC_THANH_TOAN WHERE trang_thai='hoat_dong' ORDER BY ma_phuong_thuc_thanh_toan");
        Assert.Equal(expected, methods.Select(m => m.Id));
        Assert.DoesNotContain(methods, m => m.Code == "cod");
        var after = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM PHUONG_THUC_THANH_TOAN");
        Assert.Equal(before, after);
        await Assert.ThrowsAsync<DatabaseUnavailableException>(() => context.Readiness.CheckAsync(CancellationToken.None));
    }

    [MySqlFact]
    public async Task Promotions_UseConfiguredDatabaseClockAndExcludeInactiveExpiredAndExhaustedPrograms()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        var localNow = new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(localNow, context.Clock.LocalNow);
        var eligiblePrograms = raw.Promotions.Where(promotion => promotion.Type == "theo_voucher"
            && promotion.Status == "hoat_dong" && promotion.StartsAt <= localNow && promotion.EndsAt >= localNow
            && (promotion.Limit is null || promotion.Used < promotion.Limit.Value)).ToDictionary(promotion => promotion.Id);
        var expected = raw.Vouchers.Where(voucher => eligiblePrograms.ContainsKey(voucher.PromotionId))
            .OrderByDescending(voucher => voucher.Percent).ThenBy(voucher => voucher.Code, StringComparer.Ordinal).ToArray();
        var actual = await context.Reader.PromotionsAsync(CancellationToken.None);
        Assert.Equal(expected.Select(voucher => (voucher.PromotionId, voucher.Code)),
            actual.Select(voucher => (voucher.PromotionId, voucher.Code)));
        foreach (var promotion in actual)
        {
            var source = eligiblePrograms[promotion.PromotionId];
            Assert.Equal(source.StartsAt, promotion.StartsAt);
            Assert.Equal(source.EndsAt, promotion.EndsAt);
            Assert.Equal(source.RequiredTier, promotion.RequiredTier);
            Assert.Equal(source.PerCustomerLimit, promotion.PerCustomerLimit);
            Assert.Equal(TimeSpan.FromHours(7), context.Clock.WithOffset(promotion.StartsAt).Offset);
            Assert.Equal(promotion.StartsAt, context.Clock.WithOffset(promotion.StartsAt).DateTime);
        }
    }

    [MySqlFact]
    public async Task Cancellation_PropagatesInsteadOfReportingDatabaseUnavailable()
    {
        using var context = new ReaderContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.Reader.InterestsAsync(cancellation.Token));
    }

    private static async Task<MySqlConnection> OpenAsync(string connectionString)
    {
        var connection = new MySqlConnection(connectionString);
        try { await connection.OpenAsync(); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static async Task<RawCatalog> LoadRawAsync(string connectionString)
    {
        await using var connection = await OpenAsync(connectionString);
        using var rows = await connection.QueryMultipleAsync("""
            SELECT ma_loai AS Id, ten_loai AS Name FROM LOAI_SP;
            SELECT ma_san_pham AS Id, ma_loai AS CategoryId, ten_san_pham AS Name, trang_thai AS Status FROM SAN_PHAM;
            SELECT ma_bien_the AS Id, ma_san_pham AS ProductId, gia_ban AS Price,
                   so_luong_ton AS Stock, trang_thai AS Status, hinh_anh AS ImagePath FROM BIEN_THE_SP;
            SELECT ma_ctkm AS Id, loai_khuyen_mai AS Type, trang_thai AS Status,
                   ngay_bat_dau AS StartsAt, ngay_ket_thuc AS EndsAt,
                   so_luong_gioi_han AS `Limit`, so_luot_da_dung AS Used,
                   hang_ap_dung AS RequiredTier, gioi_han_khach_hang AS PerCustomerLimit FROM CTKM;
            SELECT ma_ctkm AS PromotionId, ma_voucher AS Code, phan_tram_giam AS Percent FROM KM_THEO_VOUCHER;
            SELECT ma_so_thich AS Id, ten_so_thich AS Name FROM SO_THICH;
            SELECT ma_phuong_thuc_thanh_toan AS Id, ten_phuong_thuc_thanh_toan AS Name,
                   trang_thai AS Status FROM PHUONG_THUC_THANH_TOAN;
            """);
        return new RawCatalog(
            (await rows.ReadAsync<CategoryRaw>()).ToArray(), (await rows.ReadAsync<ProductRaw>()).ToArray(),
            (await rows.ReadAsync<VariantRaw>()).ToArray(), (await rows.ReadAsync<PromotionRaw>()).ToArray(),
            (await rows.ReadAsync<VoucherRaw>()).ToArray(), (await rows.ReadAsync<NamedRaw>()).ToArray(),
            (await rows.ReadAsync<PaymentRaw>()).ToArray());
    }

    private sealed class ReaderContext : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        public string ConnectionString { get; }
        public ICatalogReader Reader { get; }
        public ICatalogClock Clock { get; }
        public IDatabaseReadiness Readiness { get; }

        public ReaderContext(string environmentVariable = "SPORTSHOP_TEST_MYSQL")
        {
            ConnectionString = Environment.GetEnvironmentVariable(environmentVariable)
                ?? throw new InvalidOperationException("A dedicated test database must be configured.");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Database:TimeZoneId"] = "Asia/Ho_Chi_Minh"
            }).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<TimeProvider>(new FrozenTimeProvider());
            services.AddSportShopInfrastructure();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateScope();
            Reader = _scope.ServiceProvider.GetRequiredService<ICatalogReader>();
            Clock = _scope.ServiceProvider.GetRequiredService<ICatalogClock>();
            Readiness = _scope.ServiceProvider.GetRequiredService<IDatabaseReadiness>();
        }

        public void Dispose() { _scope.Dispose(); _provider.Dispose(); }
    }

    private sealed class FrozenTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FrozenNow;
    }

    private sealed record RawCatalog(CategoryRaw[] Categories, ProductRaw[] Products, VariantRaw[] Variants,
        PromotionRaw[] Promotions, VoucherRaw[] Vouchers, NamedRaw[] Interests, PaymentRaw[] Payments)
    {
        public int[] RowCounts => [Categories.Length, Products.Length, Variants.Length, Promotions.Length,
            Vouchers.Length, Interests.Length, Payments.Length];
        public IEnumerable<ProductRaw> PublicProducts(bool inStockOnly) => Products.Where(product =>
            product.Status == "hien" && Categories.Any(category => category.Id == product.CategoryId && category.Name != "[Đã xóa]")
            && (!inStockOnly || VisibleVariants(product.Id).Any(variant => variant.Stock > 0)));
        public IEnumerable<VariantRaw> VisibleVariants(int productId) => Variants.Where(variant => variant.ProductId == productId && variant.Status == "hien");
        public decimal? MinPrice(int productId) => VisibleVariants(productId).Select(variant => (decimal?)variant.Price).Min();
        public decimal? MaxPrice(int productId) => VisibleVariants(productId).Select(variant => (decimal?)variant.Price).Max();
    }

    private sealed class CategoryRaw { public int Id { get; set; } public string Name { get; set; } = ""; }
    private sealed class NamedRaw { public int Id { get; set; } public string Name { get; set; } = ""; }
    private sealed class ProductRaw { public int Id { get; set; } public int CategoryId { get; set; } public string Name { get; set; } = ""; public string Status { get; set; } = ""; }
    private sealed class VariantRaw { public int Id { get; set; } public int ProductId { get; set; } public decimal Price { get; set; } public int Stock { get; set; } public string Status { get; set; } = ""; public string? ImagePath { get; set; } }
    private sealed class PromotionRaw { public int Id { get; set; } public string Type { get; set; } = ""; public string Status { get; set; } = ""; public DateTime StartsAt { get; set; } public DateTime EndsAt { get; set; } public int? Limit { get; set; } public int Used { get; set; } public string? RequiredTier { get; set; } public int? PerCustomerLimit { get; set; } }
    private sealed class VoucherRaw { public int PromotionId { get; set; } public string Code { get; set; } = ""; public decimal Percent { get; set; } }
    private sealed class PaymentRaw { public int Id { get; set; } public string Name { get; set; } = ""; public string Status { get; set; } = ""; }
}

public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute(string environmentVariable = "SPORTSHOP_TEST_MYSQL")
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(environmentVariable)))
            Skip = $"Set {environmentVariable} to a dedicated MySQL test database to run this integration test.";
    }
}
