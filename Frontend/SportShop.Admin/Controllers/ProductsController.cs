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
            [$"LSP{c.Id:D3}", c.Name, $"{db.Products.Count(p => p.Category == c.Name)} sản phẩm"], Url.Action(nameof(CategoryDetail), new { id = c.Id }), c.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý loại sản phẩm",
            Subtitle = "Sắp xếp danh mục hàng hóa",
            Sections = [new() { Columns = ["Mã loại", "Tên loại sản phẩm", "Số sản phẩm"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true, Entity = "category" }]
        });
    }

    // Quản lý sản phẩm: lấy loại, sản phẩm đang bán, biến thể theo sản phẩm và các dòng bảng biến thể từ ShopData, truyền sang Products/Index.cshtml qua ProductsVm.
    public IActionResult Index()
    {
        ViewBag.Menu = "products";
        var variantRows = db.ActiveVariants.Select(v => new Row(
            [$"BT{v.Id:D3}", db.Product(v.ProductId).Name, v.Size ?? "-", v.Color ?? "-", Fmt.Money(v.Price), Fmt.Money(v.Cost), v.Stock.ToString()], Url.Action("Detail", new { id = v.ProductId }), v.Id)).ToList();
        return View(new ProductsVm(db.Categories, db.ActiveProducts, db.Variants.GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.ToList()), variantRows));
    }

    // Chi tiết loại sản phẩm (Views/Products/CategoryDetail.cshtml): các sản phẩm thuộc loại lấy từ ShopData.Products và ShopData.Variants.
    public IActionResult CategoryDetail(int id)
    {
        ViewBag.Menu = "categories";
        var c = db.CategoryList.FirstOrDefault(x => x.Id == id);
        if (c == null) return NotFound();
        var products = db.Products.Where(p => p.Category == c.Name).ToList();
        var ids = products.Select(p => p.Id).ToHashSet();
        return View(new CategoryDetailVm(c, products, db.Variants.Where(v => ids.Contains(v.ProductId)).GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.ToList())));
    }

    // Chi tiết sản phẩm (Views/Products/Detail.cshtml): thông tin SAN_PHAM, các biến thể và số lượng đã bán từ ShopData (Products, Variants, Orders).
    public IActionResult Detail(int id)
    {
        ViewBag.Menu = "products";
        var p = db.Products.FirstOrDefault(x => x.Id == id);
        if (p == null) return NotFound();
        var vs = db.Variants.Where(v => v.ProductId == p.Id).ToList();
        var vids = vs.Select(v => v.Id).ToHashSet();
        return View(new ProductDetailVm(p, vs, db.Orders.Where(o => o.Invoiced).SelectMany(o => o.Lines).Where(l => vids.Contains(l.VariantId)).ToList()));
    }
}
