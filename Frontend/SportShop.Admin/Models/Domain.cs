using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Html;

namespace SportShop.Admin.Models;


public record Category(int Id, string Name);
public record ReportOrder(DateTime Date, decimal Revenue, decimal Cost);
public record ReportProduct(string Name, int Qty, decimal Revenue, decimal Cost);
public record InvoiceRow(string Number, DateTime Date, decimal Total, string Employee, string Customer);
public record InvoiceLine(DateTime Date, string Product, decimal Amount);

public record Variant(int Id, int ProductId, string? Size, string? Color, decimal Price, decimal Cost, int Stock);

public record Product(int Id, string Category, string Name, string Unit, bool Visible, string Description, string? Image = null);

public record OrderLine(int VariantId, int Qty, decimal Price, decimal Discount, decimal Cost)
{
    public decimal Amount => Qty * Price - Discount;
}

public record Order(
    int Id, string Code, int CustomerId, int? EmployeeId, DateTime Date,
    string Status, string PayStatus, string PayMethod,
    string Carrier, string TrackingCode, decimal ShipFee, decimal Discount,
    string? Voucher, decimal Total, bool Invoiced,
    string Receiver, string ReceiverPhone, string Address, string? CancelReason,
    List<OrderLine> Lines)
{
    public decimal Subtotal => Lines.Sum(l => l.Price * l.Qty);
    public decimal Cost => Lines.Sum(l => l.Cost * l.Qty);
    public int Quantity => Lines.Sum(l => l.Qty);
}

public record Employee(int Id, string LastName, string FirstName, string Gender, DateTime? Birth, string Position, decimal BaseSalary, bool Working, string Username, string Phone, string Email, string Role);

public record Account(string Username, string Role, string Email, string Phone, string Status, int Id = 0);

public record CustomerGroup(int Id, string Name, string Criterion, string Value, int Count);

public record Supplier(int Id, string Name, string Phone, string Email, string Address, bool Active);

public record ImportSlip(int Id, string Supplier, DateTime Date, int Quantity, string Employee, decimal Total, string Items = "");

public record Promotion(int Id, string Name, string Type, DateTime Start, DateTime End, string Discount, string Status, int Uses);

public record Campaign(string Title, string Channel, string Group, string Promotion, string Creator, bool Sent, int Id = 0);

public record Survey(int Id, string Title, int Questions, int Sent, int Completed, DateTime End, string Status);

public record CareNote(int CustomerId, int EmployeeId, string Channel, string Content, DateTime Date, DateTime? FollowUp);

public record FeedbackItem(
    int Id, int CustomerId, string Type, int? Stars, string Title, string Body, DateTime SentAt,
    string? Target, string Status, bool Visible, string? Reply, DateTime? ReplyAt, int? ReplyEmployeeId)
{
    public bool Answered => Status == "da_tra_loi" || Reply != null;
}

public class Customer
{
    public int Id { get; init; }
    public string LastName { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string Gender { get; init; } = "";
    public DateTime? Birth { get; init; }
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public bool Locked { get; init; }
    public bool Deleted { get; init; }
    public string[] Interests { get; init; } = [];
    public string[] Addresses { get; init; } = [];
    public string[] Groups { get; init; } = [];
    public decimal TotalSpent { get; init; }
    public int Points { get; init; }
    public string Tier { get; init; } = "";

    public string FullName => $"{LastName} {FirstName}".Trim();
    public int? Age => Birth is DateTime b ? DateTime.Today.Year - b.Year - (DateTime.Today.DayOfYear < b.DayOfYear ? 1 : 0) : null;
}

public static class Fmt
{
    static readonly CultureInfo Vi = new("vi-VN");

    public static string Money(decimal v) => string.Format(Vi, "{0:N0}đ", v);

    public static string Compact(decimal v) => v switch
    {
        >= 1_000_000_000 => string.Format(Vi, "{0:0.##} tỷ đ", v / 1_000_000_000),
        >= 1_000_000 => string.Format(Vi, "{0:0.#} tr", v / 1_000_000),
        _ => Money(v)
    };

    public static string Date(DateTime d) => d.ToString("dd/MM/yyyy", Vi);
    public static string DateTimeS(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", Vi);
    public static string DateTimeDot(DateTime d) => d.ToString("dd/MM/yyyy", Vi) + " · " + d.ToString("HH:mm", Vi);

    public static string Pct(decimal v) => string.Format(Vi, "{0:0.#}%", v);

    public static string Ago(DateTime t)
    {
        var d = DateTime.Now - t;
        if (d.TotalMinutes < 1) return "Vừa xong";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes} phút trước";
        if (d.TotalHours < 24) return $"{(int)d.TotalHours} giờ trước";
        if (d.TotalDays < 2) return "Hôm qua";
        return $"{(int)d.TotalDays} ngày trước";
    }

    public static IHtmlContent Cell(string s)
    {
        if (s.StartsWith("[l]"))
        {
            const int Shown = 3;
            var items = s[3..].Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var html = new System.Text.StringBuilder("<ul class=\"item-list\">");
            foreach (var item in items.Take(Shown)) html.Append("<li>").Append(WebUtility.HtmlEncode(item)).Append("</li>");
            if (items.Length > Shown)
            {
                html.Append("<li class=\"more\" title=\"").Append(WebUtility.HtmlEncode(string.Join("\n", items))).Append("\">+ ")
                    .Append(items.Length - Shown).Append(" mặt hàng khác</li>");
                foreach (var item in items.Skip(Shown)) html.Append("<li hidden>").Append(WebUtility.HtmlEncode(item)).Append("</li>");
            }
            return new HtmlString(html.Append("</ul>").ToString());
        }
        if (s.Length > 3 && s[0] == '[' && s[2] == ']')
            return new HtmlString($"<span class=\"pill p-{s[1]}\"><i></i>{WebUtility.HtmlEncode(s[3..])}</span>");
        return new HtmlString(WebUtility.HtmlEncode(s));
    }

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

    public static string FeedbackType(string t) => t switch { "danh_gia" => "Đánh giá", "gop_y" => "Góp ý", "khieu_nai" => "Khiếu nại", _ => t };

    public static string Channel(string c) => c switch { "goi_dien" => "Gọi điện", "email" => "Email", "tin_nhan" => "Tin nhắn", "gap_truc_tiep" => "Gặp trực tiếp", _ => c };
}

public record QuestionStat(string Text, string Type, List<(string Label, int Count)> Options, List<string> Texts)
{
    public int Total => Options.Sum(o => o.Count);
    public double? Average => Type == "thang_diem" && Total > 0
        ? Options.Where(o => int.TryParse(o.Label, out _)).Sum(o => int.Parse(o.Label) * o.Count) / (double)Total : null;
}

public record SurveyStats(int Id, string Title, string? Description, DateTime Start, DateTime End, string Status, int Sent, int Completed, List<QuestionStat> Questions);
