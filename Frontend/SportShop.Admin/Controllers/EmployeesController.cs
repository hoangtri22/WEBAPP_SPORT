using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class EmployeesController(ShopData db) : Controller
{
    // Quản lý nhân viên: danh sách từ ShopData.Employees (NHAN_VIEN + TAI_KHOAN).
    public IActionResult Index()
    {
        ViewBag.Menu = "employees";
        var rows = db.Employees.Select(e => new Row(
        [
            $"{e.LastName} {e.FirstName}", $"NV{e.Id:D3}", e.Position, Fmt.Money(e.BaseSalary),
            e.Working ? "[g]Đang làm việc" : "[x]Nghỉ việc"
        ], null, e.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý nhân viên",
            Subtitle = "Theo dõi đội ngũ và trạng thái làm việc",
            Sections = [new() { Columns = ["Nhân viên", "Mã NV", "Chức vụ", "Lương cơ bản", "Trạng thái"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true, Entity = "employee" }]
        });
    }
}
