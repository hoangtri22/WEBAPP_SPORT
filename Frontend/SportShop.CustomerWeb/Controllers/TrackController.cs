using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

// Theo dõi đơn hàng của khách mua bằng số điện thoại (không cần tài khoản), xử lý qua GuestStore và ShopStore.
public class TrackController(DbStatus db, ShopStore shop, GuestStore guests) : Controller
{
    bool LoggedIn => !string.IsNullOrEmpty(HttpContext.Session.GetString("customer")) && (HttpContext.Session.GetInt32("customer_id") ?? 0) > 0;

    // Trang theo dõi đơn: khách đăng nhập xem ở "Đơn hàng của tôi"; khách vừa mua bằng số điện thoại trong phiên này thì hiện luôn đơn của số đó, còn lại nhập số điện thoại để tra cứu.
    [HttpGet]
    public IActionResult Index()
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        if (LoggedIn) return RedirectToAction("Orders", "Customer");
        string phone = guests.PhoneOf(HttpContext.Session.GetInt32("guest_id") ?? 0) ?? "";
        return View(phone.Length == 0 ? new TrackVm() : Lookup(phone));
    }

    // Tra cứu theo số điện thoại (gửi bằng POST để số không nằm trên địa chỉ trang): số sai định dạng hoặc đã thuộc tài khoản thì báo lỗi, còn lại trả các đơn của khách chưa tài khoản dùng số đó.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Index(string? phone)
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        if (LoggedIn) return RedirectToAction("Orders", "Customer");
        return View(Lookup((phone ?? "").Trim()));
    }

    // Gom đơn của mọi hồ sơ khách chưa tài khoản có số điện thoại này (GuestStore.FindByPhone()), mới nhất trước.
    TrackVm Lookup(string phone)
    {
        if (!Regex.IsMatch(phone, @"^0\d{9}$")) return new TrackVm { Phone = phone, Error = "Số điện thoại cần gồm 10 chữ số, bắt đầu bằng 0." };
        if (guests.PhoneHasAccount(phone)) return new TrackVm { Phone = phone, Error = "Số điện thoại này đã đăng ký tài khoản. Vui lòng đăng nhập để xem đơn hàng." };
        var orders = guests.FindAllByPhone(phone).SelectMany(id => shop.Orders(id)).OrderByDescending(o => o.Id).ToList();
        return new TrackVm { Phone = phone, Orders = orders };
    }
}

// Dữ liệu trang theo dõi đơn (Views/Track/Index.cshtml): Orders = null nghĩa là chưa tra cứu.
public class TrackVm
{
    public string Phone { get; init; } = "";
    public List<MyOrder>? Orders { get; init; }
    public string? Error { get; init; }
}
