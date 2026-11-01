using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SportShop.Api.Catalog;
using SportShop.Api.Errors;
using SportShop.Api.Media;
using SportShop.Application.Catalog;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;

namespace SportShop.Api.Controllers;

[ApiController, Route("api/v1/products")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
public sealed class ProductsController(ICatalogReader reader, MediaUrlResolver media) : ControllerBase
{
    /// <summary>Tìm sản phẩm đang bán, có lọc, sắp xếp và phân trang tại CSDL.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<ProductSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ProductSummaryDto>>> List(
        [FromQuery] ProductSearchQuery query, CancellationToken cancellationToken)
    {
        var page = await reader.ProductsAsync(query.ToSearch(), cancellationToken);
        return Ok(new PagedResponse<ProductSummaryDto>(
            page.Items.Select(item => CatalogMapping.Summary(item, media)).ToArray(),
            page.Page, page.PageSize, page.TotalItems));
    }

    /// <summary>Chi tiết sản phẩm và các biến thể đang hiện, gồm cả biến thể hết hàng.</summary>
    [HttpGet("{productId}")]
    [ProducesResponseType<ProductDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailDto>> Detail(
        [Range(1, int.MaxValue)] int productId, CancellationToken cancellationToken)
    {
        var detail = await reader.ProductAsync(productId, cancellationToken);
        if (detail is null)
            return NotFound(ApiProblems.Create(HttpContext, 404, "RESOURCE_NOT_FOUND", "Không tìm thấy sản phẩm."));
        return Ok(CatalogMapping.Detail(detail, media));
    }
}
