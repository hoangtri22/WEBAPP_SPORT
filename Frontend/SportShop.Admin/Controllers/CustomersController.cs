using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>Quản lý khách hàng: hồ sơ, hạng thành viên, nhóm khách, lịch sử mua và chăm sóc.</summary>
public class CustomersController : Controller
{
    public IActionResult Index()
    {
        ViewBag.Menu = "customers";

        // Bảng chính: danh sách khách hàng, nút con mắt mở hồ sơ + lịch sử mua hàng
        var list = SampleData.Customers.Select(c => new Row(
            [c.FullName, c.Phone, c.Tier, c.Points.ToString("N0"), Fmt.Money(c.TotalSpent), c.Locked ? "[x]Đã khóa" : "[g]Đang hoạt động"],
            Url.Action(nameof(Detail), new { id = c.Id }))).ToList();

        // Nhóm khách hàng theo 1 trong 4 tiêu chí: độ tuổi / giới tính / sở thích / mức chi tiêu
        var groups = new List<Row>
        {
            new(["Học sinh - sinh viên", "Độ tuổi", "Từ 15 đến 24 tuổi", SampleData.Customers.Count(c => c.Age is >= 15 and <= 24).ToString()]),
            new(["Khách nữ", "Giới tính", "Nữ", SampleData.Customers.Count(c => c.Gender == "Nữ").ToString()]),
            new(["Yêu thích cầu lông", "Sở thích", "Cầu lông", SampleData.Customers.Count(c => c.Interests.Contains("Cầu lông")).ToString()]),
            new(["Khách chi tiêu cao", "Mức chi tiêu", "Từ 5.000.000đ trở lên", SampleData.Customers.Count(c => c.TotalSpent >= 5_000_000).ToString()]),
        };

        // Lịch hẹn liên hệ lại lấy từ lịch sử chăm sóc
        var followUps = SampleData.CareNotes.Where(n => n.FollowUp != null).OrderBy(n => n.FollowUp)
            .Select(n => new Row([Fmt.Date(n.FollowUp!.Value), SampleData.Customer(n.CustomerId).FullName, Fmt.Channel(n.Channel), n.Content, SampleData.Employee(n.EmployeeId).FirstName])).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý khách hàng",
            Subtitle = "Thông tin và lịch sử mua hàng của khách hàng",
            Sections =
            [
                new() { Columns = ["Khách hàng", "Số điện thoại", "Hạng", "Điểm tích lũy", "Tổng chi tiêu", "Trạng thái"], Rows = list, AddLabel = "Thêm mới", ShowActions = true },
                new() { Heading = "Nhóm khách hàng", Columns = ["Tên nhóm", "Tiêu chí", "Giá trị", "Số khách"], Rows = groups, AddLabel = "Thêm nhóm", ShowActions = true, Toolbar = false },
                new() { Heading = "Lịch hẹn liên hệ lại", Columns = ["Ngày hẹn", "Khách hàng", "Hình thức", "Nội dung", "Nhân viên"], Rows = followUps, Toolbar = false },
            ]
        });
    }

    /// <summary>Hồ sơ một khách hàng + lịch sử mua hàng + lịch sử chăm sóc.</summary>
    public IActionResult Detail(int id)
    {
        ViewBag.Menu = "customers";
        var c = SampleData.Customers.FirstOrDefault(x => x.Id == id);
        if (c == null) return NotFound();
        return View(c);
    }
}
