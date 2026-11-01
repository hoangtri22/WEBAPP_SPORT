using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;

namespace SportShop.Api.Controllers;

[ApiController, Route("api/v1/pages")]
public sealed class ContentController : ControllerBase
{
    /// <summary>Danh sách các trang hỗ trợ và giới thiệu dùng chung nội dung với Web User.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PageSummary>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PageSummary>> List() => Ok(InfoPages.All.Select(p =>
        new PageSummary(p.Key, p.Value.Eyebrow, p.Value.Title, p.Value.Intro)).ToArray());

    /// <summary>Nội dung trang thông tin theo slug: đổi trả, giao hàng, chọn size, liên hệ, giới thiệu, cửa hàng, tuyển dụng.</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType<InfoPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<InfoPage> Detail(string slug) => InfoPages.All.TryGetValue(slug, out var page)
        ? Ok(page) : NotFound(Errors.ApiProblems.Create(HttpContext, 404, "RESOURCE_NOT_FOUND", "Không tìm thấy trang thông tin."));
}

public sealed record PageSummary(string Slug, string Eyebrow, string Title, string Intro);
