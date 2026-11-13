using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

// Quản lý nhà cung cấp, đọc dữ liệu qua ShopData.
public class SuppliersController(ShopData db) : Controller
{
    // Bộ lọc "Tìm kiếm nâng cao" của bảng nhà cung cấp: trạng thái hợp tác (cột 4), tỉnh / thành phố (lấy ở cuối địa chỉ) và ngày nhập hàng gần nhất (từ các phiếu nhập).
    static readonly FilterSpec SupplierFilter = new()
    {
        Id = "suppliers", Noun = "nhà cung cấp",
        Fields =
        [
            new("status", "Trạng thái hợp tác", "select", Col: 4),
            new("province", "Tỉnh/thành phố", "select", Data: "province"),
            new("lastImport", "Nhập hàng gần nhất", "date", Data: "lastImport"),
        ],
    };

    // Quản lý nhà cung cấp: danh sách từ ShopData.Suppliers (NHA_CUNG_CAP); nhà cung cấp ngừng hợp tác tách sang bảng riêng có nút Khôi phục.
    public IActionResult Index()
    {
        ViewBag.Menu = "suppliers";

        // Ngày nhập hàng gần nhất của từng nhà cung cấp (theo tên trong các phiếu nhập) và tỉnh / thành phố ở cuối địa chỉ, dùng cho bộ lọc nâng cao.
        var lastImport = db.ImportSlips.GroupBy(p => p.Supplier).ToDictionary(g => g.Key, g => g.Max(p => p.Date));
        var suppliers = db.Suppliers.Where(s => s.Active).Select(s => new Row(
            [$"NCC{s.Id:D2}", s.Name, s.Phone, s.Email, "[g]Đang hợp tác"], Url.Action(nameof(Detail), new { id = s.Id }), s.Id, null,
            new() { ["province"] = s.Address.Split(',').Last().Trim(), ["lastImport"] = lastImport.TryGetValue(s.Name, out var d) ? d.ToString("yyyy-MM-dd") : "" })).ToList();
        var stopped = db.Suppliers.Where(s => !s.Active).Select(s => new Row(
            [$"NCC{s.Id:D2}", s.Name, s.Phone, s.Email, "[x]Ngừng hợp tác"], Url.Action(nameof(Detail), new { id = s.Id }), s.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý nhà cung cấp",
            Subtitle = "Thông tin và trạng thái hợp tác của nhà cung cấp",
            Sections =
            [
                new() { Columns = ["Mã NCC", "Tên nhà cung cấp", "Số điện thoại", "Email", "Trạng thái"], Rows = suppliers, Filter = SupplierFilter, AddLabel = "Thêm nhà cung cấp", ShowActions = true, Entity = "supplier" },
                new() { Heading = "Nhà cung cấp ngừng hợp tác", Columns = ["Mã NCC", "Tên nhà cung cấp", "Số điện thoại", "Email", "Trạng thái"], Rows = stopped, ShowActions = true, Toolbar = false, Entity = "supplier", CanEdit = false, CanDelete = false, RestoreAction = "restore_supplier" },
            ]
        });
    }

    // Chi tiết nhà cung cấp (Views/Suppliers/Detail.cshtml) từ ShopData.Suppliers, kèm các phiếu nhập của nhà cung cấp trong ShopData.ImportSlips.
    public IActionResult Detail(int id)
    {
        ViewBag.Menu = "suppliers";
        var s = db.Suppliers.FirstOrDefault(x => x.Id == id);
        if (s == null) return NotFound();
        return View(new SupplierDetailVm(s, db.ImportSlips.Where(p => p.Supplier == s.Name).OrderByDescending(p => p.Date).ToList()));
    }
}
