using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

/// <summary>Đăng nhập, đăng ký, đăng xuất của khách hàng.</summary>
public class AccountController : Controller
{
    readonly DbStatus _db;
    public AccountController(DbStatus db) => _db = db;

    [HttpGet]
    public IActionResult Login() => View("Auth", new AuthVm { IsRegister = false });

    [HttpGet]
    public IActionResult Register() => View("Auth", new AuthVm { IsRegister = true });

    /// <summary>
    /// Đăng nhập. Kiểm tra dữ liệu nhập giống bản thiết kế Figma.
    /// Chưa có cơ sở dữ liệu nên đây là chế độ xem giao diện: nhập hợp lệ là vào được
    /// (giống trang đăng nhập của web admin). Khi nối MySQL sẽ kiểm tra tài khoản thật ở đây.
    /// </summary>
    [HttpPost]
    public IActionResult Login(string username, string password)
    {
        var vm = new AuthVm { IsRegister = false, Username = username ?? "" };
        Validate(vm, username, null, null, password);
        if (vm.Errors.Count > 0) return View("Auth", vm);

        // TODO (khi nối MySQL): tìm tài khoản theo tên đăng nhập, so khớp mật khẩu đã mã hóa
        HttpContext.Session.SetString("customer", username!.Trim());
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Đăng ký. Chỉ kiểm tra dữ liệu; việc tạo tài khoản cần cơ sở dữ liệu.
    /// (Ở giao diện, nút Đăng ký đã chặn trước và hiện thông báo khi chưa có CSDL.)
    /// </summary>
    [HttpPost]
    public IActionResult Register(string username, string email, string phone, string password)
    {
        var vm = new AuthVm { IsRegister = true, Username = username ?? "", Email = email ?? "", Phone = phone ?? "" };
        Validate(vm, username, email, phone, password);
        if (vm.Errors.Count > 0) return View("Auth", vm);

        if (!_db.IsConnected)
        {
            vm.Notice = "Chức năng này sẽ hoạt động khi hệ thống được kết nối cơ sở dữ liệu.";
            vm.NoticeIsError = true;
            return View("Auth", vm);
        }

        // TODO (khi nối MySQL): kiểm tra trùng tên đăng nhập / email, băm mật khẩu, lưu tài khoản + khách hàng
        vm.Notice = "Đăng ký thành công! Bạn có thể đăng nhập ngay.";
        return View("Auth", vm);
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Remove("customer");
        return RedirectToAction("Index", "Home");
    }

    /// <summary>Các quy tắc kiểm tra giống Figma: tên đăng nhập bắt buộc, email đúng dạng, SĐT 10 số, mật khẩu từ 6 ký tự.</summary>
    static void Validate(AuthVm vm, string? username, string? email, string? phone, string? password)
    {
        if (string.IsNullOrWhiteSpace(username)) vm.Errors["username"] = "Vui lòng nhập tên đăng nhập.";
        if (vm.IsRegister && !Regex.IsMatch(email ?? "", @"^[^\s@]+@[^\s@]+\.[^\s@]+$")) vm.Errors["email"] = "Email chưa đúng định dạng.";
        if (vm.IsRegister && !Regex.IsMatch(phone ?? "", @"^0\d{9}$")) vm.Errors["phone"] = "Số điện thoại cần gồm 10 chữ số.";
        if ((password ?? "").Length < 6) vm.Errors["password"] = "Mật khẩu cần có ít nhất 6 ký tự.";
    }
}
