using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class PromotionsController(ShopData db, ShopCommands cmd) : Controller
{
    // Khuyến mãi & marketing: chương trình khuyến mãi (ShopData.Promotions, chương trình đã ngừng tách sang bảng riêng có nút Khôi phục) và chiến dịch thông báo (ShopData.Campaigns).
    public IActionResult Index()
    {
        ViewBag.Menu = "promotions";
        var promos = db.Promotions.Where(p => p.Status != "ngung").Select(p => new Row(
        [
            p.Name, p.Type, $"{p.Start:dd/MM} – {p.End:dd/MM/yyyy}", p.Discount, StatusOf(p)
        ], Url.Action(nameof(Detail), new { id = p.Id }), p.Id)).ToList();
        var stopped = db.Promotions.Where(p => p.Status == "ngung").Select(p => new Row(
            [p.Name, p.Type, $"{p.Start:dd/MM} – {p.End:dd/MM/yyyy}", p.Discount, StatusOf(p)], Url.Action(nameof(Detail), new { id = p.Id }), p.Id)).ToList();

        var campaigns = db.Campaigns.Select(c => new Row(
            [c.Title, c.Channel, c.Group, c.Promotion, c.Creator, c.Sent ? "[g]Đã gửi" : "[x]Nháp"], Url.Action(nameof(CampaignDetail), new { id = c.Id }), c.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Khuyến mãi & Marketing",
            Subtitle = "Tạo và theo dõi các chiến dịch ưu đãi",
            Sections =
            [
                new() { Columns = ["Chương trình", "Loại", "Thời gian áp dụng", "Mức giảm", "Trạng thái"], Rows = promos, AddLabel = "Thêm mới", ShowActions = true, Entity = "promotion" },
                new() { Heading = "Chương trình đã ngừng", Columns = ["Chương trình", "Loại", "Thời gian áp dụng", "Mức giảm", "Trạng thái"], Rows = stopped, ShowActions = true, Toolbar = false, Entity = "promotion", CanEdit = false, CanDelete = false, RestoreAction = "restore_promotion" },
                new() { Heading = "Chiến dịch thông báo", Columns = ["Tiêu đề", "Kênh gửi", "Nhóm khách", "Khuyến mãi gắn kèm", "Người tạo", "Trạng thái"], Rows = campaigns, AddLabel = "Tạo chiến dịch", ShowActions = true, Toolbar = false, Entity = "campaign" },
            ]
        });
    }

    // Chi tiết chương trình khuyến mãi (Views/Promotions/Detail.cshtml) từ ShopData.Promotions, kèm các đơn hàng đã áp dụng trong ShopData.Orders.
    public IActionResult Detail(int id)
    {
        ViewBag.Menu = "promotions";
        var p = db.Promotions.FirstOrDefault(x => x.Id == id);
        if (p == null) return NotFound();
        var orders = db.Orders.Where(o => o.Voucher != null && o.Voucher.Split(", ").Contains(p.Name)).OrderByDescending(o => o.Id).ToList();
        return View(new PromotionDetailVm(p, orders, db.Campaigns.Where(c => c.Promotion == p.Name).ToList(),
            orders.Select(o => o.CustomerId).Distinct().ToDictionary(c => c, c => db.Customer(c).FullName)));
    }

    // Chi tiết chiến dịch thông báo (Views/Promotions/CampaignDetail.cshtml) từ ShopData.Campaigns; nội dung đọc bằng ShopCommands.Load("campaign").
    public IActionResult CampaignDetail(int id)
    {
        ViewBag.Menu = "promotions";
        var c = db.Campaigns.FirstOrDefault(x => x.Id == id);
        if (c == null) return NotFound();
        return View(new CampaignDetailVm(c, cmd.Load("campaign", c.Id).GetValueOrDefault("noi_dung") ?? "",
            db.Promotions.FirstOrDefault(p => p.Name == c.Promotion), db.CustomerGroups.FirstOrDefault(g => g.Name == c.Group)));
    }

    public static string StatusOf(Promotion p) =>
        p.Status == "ngung" ? "[x]Ngừng" : p.End < DateTime.Today ? "[x]Hết hạn" : p.Start > DateTime.Today ? "[b]Sắp diễn ra" : "[g]Đang diễn ra";
}
