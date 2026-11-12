using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;

namespace SportShop.Application.Catalog;

// Các truy vấn đọc catalog, cài đặt ở MySqlCatalogReader.
public interface ICatalogReader
{
    Task<IReadOnlyList<CategoryDto>> CategoriesAsync(CancellationToken cancellationToken);
    Task<PagedResponse<CatalogProduct>> ProductsAsync(ProductSearch search, CancellationToken cancellationToken);
    Task<ProductFilterOptionsDto> FilterOptionsAsync(bool inStockOnly, CancellationToken cancellationToken);
    Task<CatalogProductDetail?> ProductAsync(int productId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogPromotion>> PromotionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InterestDto>> InterestsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentMethodDto>> PaymentMethodsAsync(CancellationToken cancellationToken);
}

// Điều kiện tìm sản phẩm truyền cho ICatalogReader.ProductsAsync().
public sealed record ProductSearch(
    int? CategoryId = null, string? Q = null, string Sort = "new",
    bool InStockOnly = true, int Page = 1, int PageSize = 20,
    decimal? MinPrice = null, decimal? MaxPrice = null,
    IReadOnlyList<string>? Sizes = null, IReadOnlyList<string>? Colors = null, int? ExcludeProductId = null);

// Model đọc nội bộ giữ đường dẫn ảnh trong CSDL; tầng HTTP đổi thành URL công khai.
public sealed record CatalogProduct(
    int Id, string Name, int CategoryId, string CategoryName, string Unit,
    string? Description, string? ImagePath, decimal? MinPrice, decimal? MaxPrice, long Stock);

// Một biến thể trong chi tiết sản phẩm.
public sealed record CatalogVariant(
    int Id, string? Size, string? Color, decimal Price, int Stock, string? ImagePath);

// Chi tiết sản phẩm kèm các biến thể.
public sealed record CatalogProductDetail(CatalogProduct Product, IReadOnlyList<CatalogVariant> Variants);

// Một chương trình khuyến mãi đọc từ CSDL.
public sealed record CatalogPromotion(
    int PromotionId, string Code, string Name, decimal Percent, decimal? MaxDiscount,
    string? RequiredTier, int? PerCustomerLimit, DateTime StartsAt, DateTime EndsAt);
