using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public record StatCol(string Name, bool Money = true);

public record StatRow(string Label, decimal[] Values);

public record StatTable(string Key, string Title, string First, List<StatCol> Cols, List<StatRow> Rows, decimal[] Total);

public class StatsVm
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public string Group { get; init; } = "month";
    public string Kind { get; init; } = "all";
    public int Invoices { get; init; }
    public decimal Total { get; init; }
    public List<StatTable> Tables { get; init; } = [];
}

public class StatsController(ShopData db) : Controller
{
    static readonly (string Key, string Text)[] Kinds =
    [
        ("all", "Báo cáo tổng hợp"), ("range", "Theo khoảng ngày"), ("period", "Theo tháng / quý / năm"),
        ("employee", "Theo nhân viên và năm"), ("customer", "Theo khách hàng"), ("product", "Theo sản phẩm và quý"),
    ];

    public static IReadOnlyList<(string Key, string Text)> KindList => Kinds;

    // Thống kê tổng tiền hóa đơn (HOA_DON.tong_thanh_toan, tính theo ngày xuất hóa đơn) trong [from, to]: năm bảng theo khoảng ngày, kỳ, nhân viên × năm, khách hàng × quý/năm, sản phẩm × quý.
    public IActionResult Index(DateTime? from, DateTime? to, string? kind, string? group)
    {
        ViewBag.Menu = "stats";
        return View(Build(from, to, kind, group));
    }

