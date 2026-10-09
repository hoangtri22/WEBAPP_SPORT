using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>Quản lý loại sản phẩm và quản lý sản phẩm (kèm biến thể size, màu).</summary>
public class ProductsController : Controller
{
    /// <summary>Loại sản phẩm (bảng LOAI_SP): quần áo, giày, vợt cầu lông, phụ kiện.</summary>
    public IActionResult Categories()
    {
        ViewBag.Menu = "categories";
        var rows = SampleData.Categories.Select((c, i) => new Row(
            [$"LSP{i + 1:D3}", c, $"{SampleData.Products.Count(p => p.Category == c)} sản phẩm"])).ToList();

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
