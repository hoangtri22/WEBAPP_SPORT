using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public record Delta(string Text, bool Up);

public record BarCol(string Label, int Qty, int Percent, bool Highlight);

public record DonutPart(string Label, int Count, decimal Percent, string Color);

public record StaffRow(string Initials, string Name, string Detail, int Score);

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
}

public class ReportsController(ShopData db) : Controller
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) => v.ToString("0.##", Inv);

    // Báo cáo thống kê trong khoảng from–to (mặc định 30 ngày): doanh thu, số đơn, giá trị TB, lợi nhuận so với kỳ trước, biểu đồ doanh thu, sản phẩm bán chạy, nhóm tuổi khách, hiệu suất nhân viên; dữ liệu từ ShopData (Orders, Customers, Employees, PromoStats()).
    public IActionResult Index(DateTime? from, DateTime? to)
    {
        ViewBag.Menu = "reports";

        var end = (to ?? DefaultEndDate()).Date;
        var start = (from ?? end.AddDays(-29)).Date;
        if (start > end) (start, end) = (end, start);
        int days = (end - start).Days + 1;
        var prevEnd = start.AddDays(-1);
        var prevStart = prevEnd.AddDays(-(days - 1));

        List<Order> Done(DateTime a, DateTime b) => db.Orders
            .Where(o => o.Invoiced && o.Date.Date >= a && o.Date.Date <= b).ToList();
        decimal Rev(Order o) => o.Subtotal - o.Discount;

        var cur = Done(start, end);
        var prev = Done(prevStart, prevEnd);
        decimal revenue = cur.Sum(Rev), prevRevenue = prev.Sum(Rev);
        decimal avg = cur.Count == 0 ? 0 : revenue / cur.Count;
        decimal prevAvg = prev.Count == 0 ? 0 : prevRevenue / prev.Count;

        int n = days <= 84 ? Math.Max(2, (int)Math.Ceiling(days / 7.0)) : 12;
        int bucketLen = days <= 84 ? 7 : (int)Math.Ceiling(days / 12.0);
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

        var qty = cur.SelectMany(o => o.Lines)
            .GroupBy(l => db.Variant(l.VariantId).ProductId)
            .Select(g => (Name: db.Product(g.Key).Name, Qty: g.Sum(l => l.Qty)))
            .OrderByDescending(x => x.Qty).Take(7).ToList();
        int maxQty = qty.Count == 0 ? 1 : qty.Max(x => x.Qty);
        var bars = qty.Select(x => new BarCol(x.Name, x.Qty, Math.Max(6, x.Qty * 100 / maxQty), x.Qty == maxQty)).ToList();

        var cust = db.Customers;
        int g1 = cust.Count(c => c.Age <= 25), g2 = cust.Count(c => c.Age is >= 26 and <= 35), g3 = cust.Count(c => c.Age > 35);
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

        var staffRaw = db.Employees.Where(e => e.Working).Select(e =>
        {
            int inv = db.Orders.Count(o => o.EmployeeId == e.Id && o.Invoiced);
            int care = db.CareNotes.Count(c => c.EmployeeId == e.Id);
            int rep = db.FeedbackItems.Count(f => f.ReplyEmployeeId == e.Id);
            return (E: e, Inv: inv, Care: care, Rep: rep, Total: inv + care + rep);
        }).OrderByDescending(x => x.Total).Take(5).ToList();
        int topTotal = Math.Max(1, staffRaw.Select(x => x.Total).DefaultIfEmpty(0).Max());
        var staff = staffRaw.Select(x => new StaffRow(
            Initials(x.E.FirstName, x.E.LastName), $"{x.E.LastName} {x.E.FirstName}",
            $"{x.Inv} hóa đơn · {x.Care} lần chăm sóc · {x.Rep} phản hồi", x.Total * 100 / topTotal)).ToList();

        var profit = cur.SelectMany(o => o.Lines)
            .GroupBy(l => db.Variant(l.VariantId).ProductId)
            .Select(g =>
            {
                decimal rev = g.Sum(l => l.Amount);
                decimal cost = g.Sum(l => l.Cost * l.Qty);
                return new Row([db.Product(g.Key).Name, g.Sum(l => l.Qty).ToString(), Fmt.Money(rev), Fmt.Money(cost), Fmt.Money(rev - cost)]);
            }).ToList();

        var high = db.ActiveVariants.OrderByDescending(v => v.Stock).Take(5)
            .Select(v => new Row([db.VariantName(v.Id), v.Stock.ToString()])).ToList();

        var promos = db.PromoStats(start, end)
            .Select(p => new Row([p.Name, p.Uses.ToString(), Fmt.Money(p.Discount), Fmt.Money(p.Revenue)])).ToList();

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
            ProfitByProduct = profit, HighStock = high, Promos = promos, Interests = interests,
        });
    }

    static double NiceMax(double v)
    {
        if (v <= 0) return 4;
        double pow = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var m in new[] { 1.0, 2, 4, 5, 8, 10 })
            if (v <= m * pow) return m * pow;
        return 10 * pow;
    }

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

    DateTime DefaultEndDate()
    {
        var today = DateTime.Today;
        var invoiced = db.Orders.Where(o => o.Invoiced).Select(o => o.Date.Date).ToList();
        bool anyRecent = invoiced.Any(d => d >= today.AddDays(-29) && d <= today);
        return invoiced.Count == 0 || anyRecent ? today : invoiced.Max();
    }

    static Delta Change(decimal cur, decimal prev)
    {
        if (prev == 0) return new("Chưa có dữ liệu kỳ trước", true);
        var pct = (cur - prev) / prev * 100;
        return new($"{(pct >= 0 ? "+" : "")}{Fmt.Pct(Math.Round(pct, 1))} so với kỳ trước", pct >= 0);
    }

    static string Initials(string first, string last) =>
        (first.Length > 0 ? first[..1] : "") + (last.Length > 0 ? last[..1] : "");
}
