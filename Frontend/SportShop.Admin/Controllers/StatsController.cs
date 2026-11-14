using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

// Một cột của bảng thống kê; Money cho biết cột là số tiền.
public record StatCol(string Name, bool Money = true);

// Một dòng của bảng thống kê.
public record StatRow(string Label, decimal[] Values);

// Total rỗng nghĩa là bảng không có dòng Tổng cộng ở cuối (bảng thu - chi - lợi nhuận đã có cột Tổng cộng bên phải).
public record StatTable(string Key, string Title, string First, List<StatCol> Cols, List<StatRow> Rows, decimal[] Total);

// Dữ liệu trang Thống kê doanh thu, dùng chung cho xuất Excel và PDF.
public class StatsVm
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public string Group { get; init; } = "month";
    public string Kind { get; init; } = "all";
    public bool DatesChosen { get; init; }
    public int Invoices { get; init; }
    public decimal Total { get; init; }
    public List<StatTable> Tables { get; init; } = [];
}

// Trang Thống kê doanh thu và xuất Excel, PDF từ ShopData.
public class StatsController(ShopData db) : Controller
{
    // Các loại báo cáo hiện ở thanh tab.
    static readonly (string Key, string Text)[] Kinds =
    [
        ("all", "Báo cáo tổng hợp"), ("range", "Theo khoảng ngày"), ("period", "Theo tháng / quý / năm"),
        ("employee", "Theo nhân viên và năm"), ("customer", "Theo khách hàng"), ("product", "Theo sản phẩm và quý"),
    ];

    public static IReadOnlyList<(string Key, string Text)> KindList => Kinds;

    // Thống kê tổng tiền hóa đơn (HOA_DON.tong_thanh_toan, tính theo ngày xuất hóa đơn) trong [from, to]: năm bảng theo khoảng ngày, kỳ, nhân viên × năm, khách hàng × tháng/quý/năm, sản phẩm × quý.
    public IActionResult Index(DateTime? from, DateTime? to, string? kind, string? group)
    {
        ViewBag.Menu = "stats";
        return View(Build(from, to, kind, group));
    }

    // Xuất các bảng đang xem (hoặc cả 5 bảng của báo cáo tổng hợp) ra file Excel, mỗi bảng một sheet.
    public IActionResult Export(DateTime? from, DateTime? to, string? kind, string? group)
    {
        var vm = Build(from, to, kind, group);
        return File(ExcelBytes(vm), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"thong-ke-{vm.Kind}-{DateTime.Today:yyyyMMdd}.xlsx");
    }

    // Dựng file Excel (mỗi bảng một sheet) từ dữ liệu thống kê; dùng chung cho nút Xuất Excel và việc tạo báo cáo ở trang Báo cáo thống kê (ReportsController.Create).
    internal static byte[] ExcelBytes(StatsVm vm)
    {
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
        return ms.ToArray();
    }

    // Tải file PDF các bảng đang xem: lấy dữ liệu qua Build() rồi dựng file bằng StatsPdf.Build() (Models/StatsPdf.cs).
    public IActionResult Pdf(DateTime? from, DateTime? to, string? kind, string? group)
    {
        var vm = Build(from, to, kind, group);
        return File(StatsPdf.Build(vm, Kinds.First(k => k.Key == vm.Kind).Text), "application/pdf", $"thong-ke-{vm.Kind}-{DateTime.Now:yyyyMMdd}.pdf");
    }

    // Ngày bắt đầu mặc định khi không chọn ngày: lùi về đủ 3 kỳ (3 tháng, 3 quý hoặc 3 năm) tính đến ngày kết thúc. Gọi từ Build().
    static DateTime DefaultStart(DateTime end, string group)
    {
        var month = new DateTime(end.Year, end.Month, 1);
        return group switch
        {
            "year" => new DateTime(end.Year - 2, 1, 1),
            "quarter" => month.AddMonths(-(end.Month - 1) % 3 - 6),
            _ => month.AddMonths(-2),
        };
    }

