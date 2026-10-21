using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>Quản lý loại sản phẩm và quản lý sản phẩm (kèm biến thể size, màu).</summary>
public class ProductsController(ShopData db) : Controller
{
    /// <summary>Loại sản phẩm (bảng LOAI_SP): quần áo, giày, vợt cầu lông, phụ kiện.</summary>
    public IActionResult Categories()
    {
        ViewBag.Menu = "categories";
        var rows = db.CategoryList.Select(c => new Row(
            [$"LSP{c.Id:D3}", c.Name, $"{db.Products.Count(p => p.Category == c.Name)} sản phẩm"])).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý loại sản phẩm",
            Subtitle = "Sắp xếp danh mục hàng hóa",
            Sections = [new() { Columns = ["Mã loại", "Tên loại sản phẩm", "Số sản phẩm"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true }]
        });
    }

    /// <summary>
    /// Danh sách sản phẩm dạng lưới/danh sách (đổi bằng 2 nút góc phải)
    /// và bảng biến thể (size, màu, giá bán, giá vốn, tồn).
    /// </summary>
    public IActionResult Index()
    {
        ViewBag.Menu = "products";
        return View();
    }
}
