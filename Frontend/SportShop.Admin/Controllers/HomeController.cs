using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

// Trang Tổng quan (kèm báo cáo thống kê theo kỳ, tạo và tải báo cáo) và trang lỗi, đọc dữ liệu qua ShopData, ReportBuilder và ReportStore.
public class HomeController(ShopData db, ReportStore reports) : Controller
{
    // Trang Tổng quan: tính doanh thu, số đơn hôm nay / hôm qua, nhân viên, khách hàng, phản hồi chưa trả lời, lịch hẹn và 5 đơn gần nhất từ ShopData; phần báo cáo from–to dựng bằng ReportBuilder.Build(). Truyền sang Home/Index.cshtml qua HomeVm.
    public IActionResult Index(DateTime? from, DateTime? to)
    {
        ViewBag.Menu = "home";
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        // Tổng doanh thu đơn đã xuất hóa đơn trong ngày d.
        decimal Revenue(DateTime d) => db.Orders.Where(o => o.Invoiced && o.Date.Date == d).Sum(o => o.Total);
        // Số đơn đặt trong ngày d.
        int OrdersOn(DateTime d) => db.Orders.Count(o => o.Date.Date == d);
        var report = new ReportBuilder(db).Build(from, to);
        report.History = BuildHistory();
        var recent = db.Orders.OrderByDescending(o => o.Id).Take(5).ToList();
        return View(new HomeVm(Revenue(today), Revenue(yesterday), OrdersOn(today), OrdersOn(yesterday),
            db.Employees.Count, db.Employees.Count(e => e.Working), db.ActiveCustomers.Count, db.ActiveCustomers.Count(c => !c.Locked),
            db.FeedbackItems.Count(f => !f.Answered), db.CareNotes.Count(n => n.FollowUp?.Date == today),
            recent, recent.Select(o => o.CustomerId).Distinct().ToDictionary(id => id, id => db.Customer(id).FullName), report));
    }

    // Nhãn loại báo cáo theo kiểu thống kê (khóa của StatsController.KindList).
    public static readonly Dictionary<string, string> ReportTypes = new()
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
             r.Downloads > 0 ? "[g]Đã tải xuống" : "[b]Đã tạo", $"[a]{Url.Action(nameof(DownloadReport), new { id = r.Id })}|Tải {r.Format}"],
            null, 0, null, new() { ["date"] = r.CreatedAt.ToString("yyyy-MM-dd") })).ToList(),
        Filter = HistoryFilter(),
        AddLabel = "Tạo báo cáo", AddId = "newReport", ShowActions = false,
    };

    // Tạo một báo cáo thống kê (Excel hoặc PDF) cho kiểu và khoảng ngày đã chọn, lưu vào ReportStore; trả JSON {ok, message}.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult CreateReport(DateTime? from, DateTime? to, string? kind, string? group, string? format)
    {
        kind = ReportTypes.ContainsKey(kind ?? "") ? kind! : "all";
        format = format == "PDF" ? "PDF" : "Excel";
        try
        {
            var vm = new StatsController(db).Build(from, to, kind, group);
            byte[] bytes = format == "PDF"
                ? StatsPdf.Build(vm, StatsController.KindList.First(k => k.Key == vm.Kind).Text)
                : StatsController.ExcelBytes(vm);
            string name = $"{ReportTypes[kind]} {vm.From:dd/MM/yyyy} – {vm.To:dd/MM/yyyy}";
            reports.Add(name, kind, ReportTypes[kind], format, bytes, vm.From, vm.To, User.Identity?.Name ?? "");
            return Json(new Result(true, $"Đã tạo báo cáo \"{name}\"."));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Json(new Result(false, "Không lưu được file báo cáo, vui lòng thử lại."));
        }
    }

    // Tải một báo cáo đã tạo (theo mã trong lịch sử); mỗi lượt tải tăng bộ đếm để trạng thái chuyển thành "Đã tải xuống".
    public IActionResult DownloadReport(string id)
    {
        var opened = reports.Open(id);
        if (opened is null) return NotFound();
        var (entry, path) = opened.Value;
        string ext = entry.Format == "PDF" ? ".pdf" : ".xlsx";
        string mime = entry.Format == "PDF" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        string safe = string.Concat(entry.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
        return PhysicalFile(path, mime, safe + ext);
    }

    // Trang lỗi chung (Views/Shared/Error.cshtml).
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
