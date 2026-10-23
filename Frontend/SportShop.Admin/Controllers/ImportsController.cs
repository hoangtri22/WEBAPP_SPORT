using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>Quản lý nhập hàng: phiếu nhập hàng và nhà cung cấp.</summary>
public class ImportsController(ShopData db) : Controller
{
    public IActionResult Index()
    {
        ViewBag.Menu = "imports";

        // Phiếu nhập (PHIEU_NHAP_HANG): mỗi phiếu cộng vào tồn và cập nhật giá vốn của biến thể
        var slips = db.ImportSlips.Select(p => new Row(
            [$"PN{p.Id:D3}", p.Supplier, "[l]" + p.Items, Fmt.Date(p.Date), $"{p.Quantity} sản phẩm", p.Employee, "[g]" + Fmt.Money(p.Total)], null, p.Id)).ToList();

        // Nhà cung cấp (NHA_CUNG_CAP): đang hợp tác / ngừng hợp tác
        var suppliers = db.Suppliers.Select(s => new Row(
            [$"NCC{s.Id:D2}", s.Name, s.Phone, s.Email, s.Active ? "[g]Đang hợp tác" : "[x]Ngừng hợp tác"], null, s.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý nhập hàng",
            Subtitle = "Theo dõi phiếu nhập và nhà cung cấp",
            Sections =
            [
                new() { Columns = ["Mã phiếu", "Nhà cung cấp", "Mặt hàng nhập", "Ngày nhập", "Số lượng", "Nhân viên lập", "Tổng giá trị"], Rows = slips, AddLabel = "Thêm mới", ShowActions = true, Entity = "import", CanEdit = false, CanDelete = false },
                new() { Heading = "Nhà cung cấp", Columns = ["Mã NCC", "Tên nhà cung cấp", "Số điện thoại", "Email", "Trạng thái"], Rows = suppliers, AddLabel = "Thêm nhà cung cấp", ShowActions = true, Toolbar = false, Entity = "supplier" },
            ]
        });
    }
}
