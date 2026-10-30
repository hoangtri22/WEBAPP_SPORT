using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class CustomersController(ShopData db) : Controller
{
    // Quản lý khách hàng: danh sách khách (khách đã xóa tách sang bảng riêng có nút Khôi phục), nhóm khách và lịch hẹn liên hệ lại lấy từ ShopData (Customers, CustomerGroups, CareNotes).
    public IActionResult Index()
    {
        ViewBag.Menu = "customers";

        var list = db.ActiveCustomers.Select(c => new Row(
            [c.FullName, c.Phone, c.Tier, c.Points.ToString("N0"), Fmt.Money(c.TotalSpent), c.Username.Length == 0 ? "[b]Chưa có tài khoản" : c.Locked ? "[x]Đã khóa" : "[g]Đang hoạt động"],
            Url.Action(nameof(Detail), new { id = c.Id }), c.Id, c.Username.Length > 0 ? c.Locked : null)).ToList();

        var deleted = db.Customers.Where(c => c.Deleted).Select(c => new Row(
            [c.FullName, c.Phone, c.Tier, Fmt.Money(c.TotalSpent), "[r]Đã xóa"], Url.Action(nameof(Detail), new { id = c.Id }), c.Id)).ToList();

        var groups = db.CustomerGroups.Select(g => new Row([g.Name, g.Criterion, g.Value, g.Count.ToString()], Url.Action(nameof(GroupDetail), new { id = g.Id }), g.Id)).ToList();

        var followUps = db.CareNotes.Where(n => n.FollowUp != null).OrderBy(n => n.FollowUp)
            .Select(n => new Row([Fmt.Date(n.FollowUp!.Value), db.Customer(n.CustomerId).FullName, Fmt.Channel(n.Channel), n.Content, db.Employee(n.EmployeeId).FirstName])).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý khách hàng",
            Subtitle = "Thông tin và lịch sử mua hàng của khách hàng",
            Sections =
            [
                new() { Columns = ["Khách hàng", "Số điện thoại", "Hạng", "Điểm tích lũy", "Tổng chi tiêu", "Trạng thái"], Rows = list, AddLabel = "Thêm mới", ShowActions = true, Entity = "customer", LockAction = "lock_customer" },
                new() { Heading = "Khách hàng đã xóa", Columns = ["Khách hàng", "Số điện thoại", "Hạng", "Tổng chi tiêu", "Trạng thái"], Rows = deleted, ShowActions = true, Toolbar = false, Entity = "customer", CanEdit = false, CanDelete = false, RestoreAction = "restore_customer" },
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
        var orders = db.Orders.Where(o => o.CustomerId == c.Id).OrderByDescending(o => o.Id).ToList();
        var notes = db.CareNotes.Where(n => n.CustomerId == c.Id).OrderByDescending(n => n.Date).ToList();
        return View(new CustomerDetailVm(c, orders, notes,
            orders.SelectMany(o => o.Lines).Select(l => l.VariantId).Distinct().ToDictionary(v => v, v => db.VariantName(v)),
            notes.Select(n => n.EmployeeId).Distinct().ToDictionary(e => e, e => db.Employee(e).FirstName)));
    }

    // Chi tiết nhóm khách hàng (Views/Customers/GroupDetail.cshtml) từ ShopData.CustomerGroups, kèm danh sách khách thuộc nhóm.
    public IActionResult GroupDetail(int id)
    {
        ViewBag.Menu = "customers";
        var g = db.CustomerGroups.FirstOrDefault(x => x.Id == id);
        if (g == null) return NotFound();
        return View(new GroupDetailVm(g, db.ActiveCustomers.Where(c => c.Groups.Contains(g.Name)).ToList()));
    }
}
