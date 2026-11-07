using System.ComponentModel.DataAnnotations;

namespace SportShop.Contracts.Commerce;

// Yêu cầu thêm biến thể vào giỏ.
public sealed record AddCartItemRequest(
// Yêu cầu đổi số lượng một dòng giỏ.
    [Range(1, int.MaxValue)] int VariantId,
    [Range(1, 999)] int Quantity);
public sealed record SetCartQuantityRequest([Range(1, 999)] int Quantity);
// Một dòng giỏ hàng trả cho app.
public sealed record CartItemDto(int VariantId, int ProductId, string Name, string? Size, string? Color,
    string? ImageUrl, decimal UnitPrice, int Quantity, int Stock, decimal Amount, bool Available);
// Giỏ hàng và tổng tiền.
public sealed record CartDto(IReadOnlyList<CartItemDto> Items, int TotalQuantity, decimal Subtotal);
// Thông tin người nhận.
public sealed record ReceiverDto(string Name, string Phone, string Address);
// Phương thức thanh toán ở trang thanh toán.
public sealed record CheckoutPaymentMethodDto(int Id, string Name, bool Simulated = true);
// Voucher khách dùng được kèm số tiền giảm.
public sealed record EligibleVoucherDto(string Code, string Name, decimal Percent, decimal? MaximumDiscount,
    string? MembershipTier, DateTimeOffset ExpiresAt, decimal Discount);
// Dữ liệu trang thanh toán.
public sealed record CheckoutDto(CartDto Cart, ReceiverDto Receiver,
    IReadOnlyList<CheckoutPaymentMethodDto> PaymentMethods, IReadOnlyList<EligibleVoucherDto> Vouchers,
    decimal Discount, decimal ShippingFee, decimal Total, bool CanPlaceOrder);
// Yêu cầu tính thử voucher.
public sealed record CheckoutQuoteRequest([StringLength(30)] string? Voucher);
// Yêu cầu đặt đơn hàng.
public sealed record PlaceOrderRequest(
// Một dòng sản phẩm trong đơn.
    [Required, StringLength(100)] string Name,
    [Required, RegularExpression(@"^0\d{9}$")] string Phone,
    [Required, StringLength(255)] string Address,
    [Range(1, int.MaxValue)] int PaymentMethodId,
    [StringLength(30)] string? Voucher);
public sealed record OrderItemDto(int ProductId, string Name, string Variant, int Quantity,
    decimal UnitPrice, decimal Amount, string? ImageUrl);
// Đơn hàng trả cho app.
public sealed record OrderDto(int Id, string Code, DateTimeOffset OrderedAt, string Status, string StatusLabel,
    string PaymentStatus, string PaymentMethod, ReceiverDto Receiver, string? CancellationReason,
    string? Carrier, string? TrackingNumber, decimal Subtotal, decimal Discount, decimal ShippingFee,
    decimal Total, string? InvoiceNumber, bool CanCancel, IReadOnlyList<OrderItemDto> Items);

