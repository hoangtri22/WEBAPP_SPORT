using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>Quản lý hóa đơn &amp; giao hàng, và bán hàng trực tiếp tại quầy.</summary>
public class OrdersController : Controller
{
    /// <summary>Danh sách hóa đơn, mới nhất trước. Nút con mắt mở chi tiết.</summary>
    public IActionResult Index()
    {
        ViewBag.Menu = "orders";
        var rows = SampleData.Orders.OrderByDescending(o => o.Date).Select(o => new Row(
            [o.Code, SampleData.Customer(o.CustomerId).FullName, Fmt.DateTimeDot(o.Date), Fmt.Money(o.Total), Fmt.OrderStatus(o.Status)],
            Url.Action(nameof(Detail), new { id = o.Code }))).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý hóa đơn",
            Subtitle = "Tra cứu và xử lý đơn hàng",
            Note = "Trạng thái hóa đơn: chờ xác nhận → đang chuẩn bị → đang giao → hoàn thành, hoặc đã hủy (kèm lý do, hàng được hoàn lại tồn kho).",
            Sections = [new() { Columns = ["Mã HĐ", "Khách hàng", "Ngày lập", "Tổng tiền", "Trạng thái"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true }]
        });
    }

    /// <summary>Chi tiết một hóa đơn: người nhận, vận chuyển, các dòng sản phẩm, tổng tiền.</summary>
    public IActionResult Detail(string id)
    {
        ViewBag.Menu = "orders";
        var o = SampleData.Orders.FirstOrDefault(x => x.Code == id);
        if (o == null) return NotFound();
        return View(o);
    }

    /// <summary>Bán hàng tại quầy: nhân viên lập hóa đơn trực tiếp cho khách.</summary>
    public IActionResult Pos()
    {
        ViewBag.Menu = "pos";
        return View();
    }
}
