using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>Phản hồi khách hàng (đánh giá / góp ý / khiếu nại) và khảo sát.</summary>
public class FeedbackController : Controller
{
    /// <summary>
    /// Hộp thư phản hồi: 2 tab "Chưa trả lời" / "Đã trả lời",
    /// danh sách bên trái, nội dung + khung trả lời bên phải (xử lý bằng JS trong view).
    /// </summary>
    public IActionResult Index()
    {
        ViewBag.Menu = "feedback";
        return View(SampleData.FeedbackItems.OrderByDescending(f => f.SentAt).ToList());
    }

    /// <summary>Danh sách khảo sát (KHAO_SAT): nháp / đã gửi (đang thu thập) / đã đóng.</summary>
    public IActionResult Surveys()
    {
        ViewBag.Menu = "surveys";
        var rows = new List<Row>
        {
            new(["Mức độ hài lòng sau mua hàng", "4 câu hỏi", "5 / 7 khách hoàn thành", "31/10/2026", "[g]Đang thu thập"]),
            new(["Nhu cầu sản phẩm mùa mới", "6 câu hỏi", "0 / 0 khách hoàn thành", "15/11/2026", "[x]Nháp"]),
            new(["Khảo sát chương trình thành viên", "3 câu hỏi", "6 / 7 khách hoàn thành", "31/08/2026", "[x]Đã kết thúc"]),
        };

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Khảo sát",
            Subtitle = "Thu thập ý kiến và xem thống kê kết quả",
            Note = "Dạng câu hỏi: một lựa chọn, nhiều lựa chọn, thang điểm, văn bản. Mỗi khách chỉ có một lượt trả lời cho mỗi khảo sát.",
            Sections = [new() { Columns = ["Tên khảo sát", "Câu hỏi", "Lượt phản hồi", "Ngày kết thúc", "Trạng thái"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true }]
        });
    }
}
