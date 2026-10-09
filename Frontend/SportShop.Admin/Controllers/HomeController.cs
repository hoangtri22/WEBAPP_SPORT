using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class HomeController : Controller
{
    /// <summary>Trang Tổng quan hệ thống (số liệu tính trực tiếp trong view từ dữ liệu mẫu).</summary>
    public IActionResult Index()
    {
        ViewBag.Menu = "home";
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
