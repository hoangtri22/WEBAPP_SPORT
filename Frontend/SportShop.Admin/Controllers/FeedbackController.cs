using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>Phản hồi khách hàng (đánh giá / góp ý / khiếu nại) và khảo sát.</summary>
public class FeedbackController(ShopData db) : Controller
{
    /// <summary>
    /// Hộp thư phản hồi: 2 tab "Chưa trả lời" / "Đã trả lời",
    /// danh sách bên trái, nội dung + khung trả lời bên phải (xử lý bằng JS trong view).
    /// </summary>
    public IActionResult Index()
    {
        ViewBag.Menu = "feedback";
        return View(db.FeedbackItems.OrderByDescending(f => f.SentAt).ToList());
    }

    /// <summary>Danh sách khảo sát (KHAO_SAT): nháp / đã gửi (đang thu thập) / đã đóng.</summary>
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

    /// <summary>Thống kê kết quả một khảo sát (số khách hoàn thành, số lượt chọn từng đáp án) + nút gửi / đóng khảo sát.</summary>
    public IActionResult SurveyResult(int id)
    {
        ViewBag.Menu = "surveys";
        var s = db.SurveyStat(id);
        if (s == null) return NotFound();
        return View(s);
    }
}
