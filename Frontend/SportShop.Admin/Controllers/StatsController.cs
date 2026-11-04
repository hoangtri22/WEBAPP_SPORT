using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public record StatCol(string Name, bool Money = true);

public record StatRow(string Label, decimal[] Values);

// Total rỗng nghĩa là bảng không có dòng Tổng cộng ở cuối (bảng thu - chi - lợi nhuận đã có cột Tổng cộng bên phải).
public record StatTable(string Key, string Title, string First, List<StatCol> Cols, List<StatRow> Rows, decimal[] Total, string? Note = null);

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
            if (t.Total.Length > 0)
            {
                r++;
                ws.Cell(r, 1).Value = "Tổng cộng";
                for (int i = 0; i < nc; i++) ws.Cell(r, i + 2).Value = t.Total[i];
                ws.Range(r, 1, r, nc + 1).Style.Font.SetBold();
            }
            if (r > hr)
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

        var stop = end.AddDays(1);
        var inv = all.Where(i => i.Date >= start && i.Date < stop).ToList();
        string Employee(InvoiceRow i) => i.Employee.Trim().Length == 0 ? "Đơn đặt online (chưa có nhân viên)" : i.Employee;

        // Các bảng theo quý / tháng hiển thị đủ các kỳ của năm chứa ngày kết thúc (kỳ không có dữ liệu ghi 0đ); các bảng theo năm hiển thị 3 năm gần nhất tính đến năm đó.
        int year = end.Year;
        var yearStart = new DateTime(year, 1, 1);
        var inYearFrom = start > yearStart ? start : yearStart;
        var threeYearsFrom = new DateTime(year - 2, 1, 1);
        var invYear = all.Where(i => i.Date >= inYearFrom && i.Date < stop).ToList();
        var invThreeYears = all.Where(i => i.Date >= threeYearsFrom && i.Date < stop).ToList();
        var quarters = Enumerable.Range(1, 4).Select(q => (Sort: q.ToString(), Label: $"Quý {q}")).ToList();
        var years = Enumerable.Range(year - 2, 3).Select(y => (Sort: y.ToString(), Label: y.ToString())).ToList();
        string QuarterOf(DateTime d) => ((d.Month - 1) / 3 + 1).ToString();
        string YearOf(DateTime d) => d.Year.ToString();
        string yearNote = $"Các quý của năm {year} (năm của ngày kết thúc), chỉ tính hóa đơn từ ngày bắt đầu đã chọn; quý chưa có dữ liệu ghi 0đ.";
        string threeYearNote = $"3 năm gần nhất tính đến năm {year} ({year - 2} – {year}), tính đến ngày kết thúc đã chọn; năm chưa có dữ liệu ghi 0đ.";

        var tables = new List<StatTable>();
        bool Want(string k) => kind == "all" || kind == k;

        if (Want("range"))
            tables.Add(new StatTable("range", "Hóa đơn theo khoảng ngày", "Hóa đơn", [new("Tổng tiền")],
                inv.Select(i => new StatRow($"{i.Number} · {i.Date:dd/MM/yyyy HH:mm} · {i.Customer}", [i.Total])).ToList(), [inv.Sum(i => i.Total)]));

        if (Want("period"))
        {
            // Mẫu của thầy: cột là các kỳ (Quý 1..Quý 4 hoặc Tháng 1..Tháng 12 của năm, hoặc 3 năm gần nhất) và cột Tổng cộng, dòng là Tổng thu (hóa đơn), Tổng chi (phiếu nhập hàng), Lợi nhuận (thu - chi)
            var slots = group switch
            {
                "year" => years,
                "quarter" => quarters,
                _ => Enumerable.Range(1, 12).Select(m => (Sort: m.ToString(), Label: $"Tháng {m}")).ToList(),
            };
            string SlotOf(DateTime d) => group switch { "year" => YearOf(d), "quarter" => QuarterOf(d), _ => d.Month.ToString() };
            var from0 = group == "year" ? threeYearsFrom : inYearFrom;
            decimal[] BySlot(IEnumerable<(DateTime Date, decimal Amount)> src)
            {
                var list = src.Where(x => x.Date >= from0 && x.Date < stop).ToList();
                return slots.Select(s => list.Where(x => SlotOf(x.Date) == s.Sort).Sum(x => x.Amount)).ToArray();
            }
            var thu = BySlot(all.Select(i => (i.Date, i.Total)));
            var chi = BySlot(db.ImportRows().Select(p => (p.Date, p.Total)));
            var loiNhuan = thu.Zip(chi, (a, b) => a - b).ToArray();
            StatRow Line(string label, decimal[] v) => new(label, [.. v, v.Sum()]);
            string unit = group switch { "year" => "năm", "quarter" => "quý", _ => "tháng" };
            tables.Add(new StatTable("period", $"Thu, chi và lợi nhuận theo {unit}" + (group == "year" ? "" : $" (năm {year})"),
                group == "year" ? "Chỉ tiêu" : year.ToString(),
                [.. slots.Select(s => new StatCol(s.Label)), new StatCol("Tổng cộng")],
                [Line("Tổng thu (hóa đơn)", thu), Line("Tổng chi (phiếu nhập hàng)", chi), Line("Lợi nhuận (thu − chi)", loiNhuan)], [],
                group == "year" ? threeYearNote : group == "month" ? yearNote.Replace("Các quý", "Các tháng").Replace("quý chưa", "tháng chưa") : yearNote));
        }

        if (Want("employee"))
            tables.Add(Pivot("employee", "Hóa đơn theo nhân viên và năm", "Nhân viên", invThreeYears, Employee, i => YearOf(i.Date), years, i => i.Total, threeYearNote));

        if (Want("customer"))
            tables.Add(group == "year"
                ? Pivot("customer", "Hóa đơn theo khách hàng và năm", "Khách hàng", invThreeYears, i => i.Customer, i => YearOf(i.Date), years, i => i.Total, threeYearNote)
                : Pivot("customer", $"Hóa đơn theo khách hàng và quý (năm {year})", year.ToString() + " · Khách hàng", invYear, i => i.Customer, i => QuarterOf(i.Date), quarters, i => i.Total, yearNote));

        if (Want("product"))
            tables.Add(Pivot("product", $"Doanh thu theo sản phẩm và quý (năm {year})", year.ToString() + " · Sản phẩm",
                db.InvoiceLines().Where(l => l.Date >= inYearFrom && l.Date < stop), l => l.Product, l => QuarterOf(l.Date), quarters, l => l.Amount,
                yearNote + " Tính theo thành tiền từng dòng sản phẩm, chưa trừ giảm giá toàn đơn và chưa cộng phí vận chuyển nên tổng có thể khác tổng tiền hóa đơn."));

        return new StatsVm { From = start, To = end, Group = group, Kind = kind, Invoices = inv.Count, Total = inv.Sum(i => i.Total), Tables = tables };
    }

    // Bảng chéo theo mẫu: mỗi dòng một đối tượng (nhân viên, khách hàng, sản phẩm), cột là đủ các năm / quý đã định sẵn (kỳ không có dữ liệu ghi 0đ), thêm cột Tổng cộng bên phải và dòng Tổng cộng ở cuối; dòng nhiều tiền nhất lên đầu.
    static StatTable Pivot<T>(string key, string title, string first, IEnumerable<T> source, Func<T, string> row, Func<T, string> col,
        List<(string Sort, string Label)> cols, Func<T, decimal> value, string? note = null)
    {
        var rows = source.GroupBy(row)
            .Select(g =>
            {
                var v = cols.Select(c => g.Where(x => col(x) == c.Sort).Sum(value)).ToArray();
                return new StatRow(g.Key, [.. v, v.Sum()]);
            })
            .OrderByDescending(r => r.Values[^1]).ThenBy(r => r.Label).ToList();
        var head = cols.Select(c => new StatCol(c.Label)).Append(new StatCol("Tổng cộng")).ToList();
        var total = Enumerable.Range(0, head.Count).Select(i => rows.Sum(r => r.Values[i])).ToArray();
        return new StatTable(key, title, first, head, rows, total, note);
    }
}
