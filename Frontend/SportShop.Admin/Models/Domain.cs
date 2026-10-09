using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Html;

namespace SportShop.Admin.Models;

// ============================================================
//  Các lớp dữ liệu mẫu (tương ứng các bảng trong PDF, chương 4)
//  Sau này khi có database sẽ thay bằng entity thật.
// ============================================================

/// <summary>Biến thể sản phẩm (BIEN_THE_SP): size, màu, giá bán, giá vốn, tồn.</summary>
public record Variant(int Id, int ProductId, string? Size, string? Color, decimal Price, decimal Cost, int Stock);

/// <summary>Sản phẩm (SAN_PHAM). Visible = trạng thái hiện/ẩn.</summary>
public record Product(int Id, string Category, string Name, string Unit, bool Visible, string Description);

/// <summary>Một dòng chi tiết hóa đơn (CHI_TIET_HOA_DON).</summary>
public record OrderLine(int VariantId, int Qty);

/// <summary>Hóa đơn (HOA_DON) kèm thông tin vận chuyển (VAN_CHUYEN).</summary>
public record Order(
    string Code, int CustomerId, int? EmployeeId, DateTime Date,
    string Status, string PayStatus, string PayMethod,
    string Carrier, string TrackingCode, decimal ShipFee, decimal Discount,
    string? Voucher, List<OrderLine> Lines)
{
    /// <summary>Tổng tiền hàng (chưa giảm giá).</summary>
    public decimal Subtotal => Lines.Sum(l => SampleData.Variant(l.VariantId).Price * l.Qty);
    /// <summary>Tổng giá vốn của các mặt hàng trong hóa đơn.</summary>
    public decimal Cost => Lines.Sum(l => SampleData.Variant(l.VariantId).Cost * l.Qty);
    /// <summary>Tổng thanh toán = tiền hàng - giảm giá + phí ship.</summary>
    public decimal Total => Subtotal - Discount + ShipFee;
    /// <summary>Số lượng sản phẩm trong hóa đơn.</summary>
    public int Quantity => Lines.Sum(l => l.Qty);
}

/// <summary>Nhân viên (NHAN_VIEN) + số điện thoại lấy từ tài khoản.</summary>
public record Employee(int Id, string LastName, string FirstName, string Gender, DateTime Birth, string Position, decimal BaseSalary, bool Working, string Username, string Phone);

/// <summary>Một lần chăm sóc khách hàng (LICH_SU_CHAM_SOC).</summary>
public record CareNote(int CustomerId, int EmployeeId, string Channel, string Content, DateTime Date, DateTime? FollowUp);

/// <summary>Phản hồi của khách (PHAN_HOI) kèm câu trả lời của nhân viên (TRA_LOI_PHAN_HOI).</summary>
public record FeedbackItem(
    int Id, int CustomerId, string Type, int? Stars, string Title, string Body, DateTime SentAt,
    string? Target, string Status, bool Visible, string? Reply, DateTime? ReplyAt, int? ReplyEmployeeId);

/// <summary>Khách hàng (KHACH_HANG). Hạng và điểm được tính từ tổng chi tiêu.</summary>
public class Customer
{
    public int Id { get; init; }
    public string LastName { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string Gender { get; init; } = "";
    public DateTime Birth { get; init; }
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public string Username { get; init; } = "";
    public bool Locked { get; init; }
    public string[] Interests { get; init; } = [];
    public string[] Addresses { get; init; } = [];

    public string FullName => $"{LastName} {FirstName}";
    public int Age => DateTime.Today.Year - Birth.Year;
    /// <summary>Tổng chi tiêu = tổng thanh toán của các hóa đơn đã hoàn thành.</summary>
    public decimal TotalSpent => SampleData.Orders.Where(o => o.CustomerId == Id && o.Status == "hoan_thanh").Sum(o => o.Total);
    /// <summary>Điểm tích lũy: cứ 10.000đ được 1 điểm (quy tắc tạm, PDF chưa nêu cụ thể).</summary>
    public int Points => (int)(TotalSpent / 10000);
    /// <summary>Hạng thành viên theo mốc chi tiêu (mốc tạm, PDF chưa nêu cụ thể).</summary>
    public string Tier => TotalSpent switch { >= 10_000_000 => "VIP", >= 5_000_000 => "Vàng", >= 2_000_000 => "Bạc", _ => "Thường" };
}

/// <summary>Các hàm định dạng dùng chung cho view.</summary>
public static class Fmt
{
    static readonly CultureInfo Vi = new("vi-VN");

