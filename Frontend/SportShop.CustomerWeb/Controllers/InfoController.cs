using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;

namespace SportShop.CustomerWeb.Controllers;

/// <summary>Các trang thông tin ở chân trang: chính sách đổi trả, giao hàng, hướng dẫn chọn size, liên hệ, giới thiệu, tuyển dụng.</summary>
public class InfoController : Controller
{
    /// <summary>Hiện trang theo mã (xem InfoPages.All); mã không có thì báo không tìm thấy.</summary>
    public IActionResult Page(string id)
        => InfoPages.All.TryGetValue(id ?? "", out var page) ? View(page) : NotFound();
}
