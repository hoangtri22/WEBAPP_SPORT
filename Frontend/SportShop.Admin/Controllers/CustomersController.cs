using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

// Quản lý khách hàng, nhóm khách và hồ sơ chi tiết, đọc dữ liệu qua ShopData.
public class CustomersController(ShopData db) : Controller
{
    // Bộ lọc "Tìm kiếm nâng cao" của bảng khách hàng: hạng, trạng thái, điểm, tổng chi tiêu lấy từ cột bảng; "lần mua gần nhất" lấy từ data-f của dòng.
    static readonly FilterSpec CustomerFilter = new()
    {
        Id = "customers", Noun = "khách hàng",
        Fields =
        [
            new("hang", "Hạng thành viên", "select", Col: 3, Options: ["Thường", "Bạc", "Vàng"]),
            new("status", "Trạng thái tài khoản", "select", Col: 6),
            new("spend", "Tổng chi tiêu (đ)", "number", Col: 5),
            new("points", "Điểm tích lũy", "number", Col: 4),
            new("last", "Lần mua gần nhất", "select", Data: "last", Options: ["Trong 30 ngày", "31 – 90 ngày", "Trên 90 ngày", "Chưa từng mua"]),
        ],
        Quick =
        [
            new("Khách VIP", "hang", "eq", "Vàng"), new("Sắp rời bỏ", "last", "eq", "Trên 90 ngày"),
            new("Chưa từng mua", "last", "eq", "Chưa từng mua"), new("Đang bị khóa", "status", "eq", "Đã khóa"),
        ],
    };

    // Quản lý khách hàng: danh sách khách (khách đã xóa tách sang bảng riêng có nút Khôi phục), nhóm khách và lịch hẹn liên hệ lại lấy từ ShopData (Customers, CustomerGroups, CareNotes); cột Thiết bị của khách không tài khoản lấy từ GuestCustomerDevices().
    public IActionResult Index()
    {
        ViewBag.Menu = "customers";

        var devices = db.GuestCustomerDevices();
        // Ngày mua gần nhất của từng khách (bỏ đơn đã hủy) để lọc theo "lần mua gần nhất".
        var lastBuy = db.Orders.Where(o => o.Status != "da_huy").GroupBy(o => o.CustomerId).ToDictionary(g => g.Key, g => g.Max(o => o.Date));
        string LastBuy(int id) => !lastBuy.TryGetValue(id, out var d) ? "Chưa từng mua" : (DateTime.Today - d.Date).Days switch { <= 30 => "Trong 30 ngày", <= 90 => "31 – 90 ngày", _ => "Trên 90 ngày" };
        var list = db.ActiveCustomers.Select(c => new Row(
            [c.FullName, c.Phone, devices.GetValueOrDefault(c.Id, "-"), c.Tier, c.Points.ToString("N0"), Fmt.Money(c.TotalSpent), c.Username.Length == 0 ? "[b]Chưa có tài khoản" : c.Locked ? "[x]Đã khóa" : "[g]Đang hoạt động"],
            Url.Action(nameof(Detail), new { id = c.Id }), c.Id, c.Username.Length > 0 ? c.Locked : null, new() { ["last"] = LastBuy(c.Id) })).ToList();

        var deleted = db.Customers.Where(c => c.Deleted).Select(c => new Row(
            [c.FullName, c.Phone, c.Tier, Fmt.Money(c.TotalSpent), "[r]Đã xóa"], Url.Action(nameof(Detail), new { id = c.Id }), c.Id)).ToList();

        var groups = db.CustomerGroups.Select(g => new Row([g.Name, g.Criterion, g.Value, g.Count.ToString()], Url.Action(nameof(GroupDetail), new { id = g.Id }), g.Id)).ToList();

        var followUps = db.CareNotes.Where(n => n.FollowUp != null).OrderBy(n => n.FollowUp)
            .Select(n => new Row([Fmt.Date(n.FollowUp!.Value), db.Customer(n.CustomerId).FullName, Fmt.Channel(n.Channel), n.Content, db.Employee(n.EmployeeId).FirstName])).ToList();

        var guests = db.GuestDevices().Select(g => new Row([g.Name, g.Model, g.Os, Fmt.DateTimeS(g.FirstSeen), Fmt.DateTimeS(g.LastSeen), g.Visits.ToString("N0")])).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý khách hàng",
            Subtitle = "Thông tin và lịch sử mua hàng của khách hàng",
            Sections =
            [
                new() { Columns = ["Khách hàng", "Số điện thoại", "Thiết bị", "Hạng", "Điểm tích lũy", "Tổng chi tiêu", "Trạng thái"], Rows = list, Filter = CustomerFilter, AddLabel = "Thêm mới", ShowActions = true, Entity = "customer", LockAction = "lock_customer" },
                new() { Heading = "Khách hàng đã xóa", Columns = ["Khách hàng", "Số điện thoại", "Hạng", "Tổng chi tiêu", "Trạng thái"], Rows = deleted, ShowActions = true, Toolbar = false, Entity = "customer", CanEdit = false, CanDelete = false, RestoreAction = "restore_customer" },
                new() { Heading = "Khách dùng Mobile App (không tài khoản)", Columns = ["Tên thiết bị", "Dòng máy", "Hệ điều hành", "Lần đầu", "Gần nhất", "Số lần vào"], Rows = guests, Toolbar = false },
                new() { Heading = "Nhóm khách hàng", Columns = ["Tên nhóm", "Tiêu chí", "Giá trị", "Số khách"], Rows = groups, AddLabel = "Thêm nhóm", ShowActions = true, Toolbar = false, Entity = "group" },
                new() { Heading = "Lịch hẹn liên hệ lại", Columns = ["Ngày hẹn", "Khách hàng", "Hình thức", "Nội dung", "Nhân viên"], Rows = followUps, Toolbar = false },
            ]
        });
    }

    // Hồ sơ chi tiết một khách hàng (Views/Customers/Detail.cshtml) từ ShopData.Customers; chỉ Quản lý nhận mật khẩu gốc qua ShopData.CustomerPassword().
    public IActionResult Detail(int id)
    {
        ViewBag.Menu = "customers";
        var c = db.Customers.FirstOrDefault(x => x.Id == id);
        if (c == null) return NotFound();
        var orders = db.Orders.Where(o => o.CustomerId == c.Id).OrderByDescending(o => o.Id).ToList();
        var notes = db.CareNotes.Where(n => n.CustomerId == c.Id).OrderByDescending(n => n.Date).ToList();
        return View(new CustomerDetailVm(c, User.IsInRole("Quản lý") ? db.CustomerPassword(c.Id) : "", orders, notes,
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
