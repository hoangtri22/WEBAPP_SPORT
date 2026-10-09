using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>Khuyến mãi (CTKM: theo sản phẩm / theo hóa đơn / theo voucher) và chiến dịch thông báo.</summary>
public class PromotionsController : Controller
{
    public IActionResult Index()
    {
        ViewBag.Menu = "promotions";
        var today = DateTime.Today;

        // (tên, loại, bắt đầu, kết thúc, mô tả mức giảm)
        var data = new (string Name, string Type, DateTime Start, DateTime End, string Discount)[]
        {
            ("Chào hè năng động", "Theo sản phẩm", today.AddDays(-10), today.AddDays(12), "Giảm đến 30%"),
            ("Freeship cuối tuần", "Theo hóa đơn", today.AddDays(2), today.AddDays(4), "Từ 2tr giảm 3%, từ 5tr giảm 5%"),
            ("SPORTNEW", "Theo voucher", today.AddDays(-20), today.AddDays(10), "Giảm 15% (tối đa 100.000đ)"),
            ("VIP10", "Theo voucher", today.AddDays(-40), today.AddDays(50), "Giảm 10% cho hạng VIP"),
            ("Mua nhiều giảm nhiều", "Theo hóa đơn", today.AddDays(-70), today.AddDays(-10), "Từ 2tr giảm 3%, từ 5tr giảm 5%"),
        };

        // Trạng thái tự tính theo thời gian hiệu lực của chương trình
        var promos = data.Select(p => new Row(
        [
            p.Name, p.Type, $"{p.Start:dd/MM} – {p.End:dd/MM/yyyy}", p.Discount,
            p.End < today ? "[x]Hết hạn" : p.Start > today ? "[b]Sắp diễn ra" : "[g]Đang diễn ra"
        ])).ToList();

        // Chiến dịch thông báo: gửi email / SMS / app cho một nhóm khách hoặc tất cả
        var campaigns = new List<Row>
        {
            new(["Chào hè năng động", "Email", "Tất cả khách", "Chào hè năng động", "Trần Minh Quân", "[g]Đã gửi"]),
            new(["Ưu đãi riêng cho VIP", "App", "Khách chi tiêu cao", "VIP10", "Nguyễn Thị Mai", "[x]Nháp"]),
            new(["Sale vợt cầu lông cuối tuần", "SMS", "Yêu thích cầu lông", "Freeship cuối tuần", "Nguyễn Thị Mai", "[x]Nháp"]),
        };

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Khuyến mãi & Marketing",
            Subtitle = "Tạo và theo dõi các chiến dịch ưu đãi",
            Sections =
            [
                new() { Columns = ["Chương trình", "Loại", "Thời gian áp dụng", "Mức giảm", "Trạng thái"], Rows = promos, AddLabel = "Thêm mới", ShowActions = true },
                new() { Heading = "Chiến dịch thông báo", Columns = ["Tiêu đề", "Kênh gửi", "Nhóm khách", "Khuyến mãi gắn kèm", "Người tạo", "Trạng thái"], Rows = campaigns, AddLabel = "Tạo chiến dịch", ShowActions = true, Toolbar = false },
            ]
        });
    }
}
