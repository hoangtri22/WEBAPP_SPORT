using Microsoft.AspNetCore.Mvc;
using SportShop.Application.Catalog;
using SportShop.Contracts.Catalog;

namespace SportShop.Api.Controllers;

[ApiController, Route("api/v1")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
public sealed class CatalogController(ICatalogReader reader, ICatalogClock clock) : ControllerBase
{
    /// <summary>Danh mục và số sản phẩm đang bán còn tồn kho.</summary>
    [HttpGet("categories")]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> Categories(CancellationToken cancellationToken)
        => Ok(await reader.CategoriesAsync(cancellationToken));

    /// <summary>Voucher đang hiệu lực và còn lượt; chưa xác nhận điều kiện riêng của khách.</summary>
    [HttpGet("promotions")]
    [ProducesResponseType<IReadOnlyList<PromotionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PromotionDto>>> Promotions(CancellationToken cancellationToken)
    {
        var promotions = await reader.PromotionsAsync(cancellationToken);
        return Ok(promotions.Select(promotion => new PromotionDto(
            promotion.PromotionId, promotion.Code, promotion.Name, promotion.Percent,
            promotion.MaxDiscount, promotion.RequiredTier, promotion.PerCustomerLimit,
            clock.WithOffset(promotion.StartsAt), clock.WithOffset(promotion.EndsAt))).ToArray());
    }

    /// <summary>Danh sách sở thích thể thao dùng cho hồ sơ khách hàng.</summary>
    [HttpGet("interests")]
    [ProducesResponseType<IReadOnlyList<InterestDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InterestDto>>> Interests(CancellationToken cancellationToken)
        => Ok(await reader.InterestsAsync(cancellationToken));

    /// <summary>Các phương thức thanh toán đang hoạt động, cùng lựa chọn với Web User (thanh toán mô phỏng).</summary>
    [HttpGet("payment-methods")]
    [ProducesResponseType<IReadOnlyList<PaymentMethodDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodDto>>> PaymentMethods(CancellationToken cancellationToken)
        => Ok(await reader.PaymentMethodsAsync(cancellationToken));
}
