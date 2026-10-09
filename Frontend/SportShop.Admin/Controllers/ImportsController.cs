using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>Quản lý nhập hàng: phiếu nhập hàng và nhà cung cấp.</summary>
public class ImportsController : Controller
{
    public IActionResult Index()
    {
        ViewBag.Menu = "imports";

        // Phiếu nhập (PHIEU_NHAP_HANG): mỗi phiếu cộng vào tồn và cập nhật giá vốn của biến thể
        var slips = new List<Row>
        {
            new(["PN001", "Yonex Việt Nam", Fmt.Date(DateTime.Today.AddDays(-5)), "10 sản phẩm", "Phạm Đức Anh", "[g]" + Fmt.Money(24_000_000)]),
            new(["PN002", "Nike Distribution VN", Fmt.Date(DateTime.Today.AddDays(-3)), "30 sản phẩm", "Phạm Đức Anh", "[g]" + Fmt.Money(51_000_000)]),
            new(["PN003", "Xưởng may Thành Công", Fmt.Date(DateTime.Today.AddDays(-1)), "50 sản phẩm", "Phạm Đức Anh", "[g]" + Fmt.Money(6_000_000)]),
        };

        // Nhà cung cấp (NHA_CUNG_CAP): đang hợp tác / ngừng hợp tác
        var suppliers = new List<Row>
        {
            new(["NCC01", "Yonex Việt Nam", "0281234567", "sales@yonex.vn", "[g]Đang hợp tác"]),
            new(["NCC02", "Nike Distribution VN", "0287654321", "order@nikevn.vn", "[g]Đang hợp tác"]),
            new(["NCC03", "Xưởng may Thành Công", "0909123123", "tc@xuongmay.vn", "[x]Ngừng hợp tác"]),
        };

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý nhập hàng",
            Subtitle = "Theo dõi phiếu nhập và nhà cung cấp",
            Sections =
            [
                new() { Columns = ["Mã phiếu", "Nhà cung cấp", "Ngày nhập", "Số lượng", "Nhân viên lập", "Tổng giá trị"], Rows = slips, AddLabel = "Thêm mới", ShowActions = true },
                new() { Heading = "Nhà cung cấp", Columns = ["Mã NCC", "Tên nhà cung cấp", "Số điện thoại", "Email", "Trạng thái"], Rows = suppliers, AddLabel = "Thêm nhà cung cấp", ShowActions = true, Toolbar = false },
            ]
        });
    }
}
