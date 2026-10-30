using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;

namespace SportShop.CustomerWeb.Controllers;

public class InfoController : Controller
{
    // Trang thông tin tĩnh (chính sách, hướng dẫn, giới thiệu): nội dung lấy từ InfoPages.All theo id, hiển thị bằng Views/Info/Page.cshtml.
    public IActionResult Page(string id)
        => InfoPages.All.TryGetValue(id ?? "", out var page) ? View(page) : NotFound();
}
