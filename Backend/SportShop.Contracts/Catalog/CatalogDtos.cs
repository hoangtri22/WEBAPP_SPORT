namespace SportShop.Contracts.Catalog;

// Danh mục và số sản phẩm.
public sealed record CategoryDto(int Id, string Name, int ProductCount);
// Một sở thích thể thao.
public sealed record InterestDto(int Id, string Name);
// Một phương thức thanh toán.
public sealed record PaymentMethodDto(int Id, string Code, string Name);

// Sản phẩm trong danh sách trả cho app.
public sealed record ProductSummaryDto(
    int Id, string Name, int CategoryId, string CategoryName, string Unit,
    string? ImageUrl, decimal? MinPrice, decimal? MaxPrice, bool InStock,
    string Currency = "VND");

// Một biến thể trong chi tiết sản phẩm trả cho app.
public sealed record ProductVariantDto(
    int Id, string? Size, string? Color, decimal Price, int Stock, string? ImageUrl);

// Chi tiết sản phẩm trả cho app.
public sealed record ProductDetailDto(
    int Id, string Name, int CategoryId, string CategoryName, string Unit,
    string? Description, string? ImageUrl, decimal? MinPrice, decimal? MaxPrice,
    bool InStock, IReadOnlyList<ProductVariantDto> Variants, string Currency = "VND");

// Chương trình khuyến mãi trả cho app.
public sealed record PromotionDto(
    int PromotionId, string Code, string Name, decimal Percent, decimal? MaxDiscount,
    string? RequiredTier, int? PerCustomerLimit, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);
