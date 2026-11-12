using System.ComponentModel.DataAnnotations;
using SportShop.Application.Catalog;

namespace SportShop.Api.Catalog;

// Tham số truy vấn của API tìm sản phẩm (ProductsController).
public sealed class ProductSearchQuery : IValidatableObject
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

    [Range(typeof(decimal), "0", "9999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? MinPrice { get; init; }

    [Range(typeof(decimal), "0", "9999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? MaxPrice { get; init; }

    [MaxLength(50)]
    public string[] Sizes { get; init; } = [];

    [MaxLength(50)]
    public string[] Colors { get; init; } = [];

    // Từ chối khoảng giá đảo và giá trị vượt kích thước cột dữ liệu trước khi gọi reader.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinPrice > MaxPrice)
            yield return new ValidationResult("Giá tối thiểu không được lớn hơn giá tối đa.", [nameof(MinPrice), nameof(MaxPrice)]);
        if (Sizes.Any(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 20))
            yield return new ValidationResult("Size phải có từ 1 đến 20 ký tự.", [nameof(Sizes)]);
        if (Colors.Any(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 30))
            yield return new ValidationResult("Màu phải có từ 1 đến 30 ký tự.", [nameof(Colors)]);
    }

    // Đổi tham số truy vấn thành ProductSearch cho ICatalogReader.ProductsAsync().
    public ProductSearch ToSearch() => new(CategoryId,
        string.IsNullOrWhiteSpace(Q) ? null : Q.Trim(), Sort, InStockOnly, Page, PageSize,
        MinPrice, MaxPrice, Sizes.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        Colors.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
}
