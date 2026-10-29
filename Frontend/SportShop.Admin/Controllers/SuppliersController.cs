using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class SuppliersController(ShopData db) : Controller
{
    // Quản lý nhà cung cấp: danh sách từ ShopData.Suppliers (NHA_CUNG_CAP).
    public IActionResult Index()
    {
        ViewBag.Menu = "suppliers";

        var suppliers = db.Suppliers.Select(s => new Row(
            [$"NCC{s.Id:D2}", s.Name, s.Phone, s.Email, s.Active ? "[g]Đang hợp tác" : "[x]Ngừng hợp tác"], null, s.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý nhà cung cấp",
            Subtitle = "Thông tin và trạng thái hợp tác của nhà cung cấp",
            Note = "Nhà cung cấp đã có phiếu nhập không xóa được mà chuyển sang \"Ngừng hợp tác\" để giữ lịch sử nhập hàng; nhà cung cấp ngừng hợp tác không được chọn khi lập phiếu nhập mới.",
            Sections = [new() { Columns = ["Mã NCC", "Tên nhà cung cấp", "Số điện thoại", "Email", "Trạng thái"], Rows = suppliers, AddLabel = "Thêm nhà cung cấp", ShowActions = true, Entity = "supplier" }]
        });
    }
}
