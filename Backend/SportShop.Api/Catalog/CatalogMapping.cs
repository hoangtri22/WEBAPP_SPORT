using SportShop.Api.Media;
using SportShop.Application.Catalog;
using SportShop.Contracts.Catalog;

namespace SportShop.Api.Catalog;

internal static class CatalogMapping
{
    public static ProductSummaryDto Summary(CatalogProduct product, MediaUrlResolver media) => new(
        product.Id, product.Name, product.CategoryId, product.CategoryName, product.Unit,
        media.Resolve(product.ImagePath), product.MinPrice, product.MaxPrice, product.Stock > 0);

    public static ProductDetailDto Detail(CatalogProductDetail detail, MediaUrlResolver media)
    {
        var product = detail.Product;
        return new(product.Id, product.Name, product.CategoryId, product.CategoryName, product.Unit,
            product.Description, media.Resolve(product.ImagePath), product.MinPrice, product.MaxPrice,
            product.Stock > 0, detail.Variants.Select(variant => new ProductVariantDto(
                variant.Id, variant.Size, variant.Color, variant.Price, variant.Stock,
                media.Resolve(variant.ImagePath))).ToArray());
    }
}
