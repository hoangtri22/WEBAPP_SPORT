using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class HomeController : Controller
{
    /// <summary>Trang Tổng quan hệ thống (số liệu tính trong view từ dữ liệu MySQL).</summary>
    public IActionResult Index()
    {
        ViewBag.Menu = "home";
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
