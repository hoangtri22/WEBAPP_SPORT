using System.ComponentModel.DataAnnotations;
using SportShop.Application.Catalog;

namespace SportShop.Api.Catalog;

// Tham số truy vấn của API tìm sản phẩm (ProductsController).
public sealed class ProductSearchQuery
{
    [Range(1, int.MaxValue)]
    public int? CategoryId { get; init; }

    [StringLength(100)]
    public string? Q { get; init; }

    [Required, RegularExpression("^(new|price_asc|price_desc|name)$")]
    public string Sort { get; init; } = "new";

    public bool InStockOnly { get; init; } = true;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    // Đổi tham số truy vấn thành ProductSearch cho ICatalogReader.ProductsAsync().
    public ProductSearch ToSearch() => new(CategoryId,
        string.IsNullOrWhiteSpace(Q) ? null : Q.Trim(), Sort, InStockOnly, Page, PageSize);
}
