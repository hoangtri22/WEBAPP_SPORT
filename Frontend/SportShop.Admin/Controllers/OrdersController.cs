using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>Quản lý hóa đơn &amp; giao hàng.</summary>
public class OrdersController(ShopData db) : Controller
{
    /// <summary>Danh sách đơn hàng / hóa đơn, mới nhất trước (xếp theo mã đơn nên không đổi khi chỉnh giờ máy). Nút con mắt mở chi tiết.</summary>
    public IActionResult Index()
    {
        ViewBag.Menu = "orders";
        var rows = db.Orders.OrderByDescending(o => o.Id).Select(o => new Row(
            [o.Code, db.Customer(o.CustomerId).FullName, Fmt.DateTimeDot(o.Date), Fmt.Money(o.Total), Fmt.OrderStatus(o.Status)],
            Url.Action(nameof(Detail), new { id = o.Code }), o.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý hóa đơn",
            Subtitle = "Tra cứu và xử lý đơn hàng",
            Note = "Trạng thái hóa đơn: chờ xác nhận → đang chuẩn bị → đang giao → đã giao, hoặc đã hủy (kèm lý do, hàng được hoàn lại tồn kho). Mã HĐ là số hóa đơn khi đơn đã thanh toán, đơn chưa thanh toán hiện mã đơn DHxxxxx.",
            Sections = [new() { Columns = ["Mã HĐ", "Khách hàng", "Ngày lập", "Tổng tiền", "Trạng thái"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true, Entity = "order", CanDelete = false }]
        });
    }

    /// <summary>Chi tiết một hóa đơn: người nhận, vận chuyển, các dòng sản phẩm, tổng tiền.</summary>
    public IActionResult Detail(string id)
    {
        ViewBag.Menu = "orders";
        var o = db.Orders.FirstOrDefault(x => x.Code == id);
        if (o == null) return NotFound();
        return View(o);
    }
}
