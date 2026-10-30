using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class AccountController(ShopData db) : Controller
{
    // Trang đăng nhập quản trị (Views/Account/Login.cshtml); đã đăng nhập thì về trang Tổng quan.
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.Bare = true;
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    // Đăng nhập quản trị: gọi ShopData.CheckLogin() kiểm tra TAI_KHOAN/NHAN_VIEN trong MySQL; đúng thì tạo cookie đăng nhập kèm claim họ tên, vai trò, employee_id.
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

    // Đăng xuất: xóa cookie đăng nhập rồi về trang Login.
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    // Tài khoản & phân quyền: danh sách từ ShopData.Accounts, tài khoản đã xóa (trang_thai = da_xoa) tách sang bảng riêng có nút Khôi phục; thêm/sửa/xóa/khóa qua CrudController với entity "account".
    public IActionResult Index()
    {
        ViewBag.Menu = "account";
        var me = User.FindFirst("username")?.Value;
        bool manager = User.IsInRole("Quản lý");
        var rows = db.Accounts.Where(a => a.Status != "da_xoa").Select(a => new Row([a.Username, a.Role, a.Email, a.Phone,
            a.Status == "bi_khoa" ? "[x]Đã khóa" : "[g]Đang hoạt động"], Url.Action(nameof(Detail), new { id = a.Id }), a.Id,
            a.Username == me ? null : a.Status == "bi_khoa")).ToList();
        var deleted = db.Accounts.Where(a => a.Status == "da_xoa").Select(a => new Row([a.Username, a.Role, a.Email, a.Phone, "[r]Đã xóa"],
            Url.Action(nameof(Detail), new { id = a.Id }), a.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Tài khoản & Phân quyền",
            Subtitle = "Quản lý quyền truy cập hệ thống",
            Sections =
            [
                new() { Columns = ["Tài khoản", "Vai trò", "Email", "Số điện thoại", "Trạng thái"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true, Entity = "account", LockAction = manager ? "lock_account" : null },
                new() { Heading = "Tài khoản đã xóa", Columns = ["Tài khoản", "Vai trò", "Email", "Số điện thoại", "Trạng thái"], Rows = deleted, ShowActions = true, Toolbar = false, Entity = "account", CanEdit = false, CanDelete = false, RestoreAction = manager ? "restore_account" : null },
            ]
        });
    }

    // Chi tiết một tài khoản (Views/Account/Detail.cshtml) từ ShopData.Accounts, kèm hồ sơ nhân viên / khách hàng gắn với tài khoản.
    public IActionResult Detail(int id)
    {
        ViewBag.Menu = "account";
        var a = db.Accounts.FirstOrDefault(x => x.Id == id);
        if (a == null) return NotFound();
        return View(new AccountDetailVm(a, db.Employees.FirstOrDefault(e => e.Username == a.Username), db.Customers.FirstOrDefault(c => c.Username == a.Username)));
    }
}
