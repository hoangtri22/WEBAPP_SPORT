using System.ComponentModel.DataAnnotations;

namespace SportShop.Contracts.Commerce;

public sealed record AddCartItemRequest(
    [Range(1, int.MaxValue)] int VariantId,
    [Range(1, 999)] int Quantity);
public sealed record SetCartQuantityRequest([Range(1, 999)] int Quantity);
public sealed record CartItemDto(int VariantId, int ProductId, string Name, string? Size, string? Color,
    string? ImageUrl, decimal UnitPrice, int Quantity, int Stock, decimal Amount, bool Available);
public sealed record CartDto(IReadOnlyList<CartItemDto> Items, int TotalQuantity, decimal Subtotal);
public sealed record ReceiverDto(string Name, string Phone, string Address);
public sealed record CheckoutPaymentMethodDto(int Id, string Name, bool Simulated = true);
public sealed record EligibleVoucherDto(string Code, string Name, decimal Percent, decimal? MaximumDiscount,
    string? MembershipTier, DateTimeOffset ExpiresAt, decimal Discount);
public sealed record CheckoutDto(CartDto Cart, ReceiverDto Receiver,
    IReadOnlyList<CheckoutPaymentMethodDto> PaymentMethods, IReadOnlyList<EligibleVoucherDto> Vouchers,
    decimal Discount, decimal ShippingFee, decimal Total, bool CanPlaceOrder);
public sealed record CheckoutQuoteRequest([StringLength(30)] string? Voucher);
public sealed record PlaceOrderRequest(
    [Required, StringLength(100)] string Name,
    [Required, RegularExpression(@"^0\d{9}$")] string Phone,
    [Required, StringLength(255)] string Address,
    [Range(1, int.MaxValue)] int PaymentMethodId,
    [StringLength(30)] string? Voucher);
public sealed record OrderItemDto(int ProductId, string Name, string Variant, int Quantity,
    decimal UnitPrice, decimal Amount, string? ImageUrl);
public sealed record OrderDto(int Id, string Code, DateTimeOffset OrderedAt, string Status, string StatusLabel,
    string PaymentStatus, string PaymentMethod, ReceiverDto Receiver, string? CancellationReason,
    string? Carrier, string? TrackingNumber, decimal Subtotal, decimal Discount, decimal ShippingFee,
    decimal Total, string? InvoiceNumber, bool CanCancel, IReadOnlyList<OrderItemDto> Items);

