using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

// Quản lý phiếu nhập hàng, đọc qua ShopData và nhập file qua ShopCommands.
public class ImportsController(ShopData db, ShopCommands cmd) : Controller
{
    // Bộ lọc "Tìm kiếm nâng cao" của bảng phiếu nhập: nhà cung cấp (cột 1), ngày lập (cột 2), nhân viên lập (cột 4) và tổng tiền (cột 5).
    static readonly FilterSpec ImportFilter = new()
    {
        Id = "imports", Noun = "phiếu nhập",
        Fields =
        [
            new("supplier", "Nhà cung cấp", "select", Col: 1),
            new("total", "Tổng tiền (đ)", "number", Col: 5),
            new("date", "Ngày lập phiếu", "date", Col: 2),
            new("employee", "Nhân viên lập", "select", Col: 4),
        ],
    };

    // Quản lý nhập hàng: danh sách phiếu nhập từ ShopData.ImportSlips; lập phiếu mới qua CrudController → ShopCommands.SaveImport().
    public IActionResult Index()
    {
        ViewBag.Menu = "imports";

        var slips = db.ImportSlips.Select(p => new Row(
            [$"PN{p.Id:D3}", p.Supplier, Fmt.Date(p.Date), $"{p.Quantity} sản phẩm", p.Employee, "[g]" + Fmt.Money(p.Total)], Url.Action(nameof(Detail), new { id = p.Id }), p.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý nhập hàng",
            Subtitle = "Theo dõi phiếu nhập hàng",
            Sections =
            [
                new() { Columns = ["Mã phiếu", "Nhà cung cấp", "Ngày nhập", "Số lượng", "Nhân viên lập", "Tổng giá trị"], Rows = slips, Filter = ImportFilter, AddLabel = "Thêm mới", Entity = "import", CanEdit = false, CanDelete = false },
            ]
        });
    }

    // Chi tiết phiếu nhập (Views/Imports/Detail.cshtml): thông tin từ ShopData.ImportSlips, các dòng hàng đọc bằng ShopCommands.ImportLines().
    public IActionResult Detail(int id)
    {
        ViewBag.Menu = "imports";
        var p = db.ImportSlips.FirstOrDefault(x => x.Id == id);
        if (p == null) return NotFound();
        return View(new ImportDetailVm(p, cmd.ImportLines(p.Id), db.Suppliers.FirstOrDefault(s => s.Name == p.Supplier)));
    }
}
