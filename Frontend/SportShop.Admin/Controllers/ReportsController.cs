using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

// Mức tăng giảm so với kỳ trước, hiện trên thẻ số liệu.
public record Delta(string Text, bool Up);

// Một cột của biểu đồ sản phẩm bán chạy.
public record BarCol(string Label, int Qty, int Percent, bool Highlight);

// Một phần của biểu đồ tròn nhóm tuổi.
public record DonutPart(string Label, int Count, decimal Percent, string Color);

// Một dòng hiệu suất nhân viên.
public record StaffRow(string Initials, string Name, string Detail, int Score);

// Dữ liệu trang Báo cáo thống kê (Views/Reports/Index.cshtml).
public class ReportsVm
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }

    public decimal Revenue { get; init; }
    public Delta RevenueDelta { get; init; } = new("", true);
    public int Orders { get; init; }
    public Delta OrdersDelta { get; init; } = new("", true);
    public decimal Average { get; init; }
    public Delta AverageDelta { get; init; } = new("", true);
    public decimal Profit { get; init; }

    public string LinePath { get; init; } = "";
    public string AreaPath { get; init; } = "";
    public List<(double X, double Y, string Tip)> Dots { get; init; } = [];
    public List<(double X, string Label)> XLabels { get; init; } = [];
    public List<(double Y, string Label)> YTicks { get; init; } = [];

    public List<BarCol> Bars { get; init; } = [];
    public List<DonutPart> Donut { get; init; } = [];
    public int CustomerTotal { get; init; }
    public List<StaffRow> Staff { get; init; } = [];

    public List<Row> ProfitByProduct { get; init; } = [];
    public List<Row> HighStock { get; init; } = [];
    public List<Row> Promos { get; init; } = [];
    public List<Row> Interests { get; init; } = [];

    // Khung "Các báo cáo đã tạo và tải xuống" (lịch sử báo cáo từ ReportStore).
    public TableSection History { get; init; } = new();
}

// Dựng trang Báo cáo thống kê từ ShopData.
public class ReportsController(ShopData db, ReportStore reports) : Controller
{
    // Nhãn loại báo cáo theo kiểu thống kê (khóa của StatsController.KindList).
    public static readonly Dictionary<string, string> TypeLabels = new()
    {
        ["all"] = "Báo cáo tổng hợp",
        ["range"] = "Báo cáo doanh thu theo khoảng ngày",
        ["period"] = "Báo cáo doanh thu theo tháng / quý / năm",
        ["employee"] = "Báo cáo doanh thu theo nhân viên",
        ["customer"] = "Báo cáo doanh thu theo khách hàng",
        ["product"] = "Báo cáo doanh thu theo sản phẩm",
    };

    // Bộ lọc "Tìm kiếm nâng cao" của khung báo cáo đã tạo: loại (cột 1), người lập (cột 3), trạng thái (cột 5) và ngày tạo (data-f).
    static FilterSpec HistoryFilter() => new()
    {
        Id = "reports", Noun = "kết quả",
        Fields =
        [
            new("type", "Loại báo cáo", "select", Col: 1),
            new("status", "Trạng thái", "select", Col: 5, Options: ["Đã tạo", "Đã tải xuống"]),
            new("by", "Người lập", "select", Col: 3),
            new("date", "Ngày tạo", "date", Data: "date"),
        ],
        Quick = [new("Báo cáo doanh thu", "type", "contains", "doanh thu"), new("Tạo trong tháng này", "date", "gte", new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToString("yyyy-MM-dd"))],
    };

