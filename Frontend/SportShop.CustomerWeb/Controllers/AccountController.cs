using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

/// <summary>Đăng nhập, đăng ký, đăng xuất của khách hàng.</summary>
public class AccountController : Controller
{
    readonly DbStatus _db;
    readonly CustomerStore _store;
    readonly LoginThrottle _throttle;
    public AccountController(DbStatus db, CustomerStore store, LoginThrottle throttle) { _db = db; _store = store; _throttle = throttle; }

    [HttpGet]
    public IActionResult Login() => View("Auth", new AuthVm { IsRegister = false });

    [HttpGet]
    public IActionResult Register() => View("Auth", new AuthVm { IsRegister = true });

    /// <summary>
    /// Đăng nhập. Kiểm tra dữ liệu nhập giống bản thiết kế Figma.
    /// Đã kết nối MySQL: kiểm tra tài khoản khách hàng trong bảng TAI_KHOAN.
    /// Chưa kết nối: chế độ xem giao diện, nhập hợp lệ là vào được.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Login(string username, string password)
    {
        var vm = new AuthVm { IsRegister = false, Username = username ?? "" };
        Validate(vm, username, null, null, password);
        if (vm.Errors.Count > 0) return View("Auth", vm);

        if (_db.IsConnected)
        {
            // Sai mật khẩu quá nhiều lần thì khóa tạm thời để chống dò mật khẩu
            string? ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            int minutesLeft = _throttle.MinutesLeft(username!, ip);
            if (minutesLeft > 0)
            {
                vm.Notice = $"Bạn đăng nhập sai quá nhiều lần. Vui lòng thử lại sau {minutesLeft} phút.";
                vm.NoticeIsError = true;
                return View("Auth", vm);
            }

            var (customerId, error) = _store.Login(username!, password);
            if (customerId == null)
            {
                _throttle.Fail(username!, ip);
                vm.Notice = error;
                vm.NoticeIsError = true;
                return View("Auth", vm);
            }
            _throttle.Reset(username!, ip);
            HttpContext.Session.SetInt32("customer_id", customerId.Value);
        }

        HttpContext.Session.SetString("customer", username!.Trim());
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Đăng ký: tạo tài khoản khách hàng (TAI_KHOAN + KHACH_HANG).
    /// Chưa kết nối CSDL thì chỉ kiểm tra dữ liệu và hiện thông báo.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
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

        var errors = _store.Register(username!.Trim(), email!.Trim(), phone!.Trim(), password);
        if (errors.Count > 0)
        {
            foreach (var (k, v) in errors) vm.Errors[k] = v;
            return View("Auth", vm);
        }

        // Đăng ký xong chuyển sang trang đăng nhập, điền sẵn tên đăng nhập
        return View("Auth", new AuthVm { IsRegister = false, Username = username!.Trim(), Notice = "Đăng ký thành công! Bạn có thể đăng nhập ngay." });
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Remove("customer");
        HttpContext.Session.Remove("customer_id");
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
