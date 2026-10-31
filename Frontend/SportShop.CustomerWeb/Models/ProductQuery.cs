using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SportShop.CustomerWeb.Models;

/// <summary>
/// Điều kiện tìm sản phẩm dùng chung cho mọi chỗ tìm kiếm: ô tìm ở đầu trang, bộ lọc ở trang cửa hàng và tìm kiếm nâng cao.
/// Đọc thẳng từ chuỗi truy vấn (?q=...&amp;cat=...&amp;min=...&amp;max=...&amp;size=...&amp;color=...&amp;sort=...) và được ShopStore.Search() dùng để dựng câu SQL.
/// </summary>
public class ProductQuery
{
    /// <summary>Từ khóa: mỗi từ phải có trong tên sản phẩm hoặc tên loại (không phân biệt hoa thường và dấu).</summary>
    [FromQuery(Name = "q")] public string? Keyword { get; set; }
    [FromQuery(Name = "cat")] public int? Category { get; set; }
    [FromQuery(Name = "min")] public decimal? MinPrice { get; set; }
    [FromQuery(Name = "max")] public decimal? MaxPrice { get; set; }
    [FromQuery(Name = "size")] public string? Size { get; set; }
    [FromQuery(Name = "color")] public string? Color { get; set; }
    /// <summary>new | price_asc | price_desc | name</summary>
    [FromQuery(Name = "sort")] public string Sort { get; set; } = "new";
    [BindNever] public int? Take { get; set; }
    /// <summary>?adv=1: mở sẵn khung tìm kiếm nâng cao (nút "Tìm kiếm nâng cao" ở đầu trang).</summary>
    [FromQuery(Name = "adv")] public string? Adv { get; set; }
    public bool ShowAdvanced => Adv is "1" or "true";

    /// <summary>Các từ trong ô tìm kiếm (tối đa 6 từ).</summary>
    public string[] Words => (Keyword ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6).ToArray();

    public bool HasKeyword => Words.Length > 0;

    /// <summary>Có dùng bộ lọc nâng cao (khoảng giá, size, màu) không.</summary>
    public bool IsAdvanced => MinPrice is > 0 || MaxPrice is > 0 || !string.IsNullOrWhiteSpace(Size) || !string.IsNullOrWhiteSpace(Color);

    /// <summary>Mô tả ngắn các điều kiện nâng cao đang dùng, ví dụ "Giá từ 100.000₫ đến 500.000₫ · Size M".</summary>
    public string AdvancedSummary()
    {
        var parts = new List<string>();
        if (MinPrice is > 0 && MaxPrice is > 0) parts.Add($"Giá từ {Fmt.Money(MinPrice.Value)} đến {Fmt.Money(MaxPrice.Value)}");
        else if (MinPrice is > 0) parts.Add($"Giá từ {Fmt.Money(MinPrice.Value)}");
        else if (MaxPrice is > 0) parts.Add($"Giá đến {Fmt.Money(MaxPrice.Value)}");
        if (!string.IsNullOrWhiteSpace(Size)) parts.Add("Size " + Size);
        if (!string.IsNullOrWhiteSpace(Color)) parts.Add("Màu " + Color);
        return string.Join(" · ", parts);
    }
}
