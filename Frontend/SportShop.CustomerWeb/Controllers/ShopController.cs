using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

public class ShopController(DbStatus db, ShopStore shop) : Controller
{
    // Danh sách / tìm kiếm sản phẩm: lọc theo loại (cat), từ khóa (q) và sắp xếp (sort) qua ShopStore.Products(); danh mục lấy từ ShopStore.Categories().
    public IActionResult Index(int? cat, string? q, string sort = "new")
    {
        if (!db.IsConnected) return View("NoDb");
        ViewBag.Categories = shop.Categories();
        ViewBag.Cat = cat; ViewBag.Q = q; ViewBag.Sort = sort;
        return View(shop.Products(cat, q, sort));
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
