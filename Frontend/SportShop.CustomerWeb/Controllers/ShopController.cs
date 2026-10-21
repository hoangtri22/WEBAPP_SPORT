using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

/// <summary>Cửa hàng: danh sách sản phẩm (lọc theo loại, tìm kiếm, sắp xếp), chi tiết sản phẩm, khuyến mãi. Cần đã kết nối MySQL.</summary>
public class ShopController(DbStatus db, ShopStore shop) : Controller
{
    /// <summary>Danh sách sản phẩm. cat = mã loại, q = từ khóa, sort = new | price_asc | price_desc | name.</summary>
    public IActionResult Index(int? cat, string? q, string sort = "new")
    {
        if (!db.IsConnected) return View("NoDb");
        ViewBag.Categories = shop.Categories();
        ViewBag.Cat = cat; ViewBag.Q = q; ViewBag.Sort = sort;
        return View(shop.Products(cat, q, sort));
    }

    public IActionResult Detail(int id)
    {
        if (!db.IsConnected) return View("NoDb");
        var p = shop.Product(id);
        if (p == null) return NotFound();
        ViewBag.Related = shop.Products(null, null, "new", 40).Where(x => x.Category == p.Value.Product.Category && x.Id != id).Take(4).ToList();
        return View(p.Value);
    }

    /// <summary>Các mã giảm giá đang áp dụng (dùng ở bước thanh toán).</summary>
    public IActionResult Promotions()
    {
        if (!db.IsConnected) return View("NoDb");
        return View(shop.Promos());
    }
}
