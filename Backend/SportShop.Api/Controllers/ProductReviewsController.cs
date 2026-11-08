using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SportShop.Api.Errors;
using SportShop.Contracts.Catalog;
using SportShop.CustomerWeb.Services;

namespace SportShop.Api.Controllers;

// Đánh giá sản phẩm công khai, chỉ đọc các phản hồi được cửa hàng cho hiển thị.
[ApiController, Route("api/v1/products/{productId}/reviews")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(503)]
public sealed class ProductReviewsController(ProductReviewStore reviews) : ControllerBase
{
    /// <summary>Đánh giá công khai mới nhất, kèm tổng số và sao trung bình; tên người đánh giá được ẩn danh.</summary>
    [HttpGet]
    [ProducesResponseType<ProductReviewsResponse>(200)]
    public async Task<ActionResult<ProductReviewsResponse>> List(
        [Range(1, int.MaxValue)] int productId,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await reviews.ListAsync(productId, page, pageSize, cancellationToken);
        return result is null
            ? NotFound(ApiProblems.Create(HttpContext, 404, "RESOURCE_NOT_FOUND", "Không tìm thấy sản phẩm."))
            : Ok(result);
    }
}
