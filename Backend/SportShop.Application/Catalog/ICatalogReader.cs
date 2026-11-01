using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;

namespace SportShop.Application.Catalog;

public interface ICatalogReader
{
    Task<IReadOnlyList<CategoryDto>> CategoriesAsync(CancellationToken cancellationToken);
    Task<PagedResponse<CatalogProduct>> ProductsAsync(ProductSearch search, CancellationToken cancellationToken);
    Task<CatalogProductDetail?> ProductAsync(int productId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogPromotion>> PromotionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InterestDto>> InterestsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentMethodDto>> PaymentMethodsAsync(CancellationToken cancellationToken);
}

public sealed record ProductSearch(
    int? CategoryId = null, string? Q = null, string Sort = "new",
    bool InStockOnly = true, int Page = 1, int PageSize = 20);

// Internal read models retain DB image paths; the HTTP layer resolves public URLs.
public sealed record CatalogProduct(
    int Id, string Name, int CategoryId, string CategoryName, string Unit,
    string? Description, string? ImagePath, decimal? MinPrice, decimal? MaxPrice, long Stock);

public sealed record CatalogVariant(
    int Id, string? Size, string? Color, decimal Price, int Stock, string? ImagePath);

public sealed record CatalogProductDetail(CatalogProduct Product, IReadOnlyList<CatalogVariant> Variants);

public sealed record CatalogPromotion(
    int PromotionId, string Code, string Name, decimal Percent, decimal? MaxDiscount,
    string? RequiredTier, int? PerCustomerLimit, DateTime StartsAt, DateTime EndsAt);
