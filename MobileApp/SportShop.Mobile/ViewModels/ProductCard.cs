using System.Globalization;
using SportShop.Contracts.Catalog;

namespace SportShop.Mobile.ViewModels;

// Một thẻ sản phẩm hiển thị trên MainPage.
public sealed class ProductCard(ProductSummaryDto product)
{
    public int Id => product.Id;
    public string Name => product.Name;
    public string CategoryName => product.CategoryName;
    public string? ImageUrl => product.ImageUrl;
    public string StockText => product.InStock ? "Còn hàng" : "Tạm hết hàng";
    public string PriceText => product.MinPrice is { } price
        ? price.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " ₫"
        : "Liên hệ";
}