    // Khung lịch sử báo cáo: mỗi báo cáo đã tạo một dòng, cột cuối là liên kết tải xuống.
    TableSection BuildHistory() => new()
    {
        Heading = "Các báo cáo đã tạo và tải xuống",
        Columns = ["Tên báo cáo", "Loại báo cáo", "Kỳ báo cáo", "Người lập", "Ngày tạo", "Trạng thái", "Tải xuống"],
        Rows = reports.All().Select(r => new Row(
            [r.Name, r.TypeLabel, $"{r.From:dd/MM/yyyy} – {r.To:dd/MM/yyyy}", r.CreatedBy, Fmt.DateTimeDot(r.CreatedAt),
             r.Downloads > 0 ? "[g]Đã tải xuống" : "[b]Đã tạo", $"[a]{Url.Action(nameof(Download), new { id = r.Id })}|Tải {r.Format}"],
            null, 0, null, new() { ["date"] = r.CreatedAt.ToString("yyyy-MM-dd") })).ToList(),
        Filter = HistoryFilter(),
        AddLabel = "Tạo báo cáo", AddId = "newReport", ShowActions = false,
    };

    // Tạo một báo cáo thống kê (Excel hoặc PDF) cho kiểu và khoảng ngày đã chọn, lưu vào ReportStore; trả JSON {ok, message}.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(DateTime? from, DateTime? to, string? kind, string? group, string? format)
    {
        kind = TypeLabels.ContainsKey(kind ?? "") ? kind! : "all";
        format = format == "PDF" ? "PDF" : "Excel";
        try
        {
            var vm = new StatsController(db).Build(from, to, kind, group);
            byte[] bytes = format == "PDF"
                ? StatsPdf.Build(vm, StatsController.KindList.First(k => k.Key == vm.Kind).Text)
                : StatsController.ExcelBytes(vm);
            string name = $"{TypeLabels[kind]} {vm.From:dd/MM/yyyy} – {vm.To:dd/MM/yyyy}";
            reports.Add(name, kind, TypeLabels[kind], format, bytes, vm.From, vm.To, User.Identity?.Name ?? "");
            return Json(new Result(true, $"Đã tạo báo cáo \"{name}\"."));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Json(new Result(false, "Không lưu được file báo cáo, vui lòng thử lại."));
        }
    }

