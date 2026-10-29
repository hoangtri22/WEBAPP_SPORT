using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class OrdersController(ShopData db) : Controller
{
    // Quản lý đơn hàng: danh sách từ ShopData.Orders; tạo đơn / cập nhật trạng thái qua CrudController với entity "order".
    public IActionResult Index()
    {
        ViewBag.Menu = "orders";
        var rows = db.Orders.OrderByDescending(o => o.Id).Select(o => new Row(
            [$"DH{o.Id:D5}", db.Customer(o.CustomerId).FullName, Fmt.DateTimeDot(o.Date), Fmt.Money(o.Total),
             Fmt.PayStatus(o.PayStatus), o.Carrier, Fmt.OrderStatus(o.Status)],
            Url.Action(nameof(Detail), new { id = o.Code }), o.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý đơn đặt hàng và giao hàng",
            Subtitle = "Tiếp nhận, xử lý và theo dõi giao hàng",
            Note = "Trạng thái đơn: chờ xác nhận → đang chuẩn bị → đang giao → đã giao, hoặc đã hủy (kèm lý do, hàng được hoàn lại tồn kho). Đơn đã thanh toán được lập hóa đơn, xem ở trang \"Quản lý hóa đơn\".",
            Sections = [new() { Columns = ["Mã đơn", "Khách hàng", "Ngày đặt", "Tổng tiền", "Thanh toán", "Giao hàng", "Trạng thái"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true, Entity = "order", CanDelete = false }]
        });
    }

    // Quản lý hóa đơn: các đơn đã lập hóa đơn (Order.Invoiced) trong ShopData.Orders.
    public IActionResult Invoices()
    {
        ViewBag.Menu = "invoices";
        var rows = db.Orders.Where(o => o.Invoiced).OrderByDescending(o => o.Id).Select(o => new Row(
            [o.Code, $"DH{o.Id:D5}", db.Customer(o.CustomerId).FullName, Fmt.DateTimeDot(o.Date), Fmt.Money(o.Total)],
            Url.Action(nameof(Detail), new { id = o.Code, from = "invoices" }), o.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý hóa đơn",
            Subtitle = "Tra cứu và in hóa đơn bán hàng",
            Sections = [new() { Columns = ["Số hóa đơn", "Mã đơn", "Khách hàng", "Ngày đặt", "Tổng thanh toán"], Rows = rows, Entity = "order" }]
        });
    }

    // Chi tiết đơn hàng / hóa đơn theo mã (Order.Code) từ ShopData.Orders.
    public IActionResult Detail(string id, string? from)
    {
        ViewBag.Menu = from == "invoices" ? "invoices" : "orders";
        var o = db.Orders.FirstOrDefault(x => x.Code == id);
        if (o == null) return NotFound();
        return View(o);
    }
}
