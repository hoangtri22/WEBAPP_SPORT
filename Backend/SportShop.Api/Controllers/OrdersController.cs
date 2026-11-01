using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Api.Errors;
using SportShop.Api.Media;
using SportShop.Application.Catalog;
using SportShop.Contracts.Commerce;
using SportShop.Contracts.Common;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;

namespace SportShop.Api.Controllers;

[ApiController, Route("api/v1/orders"), Authorize(Roles = "Customer")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
[ProducesResponseType<ProblemDetails>(503)]
public sealed class OrdersController(ShopStore shop, MediaUrlResolver media, ICatalogClock clock) : ControllerBase
{
    int CustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Lịch sử đơn của tài khoản, lọc trạng thái và phân trang tại CSDL.</summary>
    [HttpGet, ProducesResponseType<PagedResponse<OrderDto>>(200)]
    public ActionResult<PagedResponse<OrderDto>> List([Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20,
        [RegularExpression("^(cho_xac_nhan|dang_chuan_bi|dang_giao|da_giao|da_huy)$")] string? status = null)
    {
        var orders = shop.Orders(CustomerId, page: page, pageSize: pageSize, status: status);
        return Ok(new PagedResponse<OrderDto>(orders.Select(order => CommerceMapping.Order(order, media, clock)).ToArray(),
            page, pageSize, shop.OrderCount(CustomerId, status)));
    }

    /// <summary>Đơn thuộc tài khoản, kèm sản phẩm, giao hàng, trạng thái và hóa đơn.</summary>
    [HttpGet("{orderId}"), ProducesResponseType<OrderDto>(200)]
    public ActionResult<OrderDto> Detail([Range(1, int.MaxValue)] int orderId)
    {
        var order = shop.Orders(CustomerId, orderId).FirstOrDefault();
        return order is null ? NotFound(ApiProblems.Create(HttpContext, 404, "RESOURCE_NOT_FOUND", "Không tìm thấy đơn hàng."))
            : Ok(CommerceMapping.Order(order, media, clock));
    }

    /// <summary>Đặt giỏ hiện tại trong một transaction; Idempotency-Key bảo vệ khi mạng gửi lại.</summary>
    [HttpPost, ProducesResponseType<OrderDto>(201), ProducesResponseType<OrderDto>(200)]
    public ActionResult<OrderDto> Place(PlaceOrderRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required, StringLength(128, MinimumLength = 1), RegularExpression("^[!-~]{1,128}$")] string requestKey)
    {
        var result = shop.PlaceCartOrder(CustomerId, new ReceiverInfo(request.Name, request.Phone, request.Address),
            request.PaymentMethodId, request.Voucher, requestKey);
        if (result.OrderId is not int id)
            return Conflict(ApiProblems.Create(HttpContext, 409, "ORDER_CONFLICT", result.Error ?? "Không thể tạo đơn hàng."));
        var order = CommerceMapping.Order(shop.Orders(CustomerId, id).Single(), media, clock);
        if (result.Replayed) { Response.Headers["Idempotency-Replayed"] = "true"; return Ok(order); }
        return CreatedAtAction(nameof(Detail), new { orderId = id }, order);
    }

    /// <summary>Hủy đơn còn chờ xác nhận; trả lại tồn kho và lượt dùng voucher đúng một lần.</summary>
    [HttpPost("{orderId}/cancel"), ProducesResponseType<OrderDto>(200)]
    public ActionResult<OrderDto> Cancel([Range(1, int.MaxValue)] int orderId)
    {
        if (!shop.Orders(CustomerId, orderId).Any())
            return NotFound(ApiProblems.Create(HttpContext, 404, "RESOURCE_NOT_FOUND", "Không tìm thấy đơn hàng."));
        var error = shop.CancelOrder(CustomerId, orderId);
        return error is null ? Detail(orderId) : Conflict(ApiProblems.Create(HttpContext, 409, "ORDER_STATE_CONFLICT", error));
    }
}
