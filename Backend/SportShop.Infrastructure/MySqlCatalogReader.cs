using System.Data;
using Dapper;
using MySqlConnector;
using SportShop.Application;
using SportShop.Application.Catalog;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;

namespace SportShop.Infrastructure;

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

    public Task<PagedResponse<CatalogProduct>> ProductsAsync(ProductSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);
        if (search.Page < 1 || search.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(search), "Page must be positive and page size must be between 1 and 100.");
        if (search.CategoryId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(search), "Category ID must be positive.");

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
            take = search.PageSize, offset
        };

        return ReadAsync(async () =>
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead, isReadOnly: true, cancellationToken);
            var countSql = "SELECT COUNT(*) FROM SAN_PHAM sp JOIN LOAI_SP l ON l.ma_loai = sp.ma_loai\n" + SearchPredicate;
            var count = await connection.ExecuteScalarAsync<long>(Command(countSql, parameters, cancellationToken, transaction));
            var rows = await connection.QueryAsync<ProductRow>(Command(
                ProductProjection + "\n" + SearchPredicate + "\n" + ProductGrouping + $"\nORDER BY {order}\nLIMIT @take OFFSET @offset",
                parameters, cancellationToken, transaction));
            var products = rows.Select(ToProduct).ToArray();
            await transaction.CommitAsync(cancellationToken);
            return new PagedResponse<CatalogProduct>(products, search.Page, search.PageSize, count);
        });
    }

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

    public Task<IReadOnlyList<InterestDto>> InterestsAsync(CancellationToken cancellationToken) => ReadAsync(async () =>
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<NamedRow>(Command(
            "SELECT ma_so_thich AS Id, ten_so_thich AS Name FROM SO_THICH ORDER BY ma_so_thich",
            null, cancellationToken));
        return (IReadOnlyList<InterestDto>)rows.Select(row => new InterestDto(row.Id, row.Name)).ToArray();
    });

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

    private static string? ContainsPattern(string? query) => string.IsNullOrWhiteSpace(query) ? null :
        "%" + query.Trim().Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal).Replace("_", "!_", StringComparison.Ordinal) + "%";

    private static CommandDefinition Command(string sql, object? parameters, CancellationToken cancellationToken,
        MySqlTransaction? transaction = null) => new(sql, parameters, transaction,
            commandTimeout: MySqlConnectionFactory.CommandTimeoutSeconds, cancellationToken: cancellationToken);

    private static async Task<T> ReadAsync<T>(Func<Task<T>> read)
    {
        try { return await read(); }
        catch (MySqlException exception) when (MySqlConnectionFactory.IsUnavailable(exception))
        {
            throw new DatabaseUnavailableException("The catalog database is unavailable or has an incompatible schema.", exception);
        }
    }

    private static CatalogProduct ToProduct(ProductRow row) => new(row.Id, row.Name, row.CategoryId,
        row.CategoryName, row.Unit, row.Description, row.ImagePath, row.MinPrice, row.MaxPrice, row.Stock);

    private sealed class CategoryRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int ProductCount { get; set; }
    }

    private sealed class NamedRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

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

    private sealed class VariantRow
    {
        public int Id { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string? ImagePath { get; set; }
    }

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
