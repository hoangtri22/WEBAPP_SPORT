using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class ImportsController(ShopData db) : Controller
{
    // Quản lý nhập hàng: danh sách phiếu nhập từ ShopData.ImportSlips; lập phiếu mới qua CrudController → ShopCommands.SaveImport().
    public IActionResult Index()
    {
        ViewBag.Menu = "imports";

        var slips = db.ImportSlips.Select(p => new Row(
            [$"PN{p.Id:D3}", p.Supplier, Fmt.Date(p.Date), $"{p.Quantity} sản phẩm", p.Employee, "[g]" + Fmt.Money(p.Total)], null, p.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý nhập hàng",
            Subtitle = "Theo dõi phiếu nhập hàng",
            Sections =
            [
                new() { Columns = ["Mã phiếu", "Nhà cung cấp", "Ngày nhập", "Số lượng", "Nhân viên lập", "Tổng giá trị"], Rows = slips, AddLabel = "Thêm mới", ShowActions = true, Entity = "import", CanEdit = false, CanDelete = false },
            ]
        });
    }
}