    /// <summary>Tiền đầy đủ, ví dụ 53.990đ.</summary>
    public static string Money(decimal v) => string.Format(Vi, "{0:N0}đ", v);

    /// <summary>Tiền rút gọn cho thẻ thống kê: 1,24 tỷ đ / 48,6 tr / 358.000đ.</summary>
    public static string Compact(decimal v) => v switch
    {
        >= 1_000_000_000 => string.Format(Vi, "{0:0.##} tỷ đ", v / 1_000_000_000),
        >= 1_000_000 => string.Format(Vi, "{0:0.#} tr", v / 1_000_000),
        _ => Money(v)
    };

    public static string Date(DateTime d) => d.ToString("dd/MM/yyyy", Vi);
    public static string DateTimeS(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", Vi);
    /// <summary>Dạng "24/05/2025 · 09:32" như trong thiết kế.</summary>
    public static string DateTimeDot(DateTime d) => d.ToString("dd/MM/yyyy", Vi) + " · " + d.ToString("HH:mm", Vi);

    /// <summary>Phần trăm kiểu Việt Nam: 18,4%.</summary>
    public static string Pct(decimal v) => string.Format(Vi, "{0:0.#}%", v);

    /// <summary>Thời gian tương đối: "10 phút trước", "Hôm qua"...</summary>
    public static string Ago(DateTime t)
    {
        var d = DateTime.Now - t;
        if (d.TotalMinutes < 1) return "Vừa xong";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes} phút trước";
        if (d.TotalHours < 24) return $"{(int)d.TotalHours} giờ trước";
        if (d.TotalDays < 2) return "Hôm qua";
        return $"{(int)d.TotalDays} ngày trước";
    }

    /// <summary>
    /// Hiển thị ô bảng. Chuỗi bắt đầu bằng [g]/[o]/[b]/[r]/[x]/[p] sẽ thành "pill" có chấm màu:
    /// g = xanh lá, o = vàng cam, b = xanh dương, r = đỏ, x = xám, p = tím.
    /// </summary>
    public static IHtmlContent Cell(string s)
    {
        if (s.Length > 3 && s[0] == '[' && s[2] == ']')
            return new HtmlString($"<span class=\"pill p-{s[1]}\"><i></i>{WebUtility.HtmlEncode(s[3..])}</span>");
        return new HtmlString(WebUtility.HtmlEncode(s));
    }

    /// <summary>Trạng thái hóa đơn (theo PDF) → pill màu.</summary>
    public static string OrderStatus(string s) => s switch
    {
        "cho_xac_nhan" => "[o]Chờ xác nhận",
        "dang_chuan_bi" => "[b]Đang chuẩn bị",
        "dang_giao" => "[p]Đang giao",
        "hoan_thanh" => "[g]Hoàn thành",
        "da_huy" => "[r]Đã hủy",
        _ => s
    };

    public static string PayStatus(string s) => s == "da_thanh_toan" ? "[g]Đã thanh toán" : "[x]Chưa thanh toán";

    /// <summary>Loại phản hồi (danh_gia / gop_y / khieu_nai) → chữ tiếng Việt.</summary>
    public static string FeedbackType(string t) => t switch { "danh_gia" => "Đánh giá", "gop_y" => "Góp ý", "khieu_nai" => "Khiếu nại", _ => t };

    /// <summary>Hình thức chăm sóc → chữ tiếng Việt.</summary>
    public static string Channel(string c) => c switch { "goi_dien" => "Gọi điện", "email" => "Email", "tin_nhan" => "Tin nhắn", "gap_truc_tiep" => "Gặp trực tiếp", _ => c };
}
