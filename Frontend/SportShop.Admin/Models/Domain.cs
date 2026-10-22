using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Html;

namespace SportShop.Admin.Models;

// ============================================================
//  Các lớp dữ liệu, tương ứng các bảng trong chương 4 của báo cáo.
//  Dữ liệu được đọc từ MySQL bởi lớp ShopData.
// ============================================================

/// <summary>Loại sản phẩm (LOAI_SP).</summary>
public record Category(int Id, string Name);

/// <summary>Biến thể sản phẩm (BIEN_THE_SP): size, màu, giá bán, giá vốn, tồn.</summary>
public record Variant(int Id, int ProductId, string? Size, string? Color, decimal Price, decimal Cost, int Stock);

/// <summary>Sản phẩm (SAN_PHAM). Visible = trạng thái hiện/ẩn. Image = đường dẫn ảnh, ví dụ "/images/ao-drifit.jpg".</summary>
public record Product(int Id, string Category, string Name, string Unit, bool Visible, string Description, string? Image = null);

/// <summary>Một dòng chi tiết đơn hàng: giá bán, giảm giá lưu tại thời điểm đặt, giá vốn tại thời điểm bán.</summary>
public record OrderLine(int VariantId, int Qty, decimal Price, decimal Discount, decimal Cost)
{
    /// <summary>Thành tiền = số lượng x đơn giá - giảm giá của dòng.</summary>
    public decimal Amount => Qty * Price - Discount;
}

/// <summary>
/// Đơn hàng (DON_HANG) kèm hóa đơn (HOA_DON) nếu đã thanh toán và vận chuyển (VAN_CHUYEN).
/// Code = số hóa đơn nếu đã lập hóa đơn, ngược lại là mã đơn "DHxxxxx".
/// </summary>
public record Order(
    int Id, string Code, int CustomerId, int? EmployeeId, DateTime Date,
    string Status, string PayStatus, string PayMethod,
    string Carrier, string TrackingCode, decimal ShipFee, decimal Discount,
    string? Voucher, decimal Total, bool Invoiced,
    string Receiver, string ReceiverPhone, string Address, string? CancelReason,
    List<OrderLine> Lines)
{
    /// <summary>Tổng tiền hàng (chưa giảm giá).</summary>
    public decimal Subtotal => Lines.Sum(l => l.Price * l.Qty);
    /// <summary>Tổng giá vốn của các mặt hàng trong đơn.</summary>
    public decimal Cost => Lines.Sum(l => l.Cost * l.Qty);
    /// <summary>Số lượng sản phẩm trong đơn.</summary>
    public int Quantity => Lines.Sum(l => l.Qty);
}

/// <summary>Nhân viên (NHAN_VIEN) + tên đăng nhập, số điện thoại, email, vai trò lấy từ tài khoản.</summary>
public record Employee(int Id, string LastName, string FirstName, string Gender, DateTime? Birth, string Position, decimal BaseSalary, bool Working, string Username, string Phone, string Email, string Role);

/// <summary>Tài khoản đăng nhập (TAI_KHOAN + VAI_TRO). Status: hoat_dong / bi_khoa / da_xoa.</summary>
public record Account(string Username, string Role, string Email, string Phone, string Status, int Id = 0);

/// <summary>Nhóm khách hàng (NHOM_KHACH_HANG) và số khách thuộc nhóm (KHACH_HANG_NHOM_KH).</summary>
public record CustomerGroup(int Id, string Name, string Criterion, string Value, int Count);

/// <summary>Nhà cung cấp (NHA_CUNG_CAP).</summary>
public record Supplier(int Id, string Name, string Phone, string Email, string Address, bool Active);

/// <summary>Phiếu nhập hàng (PHIEU_NHAP_HANG) và tổng số lượng nhập.</summary>
public record ImportSlip(int Id, string Supplier, DateTime Date, int Quantity, string Employee, decimal Total, string Items = "");

/// <summary>Chương trình khuyến mãi (CTKM) kèm mô tả mức giảm.</summary>
public record Promotion(int Id, string Name, string Type, DateTime Start, DateTime End, string Discount, string Status, int Uses);

/// <summary>Chiến dịch thông báo (CHIEN_DICH_THONG_BAO).</summary>
public record Campaign(string Title, string Channel, string Group, string Promotion, string Creator, bool Sent, int Id = 0);

/// <summary>Khảo sát (KHAO_SAT) và số khách đã gửi / đã hoàn thành.</summary>
public record Survey(int Id, string Title, int Questions, int Sent, int Completed, DateTime End, string Status);

