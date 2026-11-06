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
             Fmt.PayStatus(o.PayStatus), Fmt.OrderStatus(o.Status)],
            Url.Action(nameof(Detail), new { id = o.Code }), o.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý đơn đặt hàng và giao hàng",
            Subtitle = "Tiếp nhận, xử lý và theo dõi giao hàng",
            Sections = [new() { Columns = ["Mã đơn", "Khách hàng", "Ngày đặt", "Tổng tiền", "Thanh toán", "Trạng thái"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true, Entity = "order", CanDelete = false }]
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
            DetailHint = "Chọn một hóa đơn để xem chi tiết",
            Sections = [new() { Columns = ["Số hóa đơn", "Mã đơn", "Khách hàng", "Ngày đặt", "Tổng thanh toán"], Rows = rows, Entity = "order", ExportUrl = Url.Action(nameof(ExportInvoices)) }]
        });
    }

    // Xuất hóa đơn ra Excel từ ShopData.Orders: sheet "Hóa đơn" (danh sách) và sheet "Chi tiết hóa đơn" (các dòng hàng, tên từ ShopData.VariantName()). Gọi từ nút Xuất Excel ở trang Quản lý hóa đơn.
    public IActionResult ExportInvoices()
    {
        var orders = db.Orders.Where(o => o.Invoiced).OrderByDescending(o => o.Id).ToList();
        using var book = new ClosedXML.Excel.XLWorkbook();
        var list = book.Worksheets.Add("Hóa đơn");
        string[] heads = ["Số hóa đơn", "Mã đơn", "Khách hàng", "Ngày đặt", "Tổng thanh toán"];
        for (int i = 0; i < heads.Length; i++) list.Cell(1, i + 1).Value = heads[i];
        var lines = book.Worksheets.Add("Chi tiết hóa đơn");
        string[] lineHeads = ["Số hóa đơn", "Sản phẩm", "Số lượng", "Đơn giá", "Giảm giá", "Thành tiền"];
        for (int i = 0; i < lineHeads.Length; i++) lines.Cell(1, i + 1).Value = lineHeads[i];
        int r = 1, l = 1;
        foreach (var o in orders)
        {
            r++;
            list.Cell(r, 1).Value = o.Code;
            list.Cell(r, 2).Value = $"DH{o.Id:D5}";
            list.Cell(r, 3).Value = db.Customer(o.CustomerId).FullName;
            list.Cell(r, 4).Value = o.Date;
            list.Cell(r, 5).Value = o.Total;
            foreach (var x in o.Lines)
            {
                l++;
                lines.Cell(l, 1).Value = o.Code;
                lines.Cell(l, 2).Value = db.VariantName(x.VariantId);
                lines.Cell(l, 3).Value = x.Qty;
                lines.Cell(l, 4).Value = x.Price;
                lines.Cell(l, 5).Value = x.Discount;
                lines.Cell(l, 6).Value = x.Amount;
            }
        }
        list.Column(4).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        list.Column(5).Style.NumberFormat.Format = "#,##0";
        lines.Columns(4, 6).Style.NumberFormat.Format = "#,##0";
        foreach (var ws in book.Worksheets)
        {
            ws.Row(1).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromHtml("#d6ebe7"));
            ws.Columns().AdjustToContents();
        }
        using var ms = new MemoryStream();
        book.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"hoa-don-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    // Chi tiết đơn hàng / hóa đơn theo mã (Order.Code) từ ShopData.Orders.
    public IActionResult Detail(string id, string? from)
    {
        ViewBag.Menu = from == "invoices" ? "invoices" : "orders";
        var o = db.Orders.FirstOrDefault(x => x.Code == id);
        if (o == null) return NotFound();
        return View(new OrderDetailVm(o, db.Customer(o.CustomerId),
            o.EmployeeId.HasValue ? db.Employee(o.EmployeeId.Value).FirstName : "Khách tự đặt online",
            o.Lines.Select(l => l.VariantId).Distinct().ToDictionary(v => v, v => db.VariantName(v))));
    }
}
