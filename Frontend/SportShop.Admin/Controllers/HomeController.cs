using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class HomeController(ShopData db) : Controller
{
    // Trang Tổng quan: tính doanh thu, số đơn hôm nay / hôm qua, nhân viên, khách hàng, phản hồi chưa trả lời, lịch hẹn và 5 đơn gần nhất từ ShopData, truyền sang Home/Index.cshtml qua HomeVm.
    public IActionResult Index()
    {
        ViewBag.Menu = "home";
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        decimal Revenue(DateTime d) => db.Orders.Where(o => o.Invoiced && o.Date.Date == d).Sum(o => o.Total);
        int OrdersOn(DateTime d) => db.Orders.Count(o => o.Date.Date == d);
        var recent = db.Orders.OrderByDescending(o => o.Id).Take(5).ToList();
        return View(new HomeVm(Revenue(today), Revenue(yesterday), OrdersOn(today), OrdersOn(yesterday),
            db.Employees.Count, db.Employees.Count(e => e.Working), db.ActiveCustomers.Count, db.ActiveCustomers.Count(c => !c.Locked),
            db.FeedbackItems.Count(f => !f.Answered), db.CareNotes.Count(n => n.FollowUp?.Date == today),
            recent, recent.Select(o => o.CustomerId).Distinct().ToDictionary(id => id, id => db.Customer(id).FullName)));
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
