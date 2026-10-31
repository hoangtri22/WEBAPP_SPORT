using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class EmployeesController(ShopData db) : Controller
{
    // Tài khoản vai trò Nhân viên không vào được trang Quản lý nhân viên (ẩn khỏi menu và chặn cả khi gõ thẳng đường dẫn).
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (User.IsInRole("Nhân viên")) context.Result = Forbid();
        base.OnActionExecuting(context);
    }

    // Quản lý nhân viên: danh sách từ ShopData.Employees (NHAN_VIEN + TAI_KHOAN); nhân viên đã nghỉ việc tách sang bảng riêng có nút Khôi phục.
    public IActionResult Index()
    {
        ViewBag.Menu = "employees";
        var rows = db.Employees.Where(e => e.Working).Select(e => new Row(
        [
            $"{e.LastName} {e.FirstName}", $"NV{e.Id:D3}", e.Position, Fmt.Money(e.BaseSalary),
            e.Working ? "[g]Đang làm việc" : "[x]Nghỉ việc"
        ], Url.Action(nameof(Detail), new { id = e.Id }), e.Id)).ToList();
        var left = db.Employees.Where(e => !e.Working).Select(e => new Row(
            [$"{e.LastName} {e.FirstName}", $"NV{e.Id:D3}", e.Position, Fmt.Money(e.BaseSalary), "[x]Nghỉ việc"],
            Url.Action(nameof(Detail), new { id = e.Id }), e.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý nhân viên",
            Subtitle = "Theo dõi đội ngũ và trạng thái làm việc",
            Sections =
            [
                new() { Columns = ["Nhân viên", "Mã NV", "Chức vụ", "Lương cơ bản", "Trạng thái"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true, Entity = "employee" },
                new() { Heading = "Nhân viên đã nghỉ việc", Columns = ["Nhân viên", "Mã NV", "Chức vụ", "Lương cơ bản", "Trạng thái"], Rows = left, ShowActions = true, Toolbar = false, Entity = "employee", CanEdit = false, CanDelete = false, RestoreAction = User.IsInRole("Quản lý") ? "restore_employee" : null },
            ]
        });
    }

    // Hồ sơ nhân viên (Views/Employees/Detail.cshtml) từ ShopData.Employees, kèm số đơn đã lập, hóa đơn, phiếu nhập, chăm sóc khách và phản hồi đã trả lời.
    public IActionResult Detail(int id)
    {
        ViewBag.Menu = "employees";
        var e = db.Employees.FirstOrDefault(x => x.Id == id);
        if (e == null) return NotFound();
        string fullName = $"{e.LastName} {e.FirstName}";
        var orders = db.Orders.Where(o => o.EmployeeId == e.Id).OrderByDescending(o => o.Id).ToList();
        return View(new EmployeeDetailVm(e, orders, db.ImportSlips.Count(p => p.Employee == fullName),
            db.CareNotes.Count(n => n.EmployeeId == e.Id), db.FeedbackItems.Count(f => f.ReplyEmployeeId == e.Id),
            orders.Select(o => o.CustomerId).Distinct().ToDictionary(c => c, c => db.Customer(c).FullName)));
    }
}
