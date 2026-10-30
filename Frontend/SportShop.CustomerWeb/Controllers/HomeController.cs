using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

public class HomeController(DbStatus db, ShopStore shop, CustomerStore store) : Controller
{
    // Trang chủ cửa hàng: lấy danh mục (ShopStore.Categories()) và 8 sản phẩm nổi bật (CustomerStore.FeaturedProducts()), chưa kết nối CSDL thì dùng StoreData mẫu; truyền sang Home/Index.cshtml qua HomeVm.
    public IActionResult Index()
    {
        bool live = db.IsConnected;
        return View(new HomeVm(live, live ? shop.Categories() : [], StoreData.Categories, live ? store.FeaturedProducts(8) : StoreData.Products));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