    // Tải một báo cáo đã tạo (theo mã trong lịch sử); mỗi lượt tải tăng bộ đếm để trạng thái chuyển thành "Đã tải xuống".
    public IActionResult Download(string id)
    {
        var opened = reports.Open(id);
        if (opened is null) return NotFound();
        var (entry, path) = opened.Value;
        string ext = entry.Format == "PDF" ? ".pdf" : ".xlsx";
        string mime = entry.Format == "PDF" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        string safe = string.Concat(entry.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
        return PhysicalFile(path, mime, safe + ext);
    }

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    // Định dạng số cho tọa độ biểu đồ SVG.
    static string F(double v) => v.ToString("0.##", Inv);

    // Báo cáo thống kê from–to (mặc định 30 ngày): nửa tải mới từ MySQL (thẻ số liệu + biểu đồ qua ShopData.ReportOrders(), bán chạy + lợi nhuận qua ShopData.ReportProducts(), khuyến mãi qua ShopData.PromoStats()), nửa dùng lại dữ liệu trang khác đã nạp qua ShopData.Reused... / DataCache (nhóm tuổi, sở thích, hiệu suất nhân viên, tồn kho nhiều).
    public IActionResult Index(DateTime? from, DateTime? to)
    {
        ViewBag.Menu = "reports";

        var end = (to ?? DefaultEndDate()).Date;
        var start = (from ?? end.AddDays(-29)).Date;
        if (start > end) (start, end) = (end, start);
        int days = (end - start).Days + 1;
        var prevStart = start.AddDays(-days);

        var orders = db.ReportOrders(prevStart, end);
        var cur = orders.Where(o => o.Date >= start).ToList();
        var prev = orders.Where(o => o.Date < start).ToList();
        decimal revenue = cur.Sum(o => o.Revenue), prevRevenue = prev.Sum(o => o.Revenue);
        decimal avg = cur.Count == 0 ? 0 : revenue / cur.Count;
        decimal prevAvg = prev.Count == 0 ? 0 : prevRevenue / prev.Count;

        int n = days <= 84 ? Math.Max(2, (int)Math.Ceiling(days / 7.0)) : 12;
        int bucketLen = days <= 84 ? 7 : (int)Math.Ceiling(days / 12.0);
        var buckets = new double[n];
        foreach (var o in cur)
            buckets[Math.Min(n - 1, (o.Date.Date - start).Days / bucketLen)] += (double)o.Revenue / 1_000_000;
        double max = NiceMax(buckets.Max());
        const double W = 760, H = 240, padL = 44, padR = 16, padT = 16, padB = 32;
        double plotW = W - padL - padR, plotH = H - padT - padB;
        var pts = buckets.Select((v, i) => (X: padL + i * plotW / (n - 1), Y: padT + plotH * (1 - v / max))).ToList();
        string unit = bucketLen == 7 ? "Tuần" : "Kỳ";

        var sold = db.ReportProducts(start, end);
        var top = sold.OrderByDescending(x => x.Qty).Take(7).ToList();
        int maxQty = top.Count == 0 ? 1 : top[0].Qty;
        var bars = top.Select(x => new BarCol(x.Name, x.Qty, Math.Max(6, x.Qty * 100 / maxQty), x.Qty == maxQty)).ToList();
        var profit = sold.Select(x => new Row([x.Name, x.Qty.ToString(), Fmt.Money(x.Revenue), Fmt.Money(x.Cost), Fmt.Money(x.Revenue - x.Cost)])).ToList();

        var promos = db.PromoStats(start, end)
            .Select(p => new Row([p.Name, p.Uses.ToString(), Fmt.Money(p.Discount), Fmt.Money(p.Revenue)])).ToList();

        var cust = db.ReusedCustomers.Where(c => !c.Deleted).ToList();
        int g1 = cust.Count(c => c.Age <= 25), g2 = cust.Count(c => c.Age is >= 26 and <= 35), g3 = cust.Count(c => c.Age > 35);
        // Tính phần trăm trên tổng số khách.
        decimal P(int x) => cust.Count == 0 ? 0 : Math.Round(x * 100m / cust.Count, 0);
        var donut = new List<DonutPart>
        {
            new("18–25 tuổi", g1, P(g1), "#0f766e"),
            new("26–35 tuổi", g2, P(g2), "#f97316"),
            new("Trên 35 tuổi", g3, P(g3), "#d6ebe7"),
        };

        var interests = cust.SelectMany(c => c.Interests.Select(i => (Name: i, c.Age)))
            .GroupBy(x => x.Name).OrderByDescending(g => g.Count())
            .Select(g => new Row([g.Key, g.Count().ToString(), g.Any(x => x.Age.HasValue) ? g.Where(x => x.Age.HasValue).Average(x => x.Age!.Value).ToString("0.#") + " tuổi" : "-"])).ToList();

        var invoices = db.ReusedOrders.Where(o => o.Invoiced && o.EmployeeId != null).CountBy(o => o.EmployeeId!.Value).ToDictionary();
        var cares = db.ReusedCareNotes.CountBy(c => c.EmployeeId).ToDictionary();
        var replies = db.ReusedFeedback.Where(f => f.ReplyEmployeeId != null).CountBy(f => f.ReplyEmployeeId!.Value).ToDictionary();
        var staffRaw = db.ReusedEmployees.Where(e => e.Working).Select(e =>
        {
            int inv = invoices.GetValueOrDefault(e.Id), care = cares.GetValueOrDefault(e.Id), rep = replies.GetValueOrDefault(e.Id);
            return (E: e, Inv: inv, Care: care, Rep: rep, Total: inv + care + rep);
        }).OrderByDescending(x => x.Total).Take(5).ToList();
        int topTotal = Math.Max(1, staffRaw.Select(x => x.Total).DefaultIfEmpty(0).Max());
        var staff = staffRaw.Select(x => new StaffRow(
            Initials(x.E.FirstName, x.E.LastName), $"{x.E.LastName} {x.E.FirstName}",
            $"{x.Inv} hóa đơn · {x.Care} lần chăm sóc · {x.Rep} phản hồi", x.Total * 100 / topTotal)).ToList();

        var products = db.ReusedProducts.Where(p => p.Category != Catalog.ArchivedCategory).ToDictionary(p => p.Id);
        var high = db.ReusedVariants.Where(v => products.ContainsKey(v.ProductId)).OrderByDescending(v => v.Stock).Take(5)
            .Select(v => new Row([ShopData.VariantName(products[v.ProductId], v), v.Stock.ToString()])).ToList();

        return View(new ReportsVm
        {
            From = start, To = end,
            Revenue = revenue, RevenueDelta = Change(revenue, prevRevenue),
            Orders = cur.Count, OrdersDelta = Change(cur.Count, prev.Count),
            Average = avg, AverageDelta = Change(avg, prevAvg),
            Profit = cur.Sum(o => o.Revenue - o.Cost),
            LinePath = SmoothPath(pts),
            AreaPath = SmoothPath(pts) + $" L{F(pts[^1].X)},{F(H - padB)} L{F(pts[0].X)},{F(H - padB)} Z",
            Dots = pts.Select((p, i) => (p.X, p.Y, $"{unit} {i + 1}: {buckets[i]:0.#} triệu")).ToList(),
            XLabels = pts.Select((p, i) => (p.X, $"{unit} {i + 1}")).ToList(),
            YTicks = Enumerable.Range(0, 5).Select(i => (padT + plotH * (1 - i / 4.0), (max * i / 4).ToString("0.##", Inv))).ToList(),
            Bars = bars, Donut = donut, CustomerTotal = cust.Count, Staff = staff,
            ProfitByProduct = profit, HighStock = high, Promos = promos, Interests = interests,
            History = BuildHistory(),
        });
    }

    // Làm tròn giá trị lớn nhất lên mốc đẹp cho trục biểu đồ.
    static double NiceMax(double v)
    {
        if (v <= 0) return 4;
        double pow = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var m in new[] { 1.0, 2, 4, 5, 8, 10 })
            if (v <= m * pow) return m * pow;
        return 10 * pow;
    }

