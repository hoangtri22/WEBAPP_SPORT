using Microsoft.AspNetCore.Mvc;
using SportShop.Application.Health;

namespace SportShop.Api.Controllers;

public sealed record HealthDto(string Status);

[ApiController, Route("health")]
public sealed class HealthController(IDatabaseReadiness database) : ControllerBase
{
    /// <summary>Kiểm tra API host; không cần kết nối MySQL.</summary>
    [HttpGet("live")]
    [ProducesResponseType<HealthDto>(StatusCodes.Status200OK)]
    public ActionResult<HealthDto> Live() => Ok(new HealthDto("healthy"));

    /// <summary>Kiểm tra kết nối và schema API khách hàng, gồm auth/cart migrations, bằng truy vấn chỉ đọc.</summary>
    [HttpGet("ready")]
    [ProducesResponseType<HealthDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthDto>> Ready(CancellationToken cancellationToken)
    {
        await database.CheckAsync(cancellationToken);
        return Ok(new HealthDto("ready"));
    }
}
