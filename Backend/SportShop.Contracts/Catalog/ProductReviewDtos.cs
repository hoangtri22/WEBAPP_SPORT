namespace SportShop.Contracts.Catalog;

// Đánh giá công khai, không chứa mã khách hàng, đơn hàng hoặc thông tin liên hệ.
public sealed record ProductReviewDto(
    int Id, string Author, int Stars, string Content, DateTimeOffset SentAt, string? Reply);

// Trang đánh giá kèm thống kê của toàn bộ đánh giá công khai trên sản phẩm.
public sealed record ProductReviewsResponse(
    IReadOnlyList<ProductReviewDto> Items, int Page, int PageSize, long TotalItems, decimal? AverageStars);
