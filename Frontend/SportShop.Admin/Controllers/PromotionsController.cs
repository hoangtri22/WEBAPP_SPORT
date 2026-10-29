using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class PromotionsController(ShopData db) : Controller
{
    // Khuyến mãi & marketing: chương trình khuyến mãi (ShopData.Promotions) và chiến dịch thông báo (ShopData.Campaigns).
    public IActionResult Index()
    {
        ViewBag.Menu = "promotions";
        var today = DateTime.Today;

        var promos = db.Promotions.Select(p => new Row(
        [
            p.Name, p.Type, $"{p.Start:dd/MM} – {p.End:dd/MM/yyyy}", p.Discount,
            p.Status == "ngung" ? "[x]Ngừng" : p.End < today ? "[x]Hết hạn" : p.Start > today ? "[b]Sắp diễn ra" : "[g]Đang diễn ra"
        ], null, p.Id)).ToList();

        var campaigns = db.Campaigns.Select(c => new Row(
            [c.Title, c.Channel, c.Group, c.Promotion, c.Creator, c.Sent ? "[g]Đã gửi" : "[x]Nháp"], null, c.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Khuyến mãi & Marketing",
            Subtitle = "Tạo và theo dõi các chiến dịch ưu đãi",
            Sections =
            [
                new() { Columns = ["Chương trình", "Loại", "Thời gian áp dụng", "Mức giảm", "Trạng thái"], Rows = promos, AddLabel = "Thêm mới", ShowActions = true, Entity = "promotion" },
                new() { Heading = "Chiến dịch thông báo", Columns = ["Tiêu đề", "Kênh gửi", "Nhóm khách", "Khuyến mãi gắn kèm", "Người tạo", "Trạng thái"], Rows = campaigns, AddLabel = "Tạo chiến dịch", ShowActions = true, Toolbar = false, Entity = "campaign" },
            ]
        });
    }
}
