using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

// Quản lý đơn hàng và hóa đơn, xuất Excel; đọc dữ liệu qua ShopData.
public class OrdersController(ShopData db) : Controller
{
    // Bộ lọc "Tìm kiếm nâng cao" của bảng đơn hàng: trạng thái (cột 5), thanh toán (cột 4), giá trị đơn (cột 3), ngày đặt (cột 2), đơn vị vận chuyển và kênh bán (data-f).
    static readonly FilterSpec OrderFilter = new()
    {
        Id = "orders", Noun = "đơn hàng",
        Fields =
        [
            new("status", "Trạng thái đơn", "select", Col: 5),
            new("pay", "Thanh toán", "select", Col: 4),
            new("carrier", "Đơn vị vận chuyển", "select", Data: "carrier"),
            new("total", "Giá trị đơn (đ)", "number", Col: 3),
            new("date", "Ngày đặt", "date", Col: 2),
            new("channel", "Kênh bán", "select", Data: "channel", Options: ["Online", "Tại quầy"]),
        ],
        Quick = [new("Chờ xác nhận", "status", "eq", "Chờ xác nhận"), new("Đang giao", "status", "eq", "Đang giao"), new("Chưa thanh toán", "pay", "eq", "Chưa thanh toán")],
    };

    // Bộ lọc "Tìm kiếm nâng cao" của bảng hóa đơn: tổng tiền (cột 4), ngày lập (cột 3), phương thức thanh toán, thu ngân, khuyến mãi và trạng thái (data-f).
    static readonly FilterSpec InvoiceFilter = new()
    {
        Id = "invoices", Noun = "hóa đơn",
        Fields =
        [
            new("pay", "Phương thức thanh toán", "select", Data: "pay"),
            new("total", "Tổng tiền (đ)", "number", Col: 4),
            new("date", "Ngày lập", "date", Col: 3),
            new("cashier", "Thu ngân", "select", Data: "cashier"),
            new("promo", "Có dùng khuyến mãi", "select", Data: "promo", Options: ["Có", "Không"]),
            new("state", "Trạng thái", "select", Data: "state", Options: ["Đã thanh toán", "Đã hủy"]),
        ],
        Quick = [new("Đã hủy", "state", "eq", "Đã hủy"), new("Có khuyến mãi", "promo", "eq", "Có")],
    };

    // Quản lý đơn hàng: danh sách từ ShopData.Orders; tạo đơn / cập nhật trạng thái qua CrudController với entity "order".
    public IActionResult Index()
    {
        ViewBag.Menu = "orders";
        var rows = db.Orders.OrderByDescending(o => o.Id).Select(o => new Row(
            [$"DH{o.Id:D5}", db.Customer(o.CustomerId).FullName, Fmt.DateTimeDot(o.Date), Fmt.Money(o.Total),
             Fmt.PayStatus(o.PayStatus), Fmt.OrderStatus(o.Status)],
            Url.Action(nameof(Detail), new { id = o.Code }), o.Id, null,
            new() { ["carrier"] = o.Carrier ?? "", ["channel"] = o.EmployeeId == null ? "Online" : "Tại quầy" })).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý đơn đặt hàng và giao hàng",
            Subtitle = "Tiếp nhận, xử lý và theo dõi giao hàng",
            Sections = [new() { Columns = ["Mã đơn", "Khách hàng", "Ngày đặt", "Tổng tiền", "Thanh toán", "Trạng thái"], Rows = rows, Filter = OrderFilter, AddLabel = "Thêm mới", ShowActions = true, Entity = "order", CanDelete = false }]
        });
    }

    // Quản lý hóa đơn: các đơn đã lập hóa đơn (Order.Invoiced) trong ShopData.Orders.
    public IActionResult Invoices()
    {
        ViewBag.Menu = "invoices";
        var rows = db.Orders.Where(o => o.Invoiced).OrderByDescending(o => o.Id).Select(o => new Row(
            [o.Code, $"DH{o.Id:D5}", db.Customer(o.CustomerId).FullName, Fmt.DateTimeDot(o.Date), Fmt.Money(o.Total)],
            Url.Action(nameof(Detail), new { id = o.Code, from = "invoices" }), o.Id, null,
            new()
            {
                ["pay"] = o.PayMethod, ["cashier"] = o.EmployeeId is int emp ? db.Employee(emp).FirstName : "Online",
                ["promo"] = o.Discount > 0 || !string.IsNullOrEmpty(o.Voucher) ? "Có" : "Không", ["state"] = o.Status == "da_huy" ? "Đã hủy" : "Đã thanh toán",
            })).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý hóa đơn",
            Subtitle = "Tra cứu và in hóa đơn bán hàng",
            DetailHint = "Chọn một hóa đơn để xem chi tiết",
            Sections = [new() { Columns = ["Số hóa đơn", "Mã đơn", "Khách hàng", "Ngày đặt", "Tổng thanh toán"], Rows = rows, Filter = InvoiceFilter, Entity = "order", ExportUrl = Url.Action(nameof(ExportInvoices)) }]
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
