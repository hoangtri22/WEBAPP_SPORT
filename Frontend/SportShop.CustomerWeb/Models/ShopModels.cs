namespace SportShop.CustomerWeb.Models;


// Loại sản phẩm kèm số sản phẩm.
public record ShopCategory(int Id, string Name, int Count);

// Sản phẩm hiển thị ở cửa hàng.
public record ShopProduct(int Id, string Name, string Category, string Unit, string? Description, string? Image, decimal? MinPrice, decimal? MaxPrice, int Stock)
{
    public string PriceText => MinPrice == null ? "Liên hệ"
        : MinPrice == MaxPrice ? Fmt.Money(MinPrice.Value) : "Từ " + Fmt.Money(MinPrice.Value);
}

// Biến thể sản phẩm hiển thị ở cửa hàng.
public record ShopVariant(int Id, string? Size, string? Color, decimal Price, int Stock, string? Image = null)
{
    public string Label => string.Join(" / ", new[] { Size, Color }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

// Một dòng giỏ hàng đã đọc giá và tồn kho.
public record CartLine(int VariantId, int ProductId, string Name, string? Size, string? Color, string? Image, decimal Price, int Qty, int Stock)
{
    public decimal Amount => Price * Qty;
    public string Label => string.Join(" / ", new[] { Size, Color }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public bool Enough => Qty <= Stock;
}

// Một dòng sản phẩm trong đơn của khách.
public record OrderLineView(string Name, string Label, int Qty, decimal Price, decimal Amount, string? Image, int ProductId);

// Đơn hàng hiện ở trang cá nhân của khách.
public class MyOrder
{
    public int Id { get; init; }
    public string Code => "DH" + Id.ToString("D5");
    public DateTime Date { get; init; }
    public string Status { get; init; } = "";
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
    public string StatusTone => Status switch { "da_giao" => "p-g", "da_huy" => "p-r", "dang_giao" => "p-b", _ => "p-o" };
    public string PayText => PayStatus == "da_thanh_toan" ? "Đã thanh toán" : "Chưa thanh toán";
    public bool CanCancel => Status == "cho_xac_nhan" && PayStatus != "da_thanh_toan";
    public int Step => Status switch { "cho_xac_nhan" => 0, "dang_chuan_bi" => 1, "dang_giao" => 2, "da_giao" => 3, _ => -1 };
}

// Thông tin một voucher.
public record PromoInfo(string Code, string Name, decimal Percent, decimal? MaxDiscount, string? Tier, DateTime End);
// Voucher khách dùng được kèm số tiền giảm.
public record UsablePromo(PromoInfo Promo, decimal Discount);

// Mức giảm theo đơn của một chương trình khuyến mãi.
public record OrderPromo(int CtkmId, string Name, decimal Threshold, decimal Percent, decimal? MaxDiscount);

// Mức giảm theo đơn đang áp dụng.
public record AppliedOrderPromo(int CtkmId, string Name, decimal Discount);

// Sản phẩm gợi ý mua thêm ở giỏ hàng.
public record UpsellItem(int ProductId, string Name, string? Image, decimal Price, int? VariantId);

// Gợi ý mua thêm để đạt mức giảm kế tiếp.
public record CartUpsell(string Name, decimal Missing, decimal Percent, decimal Discount, List<UpsellItem> Items);

// Dữ liệu trang giỏ hàng (Views/Cart/Index.cshtml).
public record CartPageVm(List<CartLine> Lines, AppliedOrderPromo? Applied, CartUpsell? Upsell);

// Phương thức thanh toán.
public record PaymentMethod(int Id, string Name);

// Thông tin người nhận.
public record ReceiverInfo(string Name, string Phone, string Address);

// Lỗi nghiệp vụ khi đặt hàng, thông báo được hiện nguyên cho khách.
public class OrderError(string message) : Exception(message);

// Các hàm định dạng dữ liệu hiển thị ở web khách.
public static class Fmt
{
    static readonly System.Globalization.CultureInfo Vi = new("vi-VN");
    // Định dạng số tiền kiểu 1.000₫.
    public static string Money(decimal v) => string.Format(Vi, "{0:N0}₫", v);
    // Định dạng ngày dd/MM/yyyy.
    public static string Date(DateTime d) => d.ToString("dd/MM/yyyy", Vi);
    // Định dạng giờ và ngày.
    public static string Stamp(DateTime d) => d.ToString("HH:mm, dd/MM/yyyy", Vi);
}