/// <summary>Một lần chăm sóc khách hàng (LICH_SU_CHAM_SOC).</summary>
public record CareNote(int CustomerId, int EmployeeId, string Channel, string Content, DateTime Date, DateTime? FollowUp);

/// <summary>Phản hồi của khách (PHAN_HOI) kèm câu trả lời của nhân viên (TRA_LOI_PHAN_HOI).</summary>
public record FeedbackItem(
    int Id, int CustomerId, string Type, int? Stars, string Title, string Body, DateTime SentAt,
    string? Target, string Status, bool Visible, string? Reply, DateTime? ReplyAt, int? ReplyEmployeeId)
{
    /// <summary>Đã được trả lời: trạng thái "đã trả lời" hoặc đã có câu trả lời của cửa hàng (kể cả khi trạng thái còn ghi "đang xử lý").</summary>
    public bool Answered => Status == "da_tra_loi" || Reply != null;
}

/// <summary>Khách hàng (KHACH_HANG) kèm tài khoản, sở thích, sổ địa chỉ và các nhóm khách hàng.</summary>
public class Customer
{
    public int Id { get; init; }
    public string LastName { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string Gender { get; init; } = "";
    public DateTime? Birth { get; init; }
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    /// <summary>Rỗng nếu khách mua tại quầy chưa có tài khoản.</summary>
    public string Username { get; init; } = "";
    /// <summary>Mật khẩu đăng nhập (lưu dạng văn bản trong TAI_KHOAN); chỉ tài khoản Quản lý được xem ở trang chi tiết khách hàng.</summary>
    public string Password { get; init; } = "";
    public bool Locked { get; init; }
    public string[] Interests { get; init; } = [];
    public string[] Addresses { get; init; } = [];
    public string[] Groups { get; init; } = [];
    /// <summary>Tổng chi tiêu, điểm tích lũy, hạng thành viên lấy từ bảng KHACH_HANG.</summary>
    public decimal TotalSpent { get; init; }
    public int Points { get; init; }
    public string Tier { get; init; } = "";

    public string FullName => $"{LastName} {FirstName}".Trim();
    /// <summary>Tuổi tính theo ngày sinh; null nếu chưa có ngày sinh.</summary>
    public int? Age => Birth is DateTime b ? DateTime.Today.Year - b.Year - (DateTime.Today.DayOfYear < b.DayOfYear ? 1 : 0) : null;
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

    /// <summary>Trạng thái đơn hàng (DON_HANG.trang_thai_don_hang) → pill màu.</summary>
    public static string OrderStatus(string s) => s switch
    {
        "cho_xac_nhan" => "[o]Chờ xác nhận",
        "dang_chuan_bi" => "[b]Đang chuẩn bị",
        "dang_giao" => "[p]Đang giao",
        "da_giao" => "[g]Đã giao",
        "da_huy" => "[r]Đã hủy",
        _ => s
    };

    public static string PayStatus(string s) => s == "da_thanh_toan" ? "[g]Đã thanh toán" : "[x]Chưa thanh toán";

    /// <summary>Loại phản hồi (danh_gia / gop_y / khieu_nai) → chữ tiếng Việt.</summary>
    public static string FeedbackType(string t) => t switch { "danh_gia" => "Đánh giá", "gop_y" => "Góp ý", "khieu_nai" => "Khiếu nại", _ => t };

    /// <summary>Hình thức chăm sóc → chữ tiếng Việt.</summary>
    public static string Channel(string c) => c switch { "goi_dien" => "Gọi điện", "email" => "Email", "tin_nhan" => "Tin nhắn", "gap_truc_tiep" => "Gặp trực tiếp", _ => c };
}

/// <summary>Kết quả một câu hỏi khảo sát: Options = (đáp án, số lượt chọn); Texts = các câu trả lời văn bản.</summary>
public record QuestionStat(string Text, string Type, List<(string Label, int Count)> Options, List<string> Texts)
{
    public int Total => Options.Sum(o => o.Count);
    /// <summary>Điểm trung bình (chỉ với câu thang điểm có đáp án là số).</summary>
    public double? Average => Type == "thang_diem" && Total > 0
        ? Options.Where(o => int.TryParse(o.Label, out _)).Sum(o => int.Parse(o.Label) * o.Count) / (double)Total : null;
}

/// <summary>Thống kê một khảo sát (trang xem kết quả).</summary>
public record SurveyStats(int Id, string Title, string? Description, DateTime Start, DateTime End, string Status, int Sent, int Completed, List<QuestionStat> Questions);
