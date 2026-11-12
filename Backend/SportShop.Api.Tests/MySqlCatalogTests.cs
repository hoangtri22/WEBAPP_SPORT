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

// Các test này chỉ SELECT trên CSDL test được cấu hình, không thêm hay sửa dữ liệu.
[Trait("Category", "MySql")]
public sealed class MySqlCatalogTests
{
    private static readonly DateTimeOffset FrozenNow = new(2026, 10, 15, 3, 0, 0, TimeSpan.Zero);

    // Kiểm tra readiness không làm đổi số dòng dữ liệu.
    [MySqlFact]
    public async Task Readiness_VerifiesCatalogSchemaWithoutChangingRowCounts()
    {
        using var context = new ReaderContext();
        var before = await LoadRawAsync(context.ConnectionString);
        await context.Readiness.CheckAsync(CancellationToken.None);
        var after = await LoadRawAsync(context.ConnectionString);
        Assert.Equal(before.RowCounts, after.RowCounts);
    }

    // Kiểm tra danh mục bỏ loại lưu trữ và chỉ đếm sản phẩm công khai còn biến thể.
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

    // Kiểm tra trang mặc định và tổng số khớp dữ liệu đọc trực tiếp.
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

    // Đối chiếu lọc biến thể, nhiều lựa chọn, tổ hợp, sắp xếp và phân trang bằng các dòng gốc độc lập.
    [MySqlFact]
    public async Task Products_AdvancedFiltersMatchSameVariantAndKeepStablePages()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        var sample = raw.PublicProducts(true).First(product => raw.VisibleVariants(product.Id)
            .Any(variant => variant.Stock > 0 && variant.Size is not null && variant.Color is not null));
        var variant = raw.VisibleVariants(sample.Id).First(value => value.Stock > 0 && value.Size is not null && value.Color is not null);
        ProductSearch[] searches =
        [
            new(MinPrice: 200000m, MaxPrice: 800000m),
            new(Sizes: ["M", "L"]),
            new(Colors: ["Đen", "Xanh"]),
            new(MinPrice: 0m, MaxPrice: 9999999999.99m, Sizes: ["M", "L"], Colors: ["Đen", "Xanh"]),
            new(CategoryId: sample.CategoryId, Q: sample.Name, MinPrice: variant.Price, MaxPrice: variant.Price,
                Sizes: [variant.Size!], Colors: [variant.Color!]),
            new(Sizes: ["__no_size__"], Colors: ["__no_color__"])
        ];
        foreach (var search in searches)
        foreach (var inStockOnly in new[] { true, false })
        foreach (var sort in new[] { "new", "price_asc", "price_desc" })
        {
            var eligible = raw.PublicProducts(inStockOnly).Where(product =>
                (search.CategoryId is null || product.CategoryId == search.CategoryId)
                && (search.Q is null || product.Name == search.Q)
                && raw.VisibleVariants(product.Id).Any(value => (!inStockOnly || value.Stock > 0)
                    && (search.MinPrice is null || value.Price >= search.MinPrice)
                    && (search.MaxPrice is null || value.Price <= search.MaxPrice)
                    && (search.Sizes is null || search.Sizes.Contains(value.Size?.Trim(), StringComparer.OrdinalIgnoreCase))
                    && (search.Colors is null || search.Colors.Contains(value.Color?.Trim(), StringComparer.OrdinalIgnoreCase))));
            var ordered = sort switch
            {
                "price_asc" => eligible.OrderBy(product => raw.MinPrice(product.Id)).ThenBy(product => product.Id),
                "price_desc" => eligible.OrderByDescending(product => raw.MaxPrice(product.Id)).ThenByDescending(product => product.Id),
                _ => eligible.OrderByDescending(product => product.Id)
            };
            var expected = ordered.Select(product => product.Id).ToArray();
            var first = await context.Reader.ProductsAsync(search with { InStockOnly = inStockOnly, Sort = sort, PageSize = 2 }, CancellationToken.None);
            var second = await context.Reader.ProductsAsync(search with { InStockOnly = inStockOnly, Sort = sort, PageSize = 2, Page = 2 }, CancellationToken.None);
            Assert.Equal(expected.Length, first.TotalItems);
            Assert.Equal(first.TotalItems, second.TotalItems);
            Assert.Equal(expected.Take(2), first.Items.Select(product => product.Id));
            Assert.Equal(expected.Skip(2).Take(2), second.Items.Select(product => product.Id));
            Assert.Empty(first.Items.Select(product => product.Id).Intersect(second.Items.Select(product => product.Id)));
        }
    }

    // Size của biến thể A và màu của biến thể B không tạo thành một lựa chọn thực tế.
    [MySqlFact]
    public async Task Products_DoNotCombineSizeAndColorFromDifferentVariants()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        var mismatch = (from product in raw.PublicProducts(false)
                        from sizeVariant in raw.VisibleVariants(product.Id)
                        from colorVariant in raw.VisibleVariants(product.Id)
                        where sizeVariant.Size is not null && colorVariant.Color is not null
                            && !raw.VisibleVariants(product.Id).Any(value => value.Size == sizeVariant.Size && value.Color == colorVariant.Color)
                        select new { product.Id, Size = sizeVariant.Size, Color = colorVariant.Color }).First();
        var actual = await context.Reader.ProductsAsync(new ProductSearch(InStockOnly: false,
            Sizes: [mismatch.Size], Colors: [mismatch.Color], PageSize: 100), CancellationToken.None);
        Assert.DoesNotContain(actual.Items, product => product.Id == mismatch.Id);
    }

    // Loại sản phẩm đang xem ngay trong SQL để trang gợi ý vẫn đủ số lượng và đúng tổng.
    [MySqlFact]
    public async Task Products_ExcludesCurrentProductBeforePagination()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        var sample = raw.PublicProducts(true).GroupBy(product => product.CategoryId).First(group => group.Count() > 2)
            .OrderByDescending(product => product.Id).First();
        var expected = raw.PublicProducts(true).Where(product => product.CategoryId == sample.CategoryId && product.Id != sample.Id)
            .OrderByDescending(product => product.Id).Select(product => product.Id).ToArray();
        var actual = await context.Reader.ProductsAsync(new ProductSearch(CategoryId: sample.CategoryId,
            PageSize: 2, ExcludeProductId: sample.Id), CancellationToken.None);
        Assert.Equal(expected.Length, actual.TotalItems);
        Assert.Equal(expected.Take(2), actual.Items.Select(product => product.Id));
    }

    // Danh sách lựa chọn phải đến từ toàn bộ sản phẩm công khai, bỏ rỗng/ẩn/lưu trữ và tuân cờ tồn.
    [MySqlFact]
    public async Task FilterOptions_UsesOnlyPublicVisibleVariantsAndHonorsStockFlag()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        foreach (var inStockOnly in new[] { true, false })
        {
            var variants = raw.PublicProducts(false).SelectMany(product => raw.VisibleVariants(product.Id))
                .Where(variant => !inStockOnly || variant.Stock > 0).ToArray();
            var expectedSizes = variants.Select(variant => variant.Size?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expectedColors = variants.Select(variant => variant.Color?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var actual = await context.Reader.FilterOptionsAsync(inStockOnly, CancellationToken.None);
            Assert.True(expectedSizes.SetEquals(actual.Sizes));
            Assert.True(expectedColors.SetEquals(actual.Colors));
            Assert.Equal(actual.Sizes.Count, actual.Sizes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(actual.Colors.Count, actual.Colors.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    // Kiểm tra mọi kiểu sắp xếp ổn định qua các trang, giá null xếp cuối.
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

    // Kiểm tra ký tự đặc biệt và chuỗi tấn công được tìm như văn bản thường.
    [MySqlFact]
    public async Task Products_PercentUnderscoreEscapeAndInjectionTextAreLiteralSearches()
    {
        using var context = new ReaderContext();
        var raw = await LoadRawAsync(context.ConnectionString);
        var eligible = raw.PublicProducts(inStockOnly: false).Select(product => product.Id).ToHashSet();
        await using var connection = await OpenAsync(context.ConnectionString);

        foreach (var query in new[] { "%", "_", "!", "%_!", "%' OR 1=1 --", "Giày" })
        {
            // LOCATE không có ký tự đại diện nên dùng đối chiếu độc lập với phép escape LIKE.
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

    // Kiểm tra chi tiết khớp biến thể đang hiện, kể cả hết hàng, không dùng ảnh biến thể ẩn.
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

    // Kiểm tra chi tiết từ chối sản phẩm ẩn và loại lưu trữ.
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

    // Kiểm tra sản phẩm không biến thể hoặc hết hàng chỉ hiện khi tắt lọc tồn kho.
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

    // Kiểm tra trang tối đa không tràn số, sort lạ không thành SQL.
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

    // Kiểm tra sở thích và phương thức thanh toán dùng mã trong CSDL, không tự thêm dòng.
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

    // Kiểm tra thiếu COD vẫn trả các phương thức khác và không tự thêm.
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

    // Kiểm tra khuyến mãi dùng đồng hồ CSDL, bỏ chương trình ngừng, hết hạn, hết lượt.
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

    // Kiểm tra hủy request được truyền ra, không báo thành CSDL lỗi.
    [MySqlFact]
    public async Task Cancellation_PropagatesInsteadOfReportingDatabaseUnavailable()
    {
        using var context = new ReaderContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.Reader.InterestsAsync(cancellation.Token));
    }

    // Mở kết nối CSDL test.
    private static async Task<MySqlConnection> OpenAsync(string connectionString)
    {
        var connection = new MySqlConnection(connectionString);
        try { await connection.OpenAsync(); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    // Đọc dữ liệu gốc từ CSDL để so sánh.
    private static async Task<RawCatalog> LoadRawAsync(string connectionString)
    {
        await using var connection = await OpenAsync(connectionString);
        using var rows = await connection.QueryMultipleAsync("""
            SELECT ma_loai AS Id, ten_loai AS Name FROM LOAI_SP;
            SELECT ma_san_pham AS Id, ma_loai AS CategoryId, ten_san_pham AS Name, trang_thai AS Status FROM SAN_PHAM;
            SELECT ma_bien_the AS Id, ma_san_pham AS ProductId, gia_ban AS Price, kich_co AS Size, mau_sac AS Color,
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

    // Tạo MySqlCatalogReader với cấu hình test.
    private sealed class ReaderContext : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        public string ConnectionString { get; }
        public ICatalogReader Reader { get; }
        public ICatalogClock Clock { get; }
        public IDatabaseReadiness Readiness { get; }

        // Dựng dịch vụ theo chuỗi kết nối trong biến môi trường.
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

        // Giải phóng dịch vụ.
        public void Dispose() { _scope.Dispose(); _provider.Dispose(); }
    }

    // Đồng hồ cố định để thử thời gian.
    private sealed class FrozenTimeProvider : TimeProvider
    {
        // Trả thời điểm cố định.
        public override DateTimeOffset GetUtcNow() => FrozenNow;
    }

    // Dữ liệu gốc đọc trực tiếp từ CSDL.
    private sealed record RawCatalog(CategoryRaw[] Categories, ProductRaw[] Products, VariantRaw[] Variants,
        PromotionRaw[] Promotions, VoucherRaw[] Vouchers, NamedRaw[] Interests, PaymentRaw[] Payments)
    {
        public int[] RowCounts => [Categories.Length, Products.Length, Variants.Length, Promotions.Length,
            Vouchers.Length, Interests.Length, Payments.Length];
        // Lọc sản phẩm công khai.
        public IEnumerable<ProductRaw> PublicProducts(bool inStockOnly) => Products.Where(product =>
            product.Status == "hien" && Categories.Any(category => category.Id == product.CategoryId && category.Name != "[Đã xóa]")
            && (!inStockOnly || VisibleVariants(product.Id).Any(variant => variant.Stock > 0)));
        // Lọc biến thể đang hiện của sản phẩm.
        public IEnumerable<VariantRaw> VisibleVariants(int productId) => Variants.Where(variant => variant.ProductId == productId && variant.Status == "hien");
        // Giá nhỏ nhất của các biến thể đang hiện.
        public decimal? MinPrice(int productId) => VisibleVariants(productId).Select(variant => (decimal?)variant.Price).Min();
        // Giá lớn nhất của các biến thể đang hiện.
        public decimal? MaxPrice(int productId) => VisibleVariants(productId).Select(variant => (decimal?)variant.Price).Max();
    }

    // Dòng danh mục đọc bằng Dapper.
    private sealed class CategoryRaw { public int Id { get; set; } public string Name { get; set; } = ""; }
    // Dòng mã và tên đọc bằng Dapper.
    private sealed class NamedRaw { public int Id { get; set; } public string Name { get; set; } = ""; }
    // Dòng sản phẩm đọc bằng Dapper.
    private sealed class ProductRaw { public int Id { get; set; } public int CategoryId { get; set; } public string Name { get; set; } = ""; public string Status { get; set; } = ""; }
    // Dòng biến thể đọc bằng Dapper.
    private sealed class VariantRaw { public int Id { get; set; } public int ProductId { get; set; } public decimal Price { get; set; } public int Stock { get; set; } public string Status { get; set; } = ""; public string? ImagePath { get; set; } public string? Size { get; set; } public string? Color { get; set; } }
    // Dòng khuyến mãi đọc bằng Dapper.
    private sealed class PromotionRaw { public int Id { get; set; } public string Type { get; set; } = ""; public string Status { get; set; } = ""; public DateTime StartsAt { get; set; } public DateTime EndsAt { get; set; } public int? Limit { get; set; } public int Used { get; set; } public string? RequiredTier { get; set; } public int? PerCustomerLimit { get; set; } }
    // Dòng voucher đọc bằng Dapper.
    private sealed class VoucherRaw { public int PromotionId { get; set; } public string Code { get; set; } = ""; public decimal Percent { get; set; } }
    // Dòng phương thức thanh toán đọc bằng Dapper.
    private sealed class PaymentRaw { public int Id { get; set; } public string Name { get; set; } = ""; public string Status { get; set; } = ""; }
}

// Bỏ qua test khi chưa đặt biến môi trường chuỗi kết nối MySQL.
public sealed class MySqlFactAttribute : FactAttribute
{
    // Đặt lý do bỏ qua nếu thiếu biến môi trường.
    public MySqlFactAttribute(string environmentVariable = "SPORTSHOP_TEST_MYSQL")
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(environmentVariable)))
            Skip = $"Set {environmentVariable} to a dedicated MySQL test database to run this integration test.";
    }
}
