using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class FeedbackController(ShopData db) : Controller
{
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
        ], Url.Action(nameof(SurveyResult), new { id = s.Id }), s.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Khảo sát",
            Subtitle = "Thu thập ý kiến và xem thống kê kết quả",
            Sections = [new() { Columns = ["Tên khảo sát", "Câu hỏi", "Lượt phản hồi", "Ngày kết thúc", "Trạng thái"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true, Entity = "survey" }]
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
