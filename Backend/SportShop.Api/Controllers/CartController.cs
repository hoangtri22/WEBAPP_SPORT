using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Api.Errors;
using SportShop.Api.Media;
using SportShop.Contracts.Commerce;
using SportShop.CustomerWeb.Services;

namespace SportShop.Api.Controllers;

[ApiController, Route("api/v1/cart"), Authorize(Roles = "Customer")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(409)]
[ProducesResponseType<ProblemDetails>(503)]
public sealed class CartController(ShopStore shop, MediaUrlResolver media) : ControllerBase
{
    int CustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Giỏ hàng dùng chung giữa Web và Mobile, giá/tồn lấy từ CSDL.</summary>
    [HttpGet, ProducesResponseType<CartDto>(200)]
    public ActionResult<CartDto> Get() => Ok(CommerceMapping.Cart(shop.CartLines(CustomerId), media));

    /// <summary>Thêm biến thể vào giỏ, cộng vào số lượng đã có.</summary>
    [HttpPost("items"), ProducesResponseType<CartDto>(200)]
    public ActionResult<CartDto> Add(AddCartItemRequest request)
    {
        var error = shop.AddToCart(CustomerId, request.VariantId, request.Quantity);
        return error is null ? Get() : Conflict(ApiProblems.Create(HttpContext, 409, "CART_CONFLICT", error));
    }

    /// <summary>Thay số lượng dòng giỏ; không vượt quá tồn kho hoặc 999.</summary>
    [HttpPut("items/{variantId}"), ProducesResponseType<CartDto>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public ActionResult<CartDto> Set([Range(1, int.MaxValue)] int variantId, SetCartQuantityRequest request)
    {
        if (!shop.GetCart(CustomerId).ContainsKey(variantId))
            return NotFound(ApiProblems.Create(HttpContext, 404, "RESOURCE_NOT_FOUND", "Không tìm thấy dòng giỏ hàng."));
        var error = shop.SetQty(CustomerId, variantId, request.Quantity);
        return error is null ? Get() : Conflict(ApiProblems.Create(HttpContext, 409, "CART_CONFLICT", error));
    }

    /// <summary>Xóa dòng giỏ của tài khoản đang đăng nhập; gọi lại vẫn thành công.</summary>
    [HttpDelete("items/{variantId}"), ProducesResponseType<CartDto>(200)]
    public ActionResult<CartDto> Remove([Range(1, int.MaxValue)] int variantId)
    {
        shop.SetQty(CustomerId, variantId, 0);
        return Get();
    }

    /// <summary>Xóa toàn bộ giỏ của tài khoản đang đăng nhập.</summary>
    [HttpDelete, ProducesResponseType(204)]
    public IActionResult Clear() { shop.ClearCart(CustomerId); return NoContent(); }
}
