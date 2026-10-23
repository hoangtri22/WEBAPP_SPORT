namespace SportShop.CustomerWeb.Models;

// ============================================================
//  Dữ liệu cho cửa hàng: danh sách sản phẩm, giỏ hàng, đặt hàng, đơn hàng của tôi.
//  Đọc / ghi qua Services/ShopStore (MySQL).
// ============================================================

public record ShopCategory(int Id, string Name, int Count);

/// <summary>Sản phẩm trong danh sách. MinPrice / MaxPrice là giá bán thấp nhất / cao nhất của các biến thể đang bán.</summary>
public record ShopProduct(int Id, string Name, string Category, string Unit, string? Description, string? Image, decimal? MinPrice, decimal? MaxPrice, int Stock)
{
    public string PriceText => MinPrice == null ? "Liên hệ"
        : MinPrice == MaxPrice ? Fmt.Money(MinPrice.Value) : "Từ " + Fmt.Money(MinPrice.Value);
}

/// <summary>Biến thể (size, màu) của một sản phẩm.</summary>
public record ShopVariant(int Id, string? Size, string? Color, decimal Price, int Stock, string? Image = null)
{
    public string Label => string.Join(" / ", new[] { Size, Color }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>Một dòng trong giỏ hàng (đã nối thông tin sản phẩm và tồn kho hiện tại).</summary>
public record CartLine(int VariantId, int ProductId, string Name, string? Size, string? Color, string? Image, decimal Price, int Qty, int Stock)
{
    public decimal Amount => Price * Qty;
    public string Label => string.Join(" / ", new[] { Size, Color }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public bool Enough => Qty <= Stock;
}

public record OrderLineView(string Name, string Label, int Qty, decimal Price, decimal Amount, string? Image, int ProductId);

/// <summary>Đơn hàng của khách (DON_HANG + VAN_CHUYEN + HOA_DON).</summary>
public class MyOrder
{
    public int Id { get; init; }
    public string Code => "DH" + Id.ToString("D5");
    public DateTime Date { get; init; }
    /// <summary>cho_xac_nhan / dang_chuan_bi / dang_giao / da_giao / da_huy</summary>
    public string Status { get; init; } = "";
    /// <summary>chua_thanh_toan / da_thanh_toan</summary>
    public string PayStatus { get; init; } = "";
    public string PayMethod { get; init; } = "";
    public string Receiver { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Address { get; init; } = "";
    public string? CancelReason { get; init; }
    public string? Carrier { get; init; }
    public string? Tracking { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Discount { get; init; }
    public decimal Ship { get; init; }
    public decimal Total { get; init; }
    public string? Invoice { get; init; }
    public List<OrderLineView> Lines { get; init; } = [];

    public string StatusText => Status switch
    {
        "cho_xac_nhan" => "Chờ xác nhận",
        "dang_chuan_bi" => "Đang chuẩn bị hàng",
        "dang_giao" => "Đang giao hàng",
        "da_giao" => "Đã giao",
        _ => "Đã hủy",
    };
    /// <summary>Màu của nhãn trạng thái (lớp CSS p-...).</summary>
    public string StatusTone => Status switch { "da_giao" => "p-g", "da_huy" => "p-r", "dang_giao" => "p-b", _ => "p-o" };
    public string PayText => PayStatus == "da_thanh_toan" ? "Đã thanh toán" : "Chưa thanh toán";
    public bool CanCancel => Status == "cho_xac_nhan";
    /// <summary>Số bước đã qua (0–3) để vẽ thanh tiến trình.</summary>
    public int Step => Status switch { "cho_xac_nhan" => 0, "dang_chuan_bi" => 1, "dang_giao" => 2, "da_giao" => 3, _ => -1 };
}

/// <summary>Chương trình khuyến mãi theo voucher đang áp dụng (trang Khuyến mãi).</summary>
public record PromoInfo(string Code, string Name, decimal Percent, decimal? MaxDiscount, string? Tier, DateTime End);

public record PaymentMethod(int Id, string Name);

/// <summary>Thông tin nhận hàng điền sẵn ở trang thanh toán.</summary>
public record ReceiverInfo(string Name, string Phone, string Address);

/// <summary>Lỗi nghiệp vụ khi đặt hàng, nội dung hiện nguyên văn cho khách.</summary>
public class OrderError(string message) : Exception(message);

public static class Fmt
{
    static readonly System.Globalization.CultureInfo Vi = new("vi-VN");
    public static string Money(decimal v) => string.Format(Vi, "{0:N0}₫", v);
    public static string Date(DateTime d) => d.ToString("dd/MM/yyyy", Vi);
    public static string Stamp(DateTime d) => d.ToString("HH:mm, dd/MM/yyyy", Vi);
}
