using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>So sánh với kỳ trước: Text hiển thị, Up = tăng (xanh) hay giảm (đỏ).</summary>
public record Delta(string Text, bool Up);

/// <summary>Một cột trong biểu đồ cột "Sản phẩm bán chạy".</summary>
public record BarCol(string Label, int Qty, int Percent, bool Highlight);

/// <summary>Một phần của biểu đồ tròn "Nhóm khách hàng".</summary>
public record DonutPart(string Label, int Count, decimal Percent, string Color);

/// <summary>Một dòng trong "Hiệu suất nhân viên".</summary>
public record StaffRow(string Initials, string Name, string Detail, int Score);

/// <summary>Dữ liệu cho trang Báo cáo thống kê.</summary>
public class ReportsVm
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }

    // 4 thẻ số liệu
    public decimal Revenue { get; init; }
    public Delta RevenueDelta { get; init; } = new("", true);
    public int Orders { get; init; }
    public Delta OrdersDelta { get; init; } = new("", true);
    public decimal Average { get; init; }
    public Delta AverageDelta { get; init; } = new("", true);
    public decimal Profit { get; init; }

    // Biểu đồ vùng "Doanh thu theo tuần" (đã tính sẵn toạ độ SVG)
    public string LinePath { get; init; } = "";
    public string AreaPath { get; init; } = "";
    public List<(double X, double Y, string Tip)> Dots { get; init; } = [];
    public List<(double X, string Label)> XLabels { get; init; } = [];
    public List<(double Y, string Label)> YTicks { get; init; } = [];

    public List<BarCol> Bars { get; init; } = [];
    public List<DonutPart> Donut { get; init; } = [];
    public int CustomerTotal { get; init; }
    public List<StaffRow> Staff { get; init; } = [];

    // Bảng chi tiết bổ sung theo PDF
    public List<Row> ProfitByProduct { get; init; } = [];
    public List<Row> HighStock { get; init; } = [];
    public List<Row> Promos { get; init; } = [];
}

