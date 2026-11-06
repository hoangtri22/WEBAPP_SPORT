using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Api.Errors;
using SportShop.Api.Media;
using SportShop.Application.Catalog;
using SportShop.Contracts.Commerce;
using SportShop.CustomerWeb.Services;

namespace SportShop.Api.Controllers;

[ApiController, Route("api/v1/checkout"), Authorize(Roles = "Customer")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(409)]
[ProducesResponseType<ProblemDetails>(503)]
public sealed class CheckoutController(ShopStore shop, MediaUrlResolver media, ICatalogClock clock) : ControllerBase
{
    int CustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    bool IsGuest => User.HasClaim("is_guest", "true");

    /// <summary>Tải người nhận mặc định, giỏ, phương thức giả lập và voucher phù hợp qua Build().</summary>
    [HttpGet, ProducesResponseType<CheckoutDto>(200)]
    public ActionResult<CheckoutDto> Get() => Build(null);

    /// <summary>Tính lại giá, phí giao hàng và voucher từ giỏ trên máy chủ qua Build(), không tạo đơn.</summary>
    [HttpPost("quote"), ProducesResponseType<CheckoutDto>(200)]
    public ActionResult<CheckoutDto> Quote(CheckoutQuoteRequest request) => Build(request.Voucher);

    /// <summary>Các mã tài khoản có thể dùng với giỏ hiện tại, kèm số tiền giảm, lấy từ ShopStore.UsablePromos().</summary>
    [HttpGet("vouchers"), ProducesResponseType<IReadOnlyList<EligibleVoucherDto>>(200)]
    public ActionResult<IReadOnlyList<EligibleVoucherDto>> Vouchers()
    {
        if (IsGuest) return Ok(Array.Empty<EligibleVoucherDto>());
        decimal subtotal = shop.CartLines(CustomerId).Sum(line => line.Amount);
        return Ok(shop.UsablePromos(CustomerId, subtotal).Select(item => CommerceMapping.Voucher(item, clock)).ToArray());
    }

    // Dựng CheckoutDto từ ShopStore: CartLines(), PreviewVoucher(), Receiver(), PaymentMethods(), UsablePromos().
    ActionResult<CheckoutDto> Build(string? voucher)
    {
        var lines = shop.CartLines(CustomerId);
        var cart = CommerceMapping.Cart(lines, media);
        decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(voucher))
        {
            if (IsGuest)
                return Conflict(ApiProblems.Create(HttpContext, 409, "VOUCHER_NOT_APPLICABLE", "Vui lòng đăng nhập tài khoản thành viên để áp dụng mã giảm giá."));
            var preview = shop.PreviewVoucher(CustomerId, voucher, cart.Subtotal);
            if (preview.Error is not null)
                return Conflict(ApiProblems.Create(HttpContext, 409, "VOUCHER_NOT_APPLICABLE", preview.Error));
            discount = Math.Clamp(preview.Discount, 0, cart.Subtotal);
        }
        // Khách vãng lai luôn nhập tay từng trường Họ tên, SĐT, Địa chỉ khi vào Checkout (không tự điền).
        var receiver = IsGuest ? new ReceiverDto("", "", "") : ToReceiverDto(shop.Receiver(CustomerId));
        var vouchers = IsGuest
            ? Array.Empty<EligibleVoucherDto>()
            : shop.UsablePromos(CustomerId, cart.Subtotal).Select(item => CommerceMapping.Voucher(item, clock)).ToArray();
        decimal shipping = ShopStore.ShippingFor(cart.Subtotal);
        return Ok(new CheckoutDto(cart, receiver,
            shop.PaymentMethods().Select(method => new CheckoutPaymentMethodDto(method.Id, method.Name)).ToArray(),
            vouchers,
            discount, shipping, Math.Max(0, cart.Subtotal - discount) + shipping,
            lines.Count > 0 && lines.All(line => line.Enough)));
    }

    static ReceiverDto ToReceiverDto(SportShop.CustomerWeb.Models.ReceiverInfo r) => new(r.Name, r.Phone, r.Address);
}
