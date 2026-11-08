using System.Data;
using Dapper;
using SportShop.Application.Catalog;
using SportShop.Contracts.Catalog;
using SportShop.CustomerWeb.Models;

namespace SportShop.CustomerWeb.Services;

// Đọc đánh giá công khai từ PHAN_HOI cho mobile; không thay đổi luồng gửi phản hồi của web/API.
public sealed class ProductReviewStore(IConfiguration config, ICatalogClock clock)
{
    const string VisibleReviews = """
        FROM PHAN_HOI ph
        WHERE ph.ma_san_pham = @productId AND ph.an_hien = 1
          AND ph.loai = 'danh_gia' AND ph.so_sao BETWEEN 1 AND 5
        """;

    // Kiểm tra sản phẩm đang hiện, đọc tổng/sao trung bình và một trang review trong cùng snapshot MySQL.
    public async Task<ProductReviewsResponse?> ListAsync(int productId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId));
        if (page <= 0) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));

        await using var connection = StoreDatabase.Connection(config.GetConnectionString("Default") ?? "");
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, isReadOnly: true, cancellationToken);
        var parameters = new { productId, archived = Catalog.ArchivedCategory,
            take = pageSize, offset = ((long)page - 1) * pageSize };
        var exists = await connection.ExecuteScalarAsync<int>(Command("""
            SELECT COUNT(*) FROM SAN_PHAM sp JOIN LOAI_SP l ON l.ma_loai = sp.ma_loai
            WHERE sp.ma_san_pham = @productId AND sp.trang_thai = 'hien' AND l.ten_loai <> @archived
            """));
        if (exists == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var totals = await connection.QuerySingleAsync<ReviewTotals>(Command(
            "SELECT COUNT(*) AS TotalItems, AVG(ph.so_sao) AS AverageStars\n" + VisibleReviews));
        var rows = await connection.QueryAsync<ReviewRow>(Command("""
            SELECT ph.ma_phan_hoi AS Id, ph.so_sao AS Stars, ph.noi_dung AS Content, ph.ngay_gui AS SentAt,
                   (SELECT t.noi_dung FROM TRA_LOI_PHAN_HOI t WHERE t.ma_phan_hoi = ph.ma_phan_hoi
                    ORDER BY t.ma_tra_loi DESC LIMIT 1) AS Reply
            """ + "\n" + VisibleReviews + "\nORDER BY ph.ngay_gui DESC, ph.ma_phan_hoi DESC LIMIT @take OFFSET @offset"));
        var items = rows.Select(row => new ProductReviewDto(row.Id, "Khách hàng", row.Stars,
            row.Content, clock.WithOffset(row.SentAt), row.Reply)).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return new ProductReviewsResponse(items, page, pageSize, totals.TotalItems, totals.AverageStars);

        CommandDefinition Command(string sql) => new(sql, parameters, transaction,
            commandTimeout: 5, cancellationToken: cancellationToken);
    }

    sealed class ReviewTotals
    {
        public long TotalItems { get; set; }
        public decimal? AverageStars { get; set; }
    }

    sealed class ReviewRow
    {
        public int Id { get; set; }
        public int Stars { get; set; }
        public string Content { get; set; } = "";
        public DateTime SentAt { get; set; }
        public string? Reply { get; set; }
    }
}
