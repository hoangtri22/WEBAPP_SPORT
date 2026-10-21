using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>
/// Đăng nhập / đăng xuất (kiểm tra tài khoản trong bảng TAI_KHOAN)
/// và trang "Tài khoản &amp; Phân quyền".
/// </summary>
public class AccountController(ShopData db) : Controller
{
    // GET /  : màn hình đăng nhập (không có sidebar)
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.Bare = true;
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    // POST: chỉ tài khoản quản lý / nhân viên đang hoạt động mới vào được trang quản trị
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl)
    {
        var r = db.CheckLogin(username ?? "", password ?? "");
        if (!r.Ok)
        {
            ViewBag.Bare = true;
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Error = r.Error;
            ViewBag.Username = username;
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, r.FullName),
            new(ClaimTypes.GivenName, r.FirstName),
            new(ClaimTypes.Role, r.Role),
            new("username", r.Username),
            new("position", r.Position),
            new("employee_id", r.EmployeeId.ToString()),
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Home");
    }

    // Đăng xuất: xóa cookie đăng nhập, quay về màn hình đăng nhập
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    /// <summary>
    /// Danh sách tài khoản (bảng TAI_KHOAN).
    /// Vai trò: Quản lý / Nhân viên / Khách hàng. Trạng thái: hoạt động / bị khóa / đã xóa.
    /// </summary>
    public IActionResult Index()
    {
        ViewBag.Menu = "account";
        var rows = db.Accounts.Select(a => new Row([a.Username, a.Role, a.Email, a.Phone,
            a.Status switch { "hoat_dong" => "[g]Đang hoạt động", "bi_khoa" => "[x]Đã khóa", _ => "[r]Đã xóa" }], null, a.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Tài khoản & Phân quyền",
            Subtitle = "Quản lý quyền truy cập hệ thống",
            Note = "Quản lý có toàn quyền, nhân viên xử lý nghiệp vụ bán hàng và chăm sóc khách, khách hàng chỉ quản lý thông tin của mình. Tài khoản bị khóa không đăng nhập được nhưng dữ liệu vẫn được giữ lại.",
            Sections = [new() { Columns = ["Tài khoản", "Vai trò", "Email", "Số điện thoại", "Trạng thái"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true, Entity = "account" }]
        });
    }
}
