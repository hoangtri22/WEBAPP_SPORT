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

    /// <summary>Tải người nhận mặc định, giỏ, phương thức giả lập và voucher phù hợp.</summary>
    [HttpGet, ProducesResponseType<CheckoutDto>(200)]
    public ActionResult<CheckoutDto> Get() => Build(null);

    /// <summary>Tính lại giá, phí giao hàng và voucher từ giỏ trên máy chủ, không tạo đơn.</summary>
    [HttpPost("quote"), ProducesResponseType<CheckoutDto>(200)]
    public ActionResult<CheckoutDto> Quote(CheckoutQuoteRequest request) => Build(request.Voucher);

    /// <summary>Các mã tài khoản có thể dùng với giỏ hiện tại, kèm số tiền giảm.</summary>
    [HttpGet("vouchers"), ProducesResponseType<IReadOnlyList<EligibleVoucherDto>>(200)]
    public ActionResult<IReadOnlyList<EligibleVoucherDto>> Vouchers()
    {
        decimal subtotal = shop.CartLines(CustomerId).Sum(line => line.Amount);
        return Ok(shop.UsablePromos(CustomerId, subtotal).Select(item => CommerceMapping.Voucher(item, clock)).ToArray());
    }

    ActionResult<CheckoutDto> Build(string? voucher)
    {
        var lines = shop.CartLines(CustomerId);
        var cart = CommerceMapping.Cart(lines, media);
        decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(voucher))
        {
            var preview = shop.PreviewVoucher(CustomerId, voucher, cart.Subtotal);
            if (preview.Error is not null)
                return Conflict(ApiProblems.Create(HttpContext, 409, "VOUCHER_NOT_APPLICABLE", preview.Error));
            discount = Math.Clamp(preview.Discount, 0, cart.Subtotal);
        }
        var receiver = shop.Receiver(CustomerId);
        decimal shipping = ShopStore.ShippingFor(cart.Subtotal);
        return Ok(new CheckoutDto(cart, new(receiver.Name, receiver.Phone, receiver.Address),
            shop.PaymentMethods().Select(method => new CheckoutPaymentMethodDto(method.Id, method.Name)).ToArray(),
            shop.UsablePromos(CustomerId, cart.Subtotal).Select(item => CommerceMapping.Voucher(item, clock)).ToArray(),
            discount, shipping, Math.Max(0, cart.Subtotal - discount) + shipping,
            lines.Count > 0 && lines.All(line => line.Enough)));
    }
}
