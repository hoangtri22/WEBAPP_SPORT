using System.Data;
using Dapper;
using MySqlConnector;
using SportShop.Application;
using SportShop.Application.Catalog;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;

namespace SportShop.Infrastructure;

// Cài đặt ICatalogReader: đọc danh mục, sản phẩm, khuyến mãi từ MySQL bằng Dapper.
public sealed class MySqlCatalogReader(MySqlConnectionFactory connections, ICatalogClock clock) : ICatalogReader
{
    private const string ArchivedCategory = "[Đã xóa]";
    private const string CodMethodName = "Thanh toán khi nhận hàng (COD)";

    private const string ProductProjection = """
        SELECT sp.ma_san_pham AS Id, sp.ten_san_pham AS Name,
               l.ma_loai AS CategoryId, l.ten_loai AS CategoryName,
               sp.don_vi_tinh AS Unit, sp.mo_ta AS Description,
               (SELECT b.hinh_anh FROM BIEN_THE_SP b
                WHERE b.ma_san_pham = sp.ma_san_pham AND b.trang_thai = 'hien'
                  AND NULLIF(TRIM(b.hinh_anh), '') IS NOT NULL
                ORDER BY (b.so_luong_ton > 0) DESC, b.ma_bien_the LIMIT 1) AS ImagePath,
               MIN(bt.gia_ban) AS MinPrice, MAX(bt.gia_ban) AS MaxPrice,
               CAST(COALESCE(SUM(bt.so_luong_ton), 0) AS SIGNED) AS Stock
        FROM SAN_PHAM sp
        JOIN LOAI_SP l ON l.ma_loai = sp.ma_loai
        LEFT JOIN BIEN_THE_SP bt ON bt.ma_san_pham = sp.ma_san_pham AND bt.trang_thai = 'hien'
        """;

    private const string ProductGrouping = """
        GROUP BY sp.ma_san_pham, sp.ten_san_pham, l.ma_loai, l.ten_loai, sp.don_vi_tinh, sp.mo_ta
        """;

    private const string SearchPredicate = """
        WHERE sp.trang_thai = 'hien' AND l.ten_loai <> @archived
          AND (@categoryId IS NULL OR sp.ma_loai = @categoryId)
          AND (@pattern IS NULL OR sp.ten_san_pham LIKE @pattern ESCAPE '!' OR l.ten_loai LIKE @pattern ESCAPE '!')
          AND (@inStockOnly = FALSE OR EXISTS (
               SELECT 1 FROM BIEN_THE_SP available
               WHERE available.ma_san_pham = sp.ma_san_pham
                 AND available.trang_thai = 'hien' AND available.so_luong_ton > 0))
        """;

