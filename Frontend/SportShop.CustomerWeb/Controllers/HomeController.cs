using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;

namespace SportShop.CustomerWeb.Controllers;

public class HomeController : Controller
{
    // Trang chủ cửa hàng: view Home/Index.cshtml tự lấy sản phẩm nổi bật qua CustomerStore.FeaturedProducts() (chưa kết nối CSDL thì dùng StoreData mẫu).
    public IActionResult Index() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