public class ReportsController : Controller
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) => v.ToString("0.##", Inv);   // số thực → chuỗi cho SVG

    /// <summary>
    /// Báo cáo theo khoảng thời gian tùy chọn (from → to). Mặc định 30 ngày gần nhất.
    /// Doanh thu / lợi nhuận chỉ tính từ hóa đơn đã hoàn thành (PDF mục 3.1.9).
    /// </summary>
    public IActionResult Index(DateTime? from, DateTime? to)
    {
        ViewBag.Menu = "reports";

        // ----- Xác định khoảng thời gian và kỳ trước có cùng độ dài -----
        var end = (to ?? DateTime.Today).Date;
        var start = (from ?? end.AddDays(-29)).Date;
        if (start > end) (start, end) = (end, start);
        int days = (end - start).Days + 1;
        var prevEnd = start.AddDays(-1);
        var prevStart = prevEnd.AddDays(-(days - 1));

        // Hóa đơn hoàn thành trong một khoảng ngày
        List<Order> Done(DateTime a, DateTime b) => SampleData.Orders
            .Where(o => o.Status == "hoan_thanh" && o.Date.Date >= a && o.Date.Date <= b).ToList();
        // Doanh thu thuần của hóa đơn = tiền hàng sau giảm giá (chưa tính phí ship)
        decimal Rev(Order o) => o.Subtotal - o.Discount;

        var cur = Done(start, end);
        var prev = Done(prevStart, prevEnd);
        decimal revenue = cur.Sum(Rev), prevRevenue = prev.Sum(Rev);
        decimal avg = cur.Count == 0 ? 0 : revenue / cur.Count;
        decimal prevAvg = prev.Count == 0 ? 0 : prevRevenue / prev.Count;

        // ----- Biểu đồ vùng: doanh thu theo tuần (đơn vị triệu đồng) -----
        // Kỳ ngắn (≤ 12 tuần): mỗi mốc đúng 1 tuần. Kỳ dài hơn: chia đều thành 12 mốc.
        int n = days <= 84 ? Math.Max(2, (int)Math.Ceiling(days / 7.0)) : 12;   // số mốc
        int bucketLen = days <= 84 ? 7 : (int)Math.Ceiling(days / 12.0);        // số ngày mỗi mốc
        var buckets = new double[n];
        foreach (var o in cur)
        {
            int idx = Math.Min(n - 1, (o.Date.Date - start).Days / bucketLen);
            buckets[idx] += (double)Rev(o) / 1_000_000;
        }
        double max = NiceMax(buckets.Max());
        const double W = 760, H = 240, padL = 44, padR = 16, padT = 16, padB = 32;
        double plotW = W - padL - padR, plotH = H - padT - padB;
        var pts = buckets.Select((v, i) => (X: padL + i * plotW / (n - 1), Y: padT + plotH * (1 - v / max))).ToList();
        string unit = bucketLen == 7 ? "Tuần" : "Kỳ";

        // ----- Biểu đồ cột: sản phẩm bán chạy theo số lượng đã bán -----
        var qty = cur.SelectMany(o => o.Lines)
            .GroupBy(l => SampleData.Variant(l.VariantId).ProductId)
            .Select(g => (Name: SampleData.Product(g.Key).Name, Qty: g.Sum(l => l.Qty)))
            .OrderByDescending(x => x.Qty).Take(7).ToList();
        int maxQty = qty.Count == 0 ? 1 : qty.Max(x => x.Qty);
        var bars = qty.Select(x => new BarCol(x.Name, x.Qty, Math.Max(6, x.Qty * 100 / maxQty), x.Qty == maxQty)).ToList();

        // ----- Biểu đồ tròn: nhóm khách hàng theo độ tuổi (tính từ ngày sinh) -----
        var cust = SampleData.Customers;
        int g1 = cust.Count(c => c.Age <= 25), g2 = cust.Count(c => c.Age is >= 26 and <= 35), g3 = cust.Count(c => c.Age > 35);
        decimal P(int x) => cust.Count == 0 ? 0 : Math.Round(x * 100m / cust.Count, 0);
        var donut = new List<DonutPart>
        {
            new("18–25 tuổi", g1, P(g1), "#0f766e"),
            new("26–35 tuổi", g2, P(g2), "#f97316"),
            new("Trên 35 tuổi", g3, P(g3), "#d6ebe7"),
        };

        // ----- Hiệu suất nhân viên: số hóa đơn lập + lần chăm sóc + phản hồi đã trả lời -----
        var staffRaw = SampleData.Employees.Where(e => e.Working).Select(e =>
        {
            int inv = SampleData.Orders.Count(o => o.EmployeeId == e.Id && o.Status == "hoan_thanh");
            int care = SampleData.CareNotes.Count(c => c.EmployeeId == e.Id);
            int rep = SampleData.FeedbackItems.Count(f => f.ReplyEmployeeId == e.Id);
            return (E: e, Inv: inv, Care: care, Rep: rep, Total: inv + care + rep);
        }).OrderByDescending(x => x.Total).Take(5).ToList();
        int topTotal = Math.Max(1, staffRaw.Max(x => x.Total));
        var staff = staffRaw.Select(x => new StaffRow(
            Initials(x.E.FirstName, x.E.LastName), $"{x.E.LastName} {x.E.FirstName}",
            $"{x.Inv} hóa đơn · {x.Care} lần chăm sóc · {x.Rep} phản hồi", x.Total * 100 / topTotal)).ToList();

        // ----- Lợi nhuận theo sản phẩm = (giá bán - giá vốn) x số lượng -----
        var profit = cur.SelectMany(o => o.Lines)
            .GroupBy(l => SampleData.Variant(l.VariantId).ProductId)
            .Select(g =>
            {
                decimal rev = g.Sum(l => SampleData.Variant(l.VariantId).Price * l.Qty);
                decimal cost = g.Sum(l => SampleData.Variant(l.VariantId).Cost * l.Qty);
                return new Row([SampleData.Product(g.Key).Name, g.Sum(l => l.Qty).ToString(), Fmt.Money(rev), Fmt.Money(cost), Fmt.Money(rev - cost)]);
            }).ToList();

        // Sản phẩm tồn kho nhiều (báo cáo "sản phẩm tồn kho" trong PDF)
        var high = SampleData.Variants.OrderByDescending(v => v.Stock).Take(5)
            .Select(v => new Row([SampleData.VariantName(v.Id), v.Stock.ToString()])).ToList();

        // Hiệu quả khuyến mãi (số liệu mẫu)
        var promos = new List<Row>
        {
            new(["Chào hè năng động", "27", Fmt.Money(2_150_000), Fmt.Money(41_800_000)]),
            new(["SPORTNEW", "12", Fmt.Money(1_200_000), Fmt.Money(18_400_000)]),
            new(["VIP10", "5", Fmt.Money(3_160_000), Fmt.Money(36_300_000)]),
        };

        return View(new ReportsVm
        {
            From = start, To = end,
            Revenue = revenue, RevenueDelta = Change(revenue, prevRevenue),
            Orders = cur.Count, OrdersDelta = Change(cur.Count, prev.Count),
            Average = avg, AverageDelta = Change(avg, prevAvg),
            Profit = cur.Sum(o => Rev(o) - o.Cost),
            LinePath = SmoothPath(pts),
            AreaPath = SmoothPath(pts) + $" L{F(pts[^1].X)},{F(H - padB)} L{F(pts[0].X)},{F(H - padB)} Z",
            Dots = pts.Select((p, i) => (p.X, p.Y, $"{unit} {i + 1}: {buckets[i]:0.#} triệu")).ToList(),
            XLabels = pts.Select((p, i) => (p.X, $"{unit} {i + 1}")).ToList(),
            YTicks = Enumerable.Range(0, 5).Select(i => (padT + plotH * (1 - i / 4.0), (max * i / 4).ToString("0.##", Inv))).ToList(),
            Bars = bars, Donut = donut, CustomerTotal = cust.Count, Staff = staff,
            ProfitByProduct = profit, HighStock = high, Promos = promos,
        });
    }

    /// <summary>Làm tròn lên mốc "đẹp" (1, 2, 5 x 10^k) để chia 4 vạch trục tung.</summary>
    static double NiceMax(double v)
    {
        if (v <= 0) return 4;
        double pow = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var m in new[] { 1.0, 2, 4, 5, 8, 10 })
            if (v <= m * pow) return m * pow;
        return 10 * pow;
    }

    /// <summary>Đường cong mượt đi qua các điểm (Catmull-Rom chuyển sang Bezier).</summary>
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

    /// <summary>% thay đổi so với kỳ trước (không có dữ liệu kỳ trước thì báo "Chưa có dữ liệu kỳ trước").</summary>
    static Delta Change(decimal cur, decimal prev)
    {
        if (prev == 0) return new("Chưa có dữ liệu kỳ trước", true);
        var pct = (cur - prev) / prev * 100;
        return new($"{(pct >= 0 ? "+" : "")}{Fmt.Pct(Math.Round(pct, 1))} so với kỳ trước", pct >= 0);
    }

    static string Initials(string first, string last) =>
        (first.Length > 0 ? first[..1] : "") + (last.Length > 0 ? last[..1] : "");
}
