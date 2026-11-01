using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

public class ShopController(DbStatus db, ShopStore shop) : Controller
{
    // Danh sách / tìm kiếm sản phẩm: điều kiện đọc vào ProductQuery (q, cat, min, max, size, color, sort) rồi tìm qua ShopStore.Search(); danh mục lấy từ ShopStore.Categories().
    public IActionResult Index(ProductQuery query)
    {
        if (!db.IsConnected) return View("NoDb");
        var categories = shop.Categories();
        var (sizes, colors) = shop.FilterOptions();
        ViewBag.Categories = categories;
        ViewBag.Cat = query.Category; ViewBag.Q = query.Keyword ?? ""; ViewBag.Sort = query.Sort;
        ViewBag.Advanced = new AdvancedSearchVm(query, categories, sizes, colors);
        return View(shop.Search(query));
    }

    // Chi tiết sản phẩm: lấy sản phẩm và biến thể qua ShopStore.Product(id), kèm tối đa 4 sản phẩm cùng loại.
    public IActionResult Detail(int id)
    {
        if (!db.IsConnected) return View("NoDb");
        var p = shop.Product(id);
        if (p == null) return NotFound();
        ViewBag.Related = shop.Products(null, null, "new", 40).Where(x => x.Category == p.Value.Product.Category && x.Id != id).Take(4).ToList();
        return View(p.Value);
    }

    // Trang khuyến mãi: lấy các voucher đang hiệu lực qua ShopStore.Promos().
    public IActionResult Promotions()
    {
        if (!db.IsConnected) return View("NoDb");
        return View(shop.Promos());
    }
}
