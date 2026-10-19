using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;

namespace SportShop.CustomerWeb.Controllers;

public class HomeController : Controller
{
    /// <summary>Trang chủ cửa hàng (dùng được khi chưa đăng nhập).</summary>
    public IActionResult Index() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
