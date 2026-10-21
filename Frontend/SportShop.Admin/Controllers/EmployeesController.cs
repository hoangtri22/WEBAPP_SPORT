using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>Quản lý nhân viên (bảng NHAN_VIEN): thêm, sửa, nghỉ việc.</summary>
public class EmployeesController(ShopData db) : Controller
{
    public IActionResult Index()
    {
        ViewBag.Menu = "employees";
        var rows = db.Employees.Select(e => new Row(
        [
            $"{e.LastName} {e.FirstName}", $"NV{e.Id:D3}", e.Position, Fmt.Money(e.BaseSalary),
            e.Working ? "[g]Đang làm việc" : "[x]Nghỉ việc"   // PDF: dang_lam / nghi_viec
        ], null, e.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý nhân viên",
            Subtitle = "Theo dõi đội ngũ và trạng thái làm việc",
            Note = "Nhân viên nghỉ việc chỉ chuyển sang trạng thái \"nghỉ việc\" để vẫn giữ lại lịch sử hóa đơn, phiếu nhập và chăm sóc mà nhân viên đó đã thực hiện.",
            Sections = [new() { Columns = ["Nhân viên", "Mã NV", "Chức vụ", "Lương cơ bản", "Trạng thái"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true, Entity = "employee" }]
        });
    }
}
