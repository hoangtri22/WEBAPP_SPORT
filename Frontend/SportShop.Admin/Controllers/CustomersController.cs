using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>Quản lý khách hàng: hồ sơ, hạng thành viên, nhóm khách, lịch sử mua và chăm sóc.</summary>
public class CustomersController(ShopData db) : Controller
{
    public IActionResult Index()
    {
        ViewBag.Menu = "customers";

        // Bảng chính: danh sách khách hàng, nút con mắt mở hồ sơ + lịch sử mua hàng
        var list = db.Customers.Select(c => new Row(
            [c.FullName, c.Phone, c.Tier, c.Points.ToString("N0"), Fmt.Money(c.TotalSpent), c.Locked ? "[x]Đã khóa" : "[g]Đang hoạt động"],
            Url.Action(nameof(Detail), new { id = c.Id }), c.Id)).ToList();

        // Nhóm khách hàng (NHOM_KHACH_HANG) theo 1 trong 4 tiêu chí, số khách lấy từ KHACH_HANG_NHOM_KH
        var groups = db.CustomerGroups.Select(g => new Row([g.Name, g.Criterion, g.Value, g.Count.ToString()], null, g.Id)).ToList();

        // Lịch hẹn liên hệ lại lấy từ lịch sử chăm sóc
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

    /// <summary>Hồ sơ một khách hàng + lịch sử mua hàng + lịch sử chăm sóc.</summary>
    public IActionResult Detail(int id)
    {
        ViewBag.Menu = "customers";
        var c = db.Customers.FirstOrDefault(x => x.Id == id);
        if (c == null) return NotFound();
        return View(c);
    }
}
