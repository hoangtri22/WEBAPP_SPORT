using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class CustomersController(ShopData db) : Controller
{
    // Quản lý khách hàng: danh sách khách, nhóm khách và lịch hẹn liên hệ lại lấy từ ShopData (Customers, CustomerGroups, CareNotes).
    public IActionResult Index()
    {
        ViewBag.Menu = "customers";

        var list = db.Customers.Select(c => new Row(
            [c.FullName, c.Phone, c.Tier, c.Points.ToString("N0"), Fmt.Money(c.TotalSpent), c.Locked ? "[x]Đã khóa" : "[g]Đang hoạt động"],
            Url.Action(nameof(Detail), new { id = c.Id }), c.Id)).ToList();

        var groups = db.CustomerGroups.Select(g => new Row([g.Name, g.Criterion, g.Value, g.Count.ToString()], null, g.Id)).ToList();

        var followUps = db.CareNotes.Where(n => n.FollowUp != null).OrderBy(n => n.FollowUp)
            .Select(n => new Row([Fmt.Date(n.FollowUp!.Value), db.Customer(n.CustomerId).FullName, Fmt.Channel(n.Channel), n.Content, db.Employee(n.EmployeeId).FirstName])).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý khách hàng",
            Subtitle = "Thông tin và lịch sử mua hàng của khách hàng",
            Sections =
            [
                new() { Columns = ["Khách hàng", "Số điện thoại", "Hạng", "Điểm tích lũy", "Tổng chi tiêu", "Trạng thái"], Rows = list, AddLabel = "Thêm mới", ShowActions = true, Entity = "customer" },
                new() { Heading = "Nhóm khách hàng", Columns = ["Tên nhóm", "Tiêu chí", "Giá trị", "Số khách"], Rows = groups, AddLabel = "Thêm nhóm", ShowActions = true, Toolbar = false, Entity = "group" },
                new() { Heading = "Lịch hẹn liên hệ lại", Columns = ["Ngày hẹn", "Khách hàng", "Hình thức", "Nội dung", "Nhân viên"], Rows = followUps, Toolbar = false },
            ]
        });
    }

    // Hồ sơ chi tiết một khách hàng (Views/Customers/Detail.cshtml) từ ShopData.Customers.
    public IActionResult Detail(int id)
    {
        ViewBag.Menu = "customers";
        var c = db.Customers.FirstOrDefault(x => x.Id == id);
        if (c == null) return NotFound();
        return View(c);
    }
}