    // Xuất các bảng đang xem (hoặc cả 5 bảng của báo cáo tổng hợp) ra file Excel, mỗi bảng một sheet.
    public IActionResult Export(DateTime? from, DateTime? to, string? kind, string? group)
    {
        var vm = Build(from, to, kind, group);
        using var book = new XLWorkbook();
        foreach (var t in vm.Tables)
        {
            var ws = book.Worksheets.Add(t.Title.Length > 31 ? t.Title[..31] : t.Title);
            ws.Cell(1, 1).Value = t.Title.ToUpperInvariant();
            ws.Cell(2, 1).Value = $"Từ {vm.From:dd/MM/yyyy} đến {vm.To:dd/MM/yyyy}";
            ws.Range(1, 1, 1, 1).Style.Font.SetBold().Font.SetFontSize(14);
            int hr = 4, nc = t.Cols.Count;
            ws.Cell(hr, 1).Value = t.First;
            for (int i = 0; i < nc; i++) ws.Cell(hr, i + 2).Value = t.Cols[i].Name;
            ws.Range(hr, 1, hr, nc + 1).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#d6ebe7"));
            int r = hr;
            foreach (var row in t.Rows)
            {
                r++;
                ws.Cell(r, 1).Value = row.Label;
                for (int i = 0; i < nc; i++) ws.Cell(r, i + 2).Value = row.Values[i];
            }
            r++;
            ws.Cell(r, 1).Value = "Tổng cộng";
            for (int i = 0; i < nc; i++) ws.Cell(r, i + 2).Value = t.Total[i];
            ws.Range(r, 1, r, nc + 1).Style.Font.SetBold();
            for (int i = 0; i < nc; i++)
                ws.Range(hr + 1, i + 2, r, i + 2).Style.NumberFormat.Format = t.Cols[i].Money ? "#,##0" : "0";
            ws.Columns().AdjustToContents();
        }
        if (vm.Tables.Count == 0) book.Worksheets.Add("Thống kê");
        using var ms = new MemoryStream();
        book.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"thong-ke-{vm.Kind}-{DateTime.Today:yyyyMMdd}.xlsx");
    }

    StatsVm Build(DateTime? from, DateTime? to, string? kind, string? group)
    {
        var all = db.InvoiceRows();
        var end = (to ?? (all.Count == 0 ? DateTime.Today : all[^1].Date)).Date;
        var start = (from ?? new DateTime(end.Year, 1, 1)).Date;
        if (start > end) (start, end) = (end, start);
        kind = Kinds.Any(k => k.Key == kind) ? kind! : "all";
        group = group is "quarter" or "year" ? group : "month";

        var inv = all.Where(i => i.Date >= start && i.Date < end.AddDays(1)).ToList();
        var lines = db.InvoiceLines().Where(l => l.Date >= start && l.Date < end.AddDays(1)).ToList();
        string Quarter(DateTime d) => $"Q{(d.Month - 1) / 3 + 1}/{d.Year}";
        string QuarterKey(DateTime d) => $"{d.Year}-{(d.Month - 1) / 3 + 1}";
        string Employee(InvoiceRow i) => i.Employee.Trim().Length == 0 ? "Đơn đặt online (chưa có nhân viên)" : i.Employee;

        var tables = new List<StatTable>();
        bool Want(string k) => kind == "all" || kind == k;

        if (Want("range"))
            tables.Add(new StatTable("range", "Hóa đơn theo khoảng ngày", "Hóa đơn", [new("Tổng tiền")],
                inv.Select(i => new StatRow($"{i.Number} · {i.Date:dd/MM/yyyy HH:mm} · {i.Customer}", [i.Total])).ToList(), [inv.Sum(i => i.Total)]));

        if (Want("period"))
        {
            (string Sort, string Label) Period(DateTime d) => group switch
            {
                "year" => (d.Year.ToString(), d.Year.ToString()),
                "quarter" => (QuarterKey(d), Quarter(d)),
                _ => (d.ToString("yyyy-MM"), d.ToString("MM/yyyy")),
            };
            var rows = inv.GroupBy(i => Period(i.Date)).OrderBy(g => g.Key.Sort)
                .Select(g => new StatRow(g.Key.Label, [g.Count(), g.Sum(i => i.Total), g.Average(i => i.Total)])).ToList();
            string unit = group switch { "year" => "năm", "quarter" => "quý", _ => "tháng" };
            tables.Add(new StatTable("period", $"Hóa đơn theo {unit}", char.ToUpper(unit[0]) + unit[1..],
                [new("Số hóa đơn", false), new("Tổng tiền"), new("Trung bình")], rows,
                [inv.Count, inv.Sum(i => i.Total), inv.Count == 0 ? 0 : inv.Average(i => i.Total)]));
        }

        if (Want("employee"))
            tables.Add(Pivot("employee", "Hóa đơn theo nhân viên và năm", "Nhân viên", inv, Employee, i => (i.Date.Year.ToString(), i.Date.Year.ToString()), i => i.Total));

        if (Want("customer"))
            tables.Add(group == "year"
                ? Pivot("customer", "Hóa đơn theo khách hàng và năm", "Khách hàng", inv, i => i.Customer, i => (i.Date.Year.ToString(), i.Date.Year.ToString()), i => i.Total)
                : Pivot("customer", "Hóa đơn theo khách hàng và quý", "Khách hàng", inv, i => i.Customer, i => (QuarterKey(i.Date), Quarter(i.Date)), i => i.Total));

        if (Want("product"))
            tables.Add(Pivot("product", "Doanh thu theo sản phẩm và quý", "Sản phẩm", lines, l => l.Product, l => (QuarterKey(l.Date), Quarter(l.Date)), l => l.Amount));

        return new StatsVm { From = start, To = end, Group = group, Kind = kind, Invoices = inv.Count, Total = inv.Sum(i => i.Total), Tables = tables };
    }

    // Bảng chéo: mỗi dòng một đối tượng (nhân viên, khách hàng, sản phẩm), mỗi cột một năm / quý (sắp theo thời gian), thêm cột Tổng khi có từ hai cột; dòng nhiều tiền nhất lên đầu.
    static StatTable Pivot<T>(string key, string title, string first, IEnumerable<T> source, Func<T, string> row, Func<T, (string Sort, string Label)> col, Func<T, decimal> value)
    {
        var list = source.ToList();
        var cols = list.Select(col).Distinct().OrderBy(c => c.Sort).ToList();
        var rows = list.GroupBy(row)
            .Select(g => new StatRow(g.Key, cols.Select(c => g.Where(x => col(x) == c).Sum(value)).ToArray()))
            .OrderByDescending(r => r.Values.Sum()).ThenBy(r => r.Label).ToList();
        var head = cols.Select(c => new StatCol(c.Label)).ToList();
        if (cols.Count > 1)
        {
            head.Add(new StatCol("Tổng"));
            rows = rows.Select(r => new StatRow(r.Label, [.. r.Values, r.Values.Sum()])).ToList();
        }
        var total = Enumerable.Range(0, head.Count).Select(i => rows.Sum(r => r.Values[i])).ToArray();
        return new StatTable(key, title, first, head, rows, total);
    }
}