    // Dựng dữ liệu trang thống kê từ ShopData.InvoiceRows() và các bảng đang chọn trong khoảng ngày đã chọn (không chọn thì lấy 3 kỳ gần nhất qua DefaultStart()); dùng chung cho Index, xuất Excel và tải PDF.
    internal StatsVm Build(DateTime? from, DateTime? to, string? kind, string? group)
    {
        var all = db.InvoiceRows();
        kind = Kinds.Any(k => k.Key == kind) ? kind! : "all";
        group = group is "quarter" or "year" ? group : "month";
        var end = (to ?? (all.Count == 0 ? DateTime.Today : all[^1].Date)).Date;
        var start = (from ?? DefaultStart(end, group)).Date;
        if (start > end) (start, end) = (end, start);

        var stop = end.AddDays(1);
        var inv = all.Where(i => i.Date >= start && i.Date < stop).ToList();
        // Tên nhân viên của hóa đơn, đơn đặt online thì ghi nhãn riêng.
        string Employee(InvoiceRow i) => i.Employee.Trim().Length == 0 ? "Đơn đặt online (chưa có nhân viên)" : i.Employee;

        // Cột của các bảng là đúng những tháng / quý / năm nằm trong khoảng ngày (kỳ không có dữ liệu ghi 0đ); khoảng qua nhiều năm thì nhãn kèm năm.
        bool oneYear = start.Year == end.Year;
        // Mã năm của một ngày, khớp với Sort của danh sách years.
        string YearOf(DateTime d) => d.Year.ToString();
        // Mã quý của một ngày, khớp với Sort của danh sách quarters.
        string QuarterOf(DateTime d) => $"{d.Year}-{(d.Month - 1) / 3 + 1}";
        // Mã tháng của một ngày, khớp với Sort của danh sách months.
        string MonthOf(DateTime d) => $"{d.Year}-{d.Month}";
        var years = Enumerable.Range(start.Year, end.Year - start.Year + 1).Select(y => (Sort: y.ToString(), Label: y.ToString())).ToList();
        int monthCount = (end.Year - start.Year) * 12 + end.Month - start.Month + 1;
        var monthDays = Enumerable.Range(0, monthCount).Select(n => new DateTime(start.Year, start.Month, 1).AddMonths(n)).ToList();
        var months = monthDays.Select(m => (Sort: MonthOf(m), Label: oneYear ? $"Tháng {m.Month}" : $"Tháng {m.Month}/{m.Year}")).ToList();
        var quarters = monthDays.GroupBy(QuarterOf).Select(q => (Sort: q.Key, Label: oneYear ? $"Quý {(q.First().Month - 1) / 3 + 1}" : $"Quý {(q.First().Month - 1) / 3 + 1}/{q.First().Year}")).ToList();
        var slots = group switch { "year" => years, "quarter" => quarters, _ => months };
        // Mã kỳ của một ngày theo kiểu nhóm đang chọn.
        string SlotOf(DateTime d) => group switch { "year" => YearOf(d), "quarter" => QuarterOf(d), _ => MonthOf(d) };
        string unit = group switch { "year" => "năm", "quarter" => "quý", _ => "tháng" };

        var tables = new List<StatTable>();
        // Kiểm tra bảng k có cần dựng theo loại báo cáo đang chọn không.
        bool Want(string k) => kind == "all" || kind == k;

        if (Want("range"))
            tables.Add(new StatTable("range", "Hóa đơn theo khoảng ngày", "Hóa đơn", [new("Tổng tiền")],
                inv.Select(i => new StatRow($"{i.Number} · {i.Date:dd/MM/yyyy HH:mm} · {i.Customer}", [i.Total])).ToList(), [inv.Sum(i => i.Total)]));

        if (Want("period"))
        {
            // Mẫu của thầy: cột là các kỳ trong khoảng ngày và cột Tổng cộng, dòng là Tổng thu (hóa đơn), Tổng chi (phiếu nhập hàng), Lợi nhuận (thu − chi).
            decimal[] BySlot(IEnumerable<(DateTime Date, decimal Amount)> src)
            {
                var list = src.Where(x => x.Date >= start && x.Date < stop).ToList();
                return slots.Select(s => list.Where(x => SlotOf(x.Date) == s.Sort).Sum(x => x.Amount)).ToArray();
            }
            var thu = BySlot(all.Select(i => (i.Date, i.Total)));
            var chi = BySlot(db.ImportRows().Select(p => (p.Date, p.Total)));
            var loiNhuan = thu.Zip(chi, (a, b) => a - b).ToArray();
            StatRow Line(string label, decimal[] v) => new(label, [.. v, v.Sum()]);
            tables.Add(new StatTable("period", $"Thu, chi và lợi nhuận theo {unit}", "Chỉ tiêu",
                [.. slots.Select(s => new StatCol(s.Label)), new StatCol("Tổng cộng")],
                [Line("Tổng thu (hóa đơn)", thu), Line("Tổng chi (phiếu nhập hàng)", chi), Line("Lợi nhuận (thu − chi)", loiNhuan)], []));
        }

        if (Want("employee"))
            tables.Add(Pivot("employee", "Hóa đơn theo nhân viên và năm", "Nhân viên", inv, Employee, i => YearOf(i.Date), years, i => i.Total));

        if (Want("customer"))
            tables.Add(Pivot("customer", $"Hóa đơn theo khách hàng và {unit}", "Khách hàng", inv, i => i.Customer, i => SlotOf(i.Date), slots, i => i.Total));

        if (Want("product"))
            tables.Add(Pivot("product", "Doanh thu theo sản phẩm và quý", "Sản phẩm",
                db.InvoiceLines().Where(l => l.Date >= start && l.Date < stop), l => l.Product, l => QuarterOf(l.Date), quarters, l => l.Amount));

        return new StatsVm { From = start, To = end, Group = group, Kind = kind, DatesChosen = from != null || to != null, Invoices = inv.Count, Total = inv.Sum(i => i.Total), Tables = tables };
    }

    // Bảng chéo theo mẫu: mỗi dòng một đối tượng (nhân viên, khách hàng, sản phẩm), cột là các kỳ trong khoảng ngày đã chọn (kỳ không có dữ liệu ghi 0đ), thêm cột Tổng cộng bên phải và dòng Tổng cộng ở cuối; dòng nhiều tiền nhất lên đầu.
    static StatTable Pivot<T>(string key, string title, string first, IEnumerable<T> source, Func<T, string> row, Func<T, string> col,
        List<(string Sort, string Label)> cols, Func<T, decimal> value)
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
        return new StatTable(key, title, first, head, rows, total);
    }
}