    // Dựng đường cong SVG đi qua các điểm của biểu đồ.
    static string SmoothPath(List<(double X, double Y)> p)
    {
        var sb = new System.Text.StringBuilder($"M{F(p[0].X)},{F(p[0].Y)}");
        for (int i = 0; i < p.Count - 1; i++)
        {
            var p0 = p[Math.Max(0, i - 1)]; var p1 = p[i]; var p2 = p[i + 1]; var p3 = p[Math.Min(p.Count - 1, i + 2)];
            sb.Append($" C{F(p1.X + (p2.X - p0.X) / 6)},{F(p1.Y + (p2.Y - p0.Y) / 6)} {F(p2.X - (p3.X - p1.X) / 6)},{F(p2.Y - (p3.Y - p1.Y) / 6)} {F(p2.X)},{F(p2.Y)}");
        }
        return sb.ToString();
    }

    // Ngày kết thúc mặc định: hôm nay, trừ khi 30 ngày gần đây chưa có hóa đơn nào thì lấy ngày hóa đơn gần nhất (một truy vấn MAX/SUM thay vì nạp cả bảng đơn hàng).
    DateTime DefaultEndDate()
    {
        var today = DateTime.Today;
        var (last, recent) = db.InvoiceDates(today.AddDays(-29), today);
        return last == null || recent > 0 ? today : last.Value;
    }

    // Tính phần trăm thay đổi so với kỳ trước cho thẻ số liệu.
    static Delta Change(decimal cur, decimal prev)
    {
        if (prev == 0) return new("Chưa có dữ liệu kỳ trước", true);
        var pct = (cur - prev) / prev * 100;
        return new($"{(pct >= 0 ? "+" : "")}{Fmt.Pct(Math.Round(pct, 1))} so với kỳ trước", pct >= 0);
    }

    // Lấy chữ cái đầu của tên và họ.
    static string Initials(string first, string last) =>
        (first.Length > 0 ? first[..1] : "") + (last.Length > 0 ? last[..1] : "");
}
