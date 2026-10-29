using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class ProductsController(ShopData db) : Controller
{
    // Quản lý loại sản phẩm: danh sách từ ShopData.CategoryList, số sản phẩm đếm từ ShopData.Products.
    public IActionResult Categories()
    {
        ViewBag.Menu = "categories";
        var rows = db.CategoryList.Select(c => new Row(
            [$"LSP{c.Id:D3}", c.Name, $"{db.Products.Count(p => p.Category == c.Name)} sản phẩm"], null, c.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý loại sản phẩm",
            Subtitle = "Sắp xếp danh mục hàng hóa",
            Sections = [new() { Columns = ["Mã loại", "Tên loại sản phẩm", "Số sản phẩm"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true, Entity = "category" }]
        });
    }

    // Quản lý sản phẩm: view Products/Index.cshtml tự đọc sản phẩm và biến thể từ ShopData (inject trong _ViewImports.cshtml).
    public IActionResult Index()
    {
        ViewBag.Menu = "products";
        return View();
    }
}
