using System.ComponentModel.DataAnnotations;
using SportShop.Application.Catalog;

namespace SportShop.Api.Catalog;

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

    public ProductSearch ToSearch() => new(CategoryId,
        string.IsNullOrWhiteSpace(Q) ? null : Q.Trim(), Sort, InStockOnly, Page, PageSize);
}
