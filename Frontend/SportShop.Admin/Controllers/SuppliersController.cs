using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class SuppliersController(ShopData db) : Controller
{
    // Quản lý nhà cung cấp: danh sách từ ShopData.Suppliers (NHA_CUNG_CAP); nhà cung cấp ngừng hợp tác tách sang bảng riêng có nút Khôi phục.
    public IActionResult Index()
    {
        ViewBag.Menu = "suppliers";

        var suppliers = db.Suppliers.Where(s => s.Active).Select(s => new Row(
            [$"NCC{s.Id:D2}", s.Name, s.Phone, s.Email, "[g]Đang hợp tác"], Url.Action(nameof(Detail), new { id = s.Id }), s.Id)).ToList();
        var stopped = db.Suppliers.Where(s => !s.Active).Select(s => new Row(
            [$"NCC{s.Id:D2}", s.Name, s.Phone, s.Email, "[x]Ngừng hợp tác"], Url.Action(nameof(Detail), new { id = s.Id }), s.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý nhà cung cấp",
            Subtitle = "Thông tin và trạng thái hợp tác của nhà cung cấp",
            Note = "Xóa nhà cung cấp sẽ chuyển sang \"Ngừng hợp tác\" để giữ lịch sử nhập hàng và có thể khôi phục; nhà cung cấp ngừng hợp tác không được chọn khi lập phiếu nhập mới.",
            Sections =
            [
                new() { Columns = ["Mã NCC", "Tên nhà cung cấp", "Số điện thoại", "Email", "Trạng thái"], Rows = suppliers, AddLabel = "Thêm nhà cung cấp", ShowActions = true, Entity = "supplier" },
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
        return View(s);
    }
}
