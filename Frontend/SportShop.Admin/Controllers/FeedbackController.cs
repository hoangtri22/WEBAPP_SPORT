using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

// Phản hồi khách hàng và khảo sát, đọc dữ liệu qua ShopData.
public class FeedbackController(ShopData db) : Controller
{
    // Bộ lọc "Tìm kiếm nâng cao" của bảng khảo sát: trạng thái (cột 4), số phản hồi và ngày bắt đầu (data-f).
    static readonly FilterSpec SurveyFilter = new()
    {
        Id = "surveys", Noun = "khảo sát",
        Fields =
        [
            new("status", "Trạng thái", "select", Col: 4),
            new("done", "Số phản hồi", "number", Data: "done"),
            new("start", "Ngày bắt đầu", "date", Data: "start"),
        ],
        Quick = [new("Đang mở", "status", "eq", "Đang thu thập"), new("Chưa có phản hồi", "done", "lte", "0")],
    };

    // Hộp thư phản hồi từ ShopData.FeedbackItems (PHAN_HOI + TRA_LOI_PHAN_HOI); trả lời gửi qua CrudController.Act với action "feedback_reply".
    public IActionResult Index()
    {
        ViewBag.Menu = "feedback";
        var items = db.FeedbackItems.OrderByDescending(f => f.Id).ToList();
        return View(new FeedbackVm(items,
            items.Select(f => f.CustomerId).Distinct().ToDictionary(id => id, db.Customer),
            items.Where(f => f.ReplyEmployeeId.HasValue).Select(f => f.ReplyEmployeeId!.Value).Distinct().ToDictionary(id => id, id => db.Employee(id).FirstName)));
    }

    // Danh sách khảo sát từ ShopData.Surveys kèm số câu hỏi và số khách hoàn thành.
    public IActionResult Surveys()
    {
        ViewBag.Menu = "surveys";
        var rows = db.Surveys.Select(s => new Row(
        [
            s.Title, $"{s.Questions} câu hỏi", $"{s.Completed} / {s.Sent} khách hoàn thành", Fmt.Date(s.End),
            s.Status switch { "da_gui" => "[g]Đang thu thập", "da_dong" => "[x]Đã kết thúc", _ => "[x]Nháp" }
        ], Url.Action(nameof(SurveyResult), new { id = s.Id }), s.Id, null,
            new() { ["done"] = s.Completed.ToString(), ["start"] = s.Start == default ? "" : s.Start.ToString("yyyy-MM-dd") })).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Khảo sát",
            Subtitle = "Thu thập ý kiến và xem thống kê kết quả",
            Sections = [new() { Columns = ["Tên khảo sát", "Câu hỏi", "Lượt phản hồi", "Ngày kết thúc", "Trạng thái"], Rows = rows, Filter = SurveyFilter, AddLabel = "Thêm mới", ShowActions = true, Entity = "survey" }]
        });
    }

    // Thống kê kết quả một khảo sát qua ShopData.SurveyStat().
    public IActionResult SurveyResult(int id)
    {
        ViewBag.Menu = "surveys";
        var s = db.SurveyStat(id);
        if (s == null) return NotFound();
        return View(s);
    }
}
