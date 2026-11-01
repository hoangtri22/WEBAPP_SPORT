namespace SportShop.Contracts.Catalog;

public sealed record CategoryDto(int Id, string Name, int ProductCount);
public sealed record InterestDto(int Id, string Name);
public sealed record PaymentMethodDto(int Id, string Code, string Name);

public sealed record ProductSummaryDto(
    int Id, string Name, int CategoryId, string CategoryName, string Unit,
    string? ImageUrl, decimal? MinPrice, decimal? MaxPrice, bool InStock,
    string Currency = "VND");

public sealed record ProductVariantDto(
    int Id, string? Size, string? Color, decimal Price, int Stock, string? ImageUrl);

public sealed record ProductDetailDto(
    int Id, string Name, int CategoryId, string CategoryName, string Unit,
    string? Description, string? ImageUrl, decimal? MinPrice, decimal? MaxPrice,
    bool InStock, IReadOnlyList<ProductVariantDto> Variants, string Currency = "VND");

public sealed record PromotionDto(
    int PromotionId, string Code, string Name, decimal Percent, decimal? MaxDiscount,
    string? RequiredTier, int? PerCustomerLimit, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);
