using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>
/// Đăng nhập / đăng xuất (giao diện mẫu, chưa xác thực thật)
/// và trang "Tài khoản &amp; Phân quyền".
/// </summary>
public class AccountController : Controller
{
    // GET /  : màn hình đăng nhập (không có sidebar)
    [HttpGet]
    public IActionResult Login()
    {
        ViewBag.Bare = true;
        return View();
    }

    // POST: chưa kiểm tra tài khoản, chuyển thẳng vào Tổng quan
    [HttpPost]
    public IActionResult Login(string username, string password) => RedirectToAction("Index", "Home");

    // Đăng xuất: quay về màn hình đăng nhập
    public IActionResult Logout() => RedirectToAction(nameof(Login));

    /// <summary>
    /// Danh sách tài khoản của nhân viên và khách hàng (bảng TAI_KHOAN).
    /// Vai trò: Quản lý / Nhân viên / Khách hàng. Trạng thái: hoạt động / bị khóa / đã xóa.
    /// </summary>
    public IActionResult Index()
    {
        ViewBag.Menu = "account";
        var rows = new List<Row>();

        // Tài khoản nhân viên (nhân viên nghỉ việc → tài khoản bị khóa)
        foreach (var e in SampleData.Employees)
            rows.Add(new([e.Username, e.Position == "Quản lý" ? "Quản lý" : "Nhân viên", $"{e.Username}@sportshop.vn", e.Phone,
                e.Working ? "[g]Đang hoạt động" : "[x]Đã khóa"]));

        // Tài khoản khách hàng
        foreach (var c in SampleData.Customers)
            rows.Add(new([c.Username, "Khách hàng", c.Email, c.Phone, c.Locked ? "[x]Đã khóa" : "[g]Đang hoạt động"]));

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Tài khoản & Phân quyền",
            Subtitle = "Quản lý quyền truy cập hệ thống",
            Note = "Quản lý có toàn quyền, nhân viên xử lý nghiệp vụ bán hàng và chăm sóc khách, khách hàng chỉ quản lý thông tin của mình. Tài khoản bị khóa không đăng nhập được nhưng dữ liệu vẫn được giữ lại.",
            Sections = [new() { Columns = ["Tài khoản", "Vai trò", "Email", "Số điện thoại", "Trạng thái"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true }]
        });
    }
}
