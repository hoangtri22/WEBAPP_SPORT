using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class FeedbackController(ShopData db) : Controller
{
    // Hộp thư phản hồi từ ShopData.FeedbackItems (PHAN_HOI + TRA_LOI_PHAN_HOI); trả lời gửi qua CrudController.Act với action "feedback_reply".
    public IActionResult Index()
    {
        ViewBag.Menu = "feedback";
        return View(db.FeedbackItems.OrderByDescending(f => f.Id).ToList());
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
            Note = "Dạng câu hỏi: một lựa chọn, nhiều lựa chọn, thang điểm, văn bản. Mỗi khách chỉ có một lượt trả lời cho mỗi khảo sát.",
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
