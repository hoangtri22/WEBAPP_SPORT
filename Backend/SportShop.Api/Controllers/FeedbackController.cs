using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportShop.Api.Errors;
using SportShop.Application.Catalog;
using SportShop.Contracts.Common;
using SportShop.Contracts.Customer;
using SportShop.CustomerWeb.Services;

namespace SportShop.Api.Controllers;

[ApiController, Authorize(Roles = "Customer"), Route("api/v1/me/feedback")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
public sealed class FeedbackController(CustomerStore customers, ICatalogClock clock) : ControllerBase
{
    int CustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Lists the customer's feedback and latest store replies.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<FeedbackResponse>>(200)]
    public IActionResult List([Range(1, int.MaxValue)] int page = 1, [Range(1, 100)] int pageSize = 20)
    {
        var all = customers.MobileFeedbacks(CustomerId);
        var items = all.Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize)
            .Select(f => new FeedbackResponse(f.Id, f.ProductId, f.ProductName, f.Stars, f.Content, clock.WithOffset(f.SentAt), f.Status, f.Reply)).ToList();
        return Ok(new PagedResponse<FeedbackResponse>(items, page, pageSize, all.Count));
    }

    /// <summary>Lists products eligible for review (purchased in a delivered order).</summary>
    [HttpGet("eligible-products")]
    [ProducesResponseType<IEnumerable<EligibleProductResponse>>(200)]
    public IActionResult EligibleProducts() => Ok(customers.PurchasedProducts(CustomerId).Select(p => new EligibleProductResponse(p.Id, p.Name)));

    /// <summary>Reviews a received product. Optional orderId must belong to the authenticated customer.</summary>
    [HttpPost]
    [ProducesResponseType<FeedbackCreatedResponse>(201)]
    public IActionResult Create(FeedbackRequest request)
    {
        var error = customers.AddPurchasedFeedback(CustomerId, request.ProductId, request.Stars, request.Content, request.OrderId);
        return error is null ? StatusCode(201, new FeedbackCreatedResponse("Đã gửi phản hồi."))
            : StatusCode(400, ApiProblems.Create(HttpContext, 400, "FEEDBACK_NOT_ALLOWED", error));
    }
}