    // Danh mục từ LOAI_SP kèm số sản phẩm đang bán còn hàng (SAN_PHAM, BIEN_THE_SP). Gọi từ CatalogController.
    public Task<IReadOnlyList<CategoryDto>> CategoriesAsync(CancellationToken cancellationToken) => ReadAsync(async () =>
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<CategoryRow>(Command("""
            SELECT l.ma_loai AS Id, l.ten_loai AS Name,
                   (SELECT COUNT(*) FROM SAN_PHAM sp
                    WHERE sp.ma_loai = l.ma_loai AND sp.trang_thai = 'hien'
                      AND EXISTS (SELECT 1 FROM BIEN_THE_SP bt
                                  WHERE bt.ma_san_pham = sp.ma_san_pham
                                    AND bt.trang_thai = 'hien' AND bt.so_luong_ton > 0)) AS ProductCount
            FROM LOAI_SP l WHERE l.ten_loai <> @archived ORDER BY l.ma_loai
            """, new { archived = ArchivedCategory }, cancellationToken));
        return (IReadOnlyList<CategoryDto>)rows.Select(row => new CategoryDto(row.Id, row.Name, row.ProductCount)).ToArray();
    });

    // Tìm, lọc, sắp xếp và phân trang sản phẩm từ SAN_PHAM, LOAI_SP, BIEN_THE_SP. Gọi từ ProductsController.
    public Task<PagedResponse<CatalogProduct>> ProductsAsync(ProductSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);
        if (search.Page < 1 || search.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(search), "Page must be positive and page size must be between 1 and 100.");
        if (search.CategoryId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(search), "Category ID must be positive.");
        if (search.ExcludeProductId is <= 0 || search.MinPrice is < 0 or > 9999999999.99m
            || search.MaxPrice is < 0 or > 9999999999.99m || search.MinPrice > search.MaxPrice)
            throw new ArgumentOutOfRangeException(nameof(search), "Invalid product exclusion or price range.");
        var sizes = FilterValues(search.Sizes, 20);
        var colors = FilterValues(search.Colors, 30);
        var predicate = SearchPredicate;
        if (search.ExcludeProductId.HasValue)
            predicate += "\nAND sp.ma_san_pham <> @excludeProductId";
        if (search.MinPrice.HasValue || search.MaxPrice.HasValue || sizes.Length > 0 || colors.Length > 0)
        {
            // Một biến thể phải thỏa toàn bộ giá, size, màu; tránh ghép điều kiện từ các biến thể khác nhau.
            predicate += """

                AND EXISTS (SELECT 1 FROM BIEN_THE_SP matching
                            WHERE matching.ma_san_pham = sp.ma_san_pham AND matching.trang_thai = 'hien'
                              AND (@inStockOnly = FALSE OR matching.so_luong_ton > 0)
                """;
            if (search.MinPrice.HasValue) predicate += "\nAND matching.gia_ban >= @minPrice";
            if (search.MaxPrice.HasValue) predicate += "\nAND matching.gia_ban <= @maxPrice";
            if (sizes.Length > 0) predicate += "\nAND TRIM(matching.kich_co) IN @sizes";
            if (colors.Length > 0) predicate += "\nAND TRIM(matching.mau_sac) IN @colors";
            predicate += ")";
        }

        var order = search.Sort switch
        {
            "new" => "sp.ma_san_pham DESC",
            "price_asc" => "MinPrice IS NULL, MinPrice ASC, sp.ma_san_pham ASC",
            "price_desc" => "MaxPrice IS NULL, MaxPrice DESC, sp.ma_san_pham DESC",
            "name" => "sp.ten_san_pham ASC, sp.ma_san_pham ASC",
            _ => throw new ArgumentException("Unsupported catalog sort order.", nameof(search))
        };
        var offset = ((long)search.Page - 1) * search.PageSize;
        var parameters = new
        {
            archived = ArchivedCategory, categoryId = search.CategoryId,
            pattern = ContainsPattern(search.Q), inStockOnly = search.InStockOnly,
            take = search.PageSize, offset, minPrice = search.MinPrice, maxPrice = search.MaxPrice,
            sizes, colors, excludeProductId = search.ExcludeProductId
        };

        return ReadAsync(async () =>
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead, isReadOnly: true, cancellationToken);
            var countSql = "SELECT COUNT(*) FROM SAN_PHAM sp JOIN LOAI_SP l ON l.ma_loai = sp.ma_loai\n" + predicate;
            var count = await connection.ExecuteScalarAsync<long>(Command(countSql, parameters, cancellationToken, transaction));
            var rows = await connection.QueryAsync<ProductRow>(Command(
                ProductProjection + "\n" + predicate + "\n" + ProductGrouping + $"\nORDER BY {order}\nLIMIT @take OFFSET @offset",
                parameters, cancellationToken, transaction));
            var products = rows.Select(ToProduct).ToArray();
            await transaction.CommitAsync(cancellationToken);
            return new PagedResponse<CatalogProduct>(products, search.Page, search.PageSize, count);
        });
    }

    // Lấy size/màu thật từ toàn bộ biến thể công khai; SQL loại rỗng và trùng theo collation hiện tại.
    public Task<ProductFilterOptionsDto> FilterOptionsAsync(bool inStockOnly, CancellationToken cancellationToken) => ReadAsync(async () =>
    {
        const string from = """
            FROM BIEN_THE_SP bt
            JOIN SAN_PHAM sp ON sp.ma_san_pham = bt.ma_san_pham
            JOIN LOAI_SP l ON l.ma_loai = sp.ma_loai
            WHERE sp.trang_thai = 'hien' AND l.ten_loai <> @archived AND bt.trang_thai = 'hien'
              AND (@inStockOnly = FALSE OR bt.so_luong_ton > 0)
            """;
        await using var connection = await connections.OpenAsync(cancellationToken);
        using var rows = await connection.QueryMultipleAsync(Command(
            "SELECT DISTINCT TRIM(bt.kich_co) AS Value " + from + " AND NULLIF(TRIM(bt.kich_co), '') IS NOT NULL ORDER BY Value;\n" +
            "SELECT DISTINCT TRIM(bt.mau_sac) AS Value " + from + " AND NULLIF(TRIM(bt.mau_sac), '') IS NOT NULL ORDER BY Value;",
            new { archived = ArchivedCategory, inStockOnly }, cancellationToken));
        return new ProductFilterOptionsDto((await rows.ReadAsync<string>()).ToArray(), (await rows.ReadAsync<string>()).ToArray());
    });

    // Chuẩn hóa và giới hạn danh sách tham số trước khi Dapper dựng các tham số IN.
    private static string[] FilterValues(IReadOnlyList<string>? values, int maximumLength)
    {
        if (values is null) return [];
        if (values.Count > 50 || values.Any(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength))
            throw new ArgumentException("Invalid catalog filter values.", nameof(values));
        return values.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // Chi tiết một sản phẩm kèm biến thể đang hiện trong BIEN_THE_SP. Gọi từ ProductsController.
    public Task<CatalogProductDetail?> ProductAsync(int productId, CancellationToken cancellationToken) => ReadAsync(async () =>
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, isReadOnly: true, cancellationToken);
        var parameters = new { productId, archived = ArchivedCategory };
        var row = await connection.QuerySingleOrDefaultAsync<ProductRow>(Command(
            ProductProjection + "\nWHERE sp.ma_san_pham = @productId AND sp.trang_thai = 'hien' AND l.ten_loai <> @archived\n" + ProductGrouping,
            parameters, cancellationToken, transaction));
        if (row is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var variants = await connection.QueryAsync<VariantRow>(Command("""
            SELECT ma_bien_the AS Id, kich_co AS Size, mau_sac AS Color, gia_ban AS Price,
                   so_luong_ton AS Stock, hinh_anh AS ImagePath
            FROM BIEN_THE_SP WHERE ma_san_pham = @productId AND trang_thai = 'hien'
            ORDER BY gia_ban, ma_bien_the
            """, parameters, cancellationToken, transaction));
        var result = new CatalogProductDetail(ToProduct(row), variants.Select(variant =>
            new CatalogVariant(variant.Id, variant.Size, variant.Color, variant.Price, variant.Stock, variant.ImagePath)).ToArray());
        await transaction.CommitAsync(cancellationToken);
        return result;
    });

    // Voucher từ KM_THEO_VOUCHER JOIN CTKM. Gọi từ CatalogController.
    public Task<IReadOnlyList<CatalogPromotion>> PromotionsAsync(CancellationToken cancellationToken) => ReadAsync(async () =>
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<PromotionRow>(Command("""
            SELECT ct.ma_ctkm AS PromotionId, v.ma_voucher AS Code, ct.ten_ctkm AS Name,
                   v.phan_tram_giam AS Percent, ct.gia_tri_giam_toi_da AS MaxDiscount,
                   ct.hang_ap_dung AS RequiredTier, ct.gioi_han_khach_hang AS PerCustomerLimit,
                   ct.ngay_bat_dau AS StartsAt, ct.ngay_ket_thuc AS EndsAt
            FROM KM_THEO_VOUCHER v JOIN CTKM ct ON ct.ma_ctkm = v.ma_ctkm
            WHERE ct.loai_khuyen_mai = 'theo_voucher' AND ct.trang_thai = 'hoat_dong'
              AND @now BETWEEN ct.ngay_bat_dau AND ct.ngay_ket_thuc
              AND (ct.so_luong_gioi_han IS NULL OR ct.so_luot_da_dung < ct.so_luong_gioi_han)
            ORDER BY v.phan_tram_giam DESC, v.ma_voucher
            """, new { now = clock.LocalNow }, cancellationToken));
        return (IReadOnlyList<CatalogPromotion>)rows.Select(row => new CatalogPromotion(
            row.PromotionId, row.Code, row.Name, row.Percent, row.MaxDiscount, row.RequiredTier,
            row.PerCustomerLimit, row.StartsAt, row.EndsAt)).ToArray();
    });

    // Danh sách sở thích từ SO_THICH. Gọi từ CatalogController.
    public Task<IReadOnlyList<InterestDto>> InterestsAsync(CancellationToken cancellationToken) => ReadAsync(async () =>
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<NamedRow>(Command(
            "SELECT ma_so_thich AS Id, ten_so_thich AS Name FROM SO_THICH ORDER BY ma_so_thich",
            null, cancellationToken));
        return (IReadOnlyList<InterestDto>)rows.Select(row => new InterestDto(row.Id, row.Name)).ToArray();
    });

    // Phương thức thanh toán từ PHUONG_THUC_THANH_TOAN. Gọi từ CatalogController.
    public Task<IReadOnlyList<PaymentMethodDto>> PaymentMethodsAsync(CancellationToken cancellationToken) => ReadAsync(async () =>
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<NamedRow>(Command("""
            SELECT ma_phuong_thuc_thanh_toan AS Id, ten_phuong_thuc_thanh_toan AS Name
            FROM PHUONG_THUC_THANH_TOAN
            WHERE trang_thai = 'hoat_dong'
            ORDER BY ma_phuong_thuc_thanh_toan
            """, null, cancellationToken));
        return (IReadOnlyList<PaymentMethodDto>)rows.Select(row => new PaymentMethodDto(row.Id,
            row.Name == CodMethodName ? "cod" : $"method_{row.Id}", row.Name)).ToArray();
    });

    // Escape từ khóa thành mẫu LIKE %...% (ký tự escape là !).
    private static string? ContainsPattern(string? query) => string.IsNullOrWhiteSpace(query) ? null :
        "%" + query.Trim().Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal).Replace("_", "!_", StringComparison.Ordinal) + "%";

    // Tạo CommandDefinition kèm timeout của MySqlConnectionFactory cho các truy vấn trong file.
    private static CommandDefinition Command(string sql, object? parameters, CancellationToken cancellationToken,
        MySqlTransaction? transaction = null) => new(sql, parameters, transaction,
            commandTimeout: MySqlConnectionFactory.CommandTimeoutSeconds, cancellationToken: cancellationToken);

    // Đổi lỗi MySQL không khả dụng thành DatabaseUnavailableException.
    private static async Task<T> ReadAsync<T>(Func<Task<T>> read)
    {
        try { return await read(); }
        catch (MySqlException exception) when (MySqlConnectionFactory.IsUnavailable(exception))
        {
            throw new DatabaseUnavailableException("The catalog database is unavailable or has an incompatible schema.", exception);
        }
    }

    // Đổi ProductRow sang CatalogProduct.
    private static CatalogProduct ToProduct(ProductRow row) => new(row.Id, row.Name, row.CategoryId,
        row.CategoryName, row.Unit, row.Description, row.ImagePath, row.MinPrice, row.MaxPrice, row.Stock);

    // Dòng danh mục đọc bằng Dapper.
    private sealed class CategoryRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int ProductCount { get; set; }
    }

    // Dòng mã và tên đọc bằng Dapper.
    private sealed class NamedRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    // Dòng sản phẩm đọc bằng Dapper.
    private sealed class ProductRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public string Unit { get; set; } = "";
        public string? Description { get; set; }
        public string? ImagePath { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public long Stock { get; set; }
    }

    // Dòng biến thể đọc bằng Dapper.
    private sealed class VariantRow
    {
        public int Id { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string? ImagePath { get; set; }
    }

    // Dòng khuyến mãi đọc bằng Dapper.
    private sealed class PromotionRow
    {
        public int PromotionId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Percent { get; set; }
        public decimal? MaxDiscount { get; set; }
        public string? RequiredTier { get; set; }
        public int? PerCustomerLimit { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
    }
}
