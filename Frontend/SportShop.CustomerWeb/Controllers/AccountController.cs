using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

// Đăng nhập, đăng ký, đăng xuất của khách, xử lý qua CustomerStore và LoginThrottle.
public class AccountController : Controller
{
    readonly DbStatus _db;
    readonly CustomerStore _store;
    readonly LoginThrottle _throttle;
    readonly GuestStore _guests;
    // Nhận DbStatus, CustomerStore, LoginThrottle và GuestStore.
    public AccountController(DbStatus db, CustomerStore store, LoginThrottle throttle, GuestStore guests) { _db = db; _store = store; _throttle = throttle; _guests = guests; }

    // Hiển thị trang đăng nhập (Views/Account/Auth.cshtml); nhận thông báo từ TempData["LoginNotice"] do CartController.LoginRequired() gửi sang.
    [HttpGet]
    public IActionResult Login()
    {
        var notice = TempData["LoginNotice"] as string;
        return View("Auth", new AuthVm { IsRegister = false, Notice = notice, NoticeIsError = notice != null });
    }

    // Hiển thị trang đăng ký (dùng chung view Auth.cshtml với IsRegister = true).
    [HttpGet]
    public IActionResult Register() => View("Auth", new AuthVm { IsRegister = true });

    // Đăng nhập khách hàng: kiểm tra form bằng Validate(), chặn sai nhiều lần qua LoginThrottle, gọi CustomerStore.Login() đối chiếu TAI_KHOAN/KHACH_HANG trong MySQL rồi lưu customer_id và customer vào Session.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Login(string username, string password)
    {
        var vm = new AuthVm { IsRegister = false, Username = username ?? "" };
        Validate(vm, username, null, null, password);
        if (vm.Errors.Count > 0) return View("Auth", vm);

        if (_db.IsConnected)
        {
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
            DropGuestCart();
        }

        HttpContext.Session.SetString("customer", username!.Trim());
        return RedirectToAction("Index", "Home");
    }

    // Đăng ký khách hàng: kiểm tra form bằng Validate(), gọi CustomerStore.Register() thêm TAI_KHOAN và KHACH_HANG vào MySQL, đồng bộ đơn đã mua bằng số điện thoại đó; thành công thì quay về form đăng nhập.
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

        return View("Auth", new AuthVm { IsRegister = false, Username = username!.Trim(), Notice = "Đăng ký thành công! Lịch sử mua hàng của số điện thoại này (nếu có) đã được đồng bộ, bạn có thể đăng nhập ngay." });
    }

    // Đăng nhập xong: giỏ của khách chưa để lại số điện thoại (hồ sơ tạm guest_id) không được chuyển vào giỏ tài khoản, giỏ tài khoản giữ nguyên; bỏ hồ sơ tạm cùng giỏ đó. Hồ sơ đã có số điện thoại thì giữ lại, chỉ bỏ liên kết với phiên.
    void DropGuestCart()
    {
        if (HttpContext.Session.GetInt32("guest_id") is not int guest) return;
        HttpContext.Session.Remove("guest_id");
        _guests.DiscardPlaceholder(guest);
    }

    // Đăng xuất: xóa customer và customer_id khỏi Session rồi về trang chủ.
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("customer");
        HttpContext.Session.Remove("customer_id");
        return RedirectToAction("Index", "Home");
    }

    // Kiểm tra dữ liệu form đăng nhập/đăng ký (tên đăng nhập, email, SĐT 10 số, mật khẩu từ 6 ký tự); lỗi ghi vào vm.Errors.
    static void Validate(AuthVm vm, string? username, string? email, string? phone, string? password)
    {
        if (string.IsNullOrWhiteSpace(username)) vm.Errors["username"] = "Vui lòng nhập tên đăng nhập.";
        if (vm.IsRegister && !Regex.IsMatch(email ?? "", @"^[^\s@]+@[^\s@]+\.[^\s@]+$")) vm.Errors["email"] = "Email chưa đúng định dạng.";
        if (vm.IsRegister && !Regex.IsMatch(phone ?? "", @"^0\d{9}$")) vm.Errors["phone"] = "Số điện thoại cần gồm 10 chữ số.";
        if ((password ?? "").Length < 6) vm.Errors["password"] = "Mật khẩu cần có ít nhất 6 ký tự.";
    }
}
