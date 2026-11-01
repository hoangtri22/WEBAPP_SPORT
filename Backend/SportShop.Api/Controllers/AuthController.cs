using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using SportShop.Api.Auth;
using SportShop.Api.Errors;
using SportShop.Contracts.Customer;
using SportShop.CustomerWeb.Services;

namespace SportShop.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(CustomerStore customers, MobileTokenStore tokens, LoginThrottle throttle) : ControllerBase
{
    /// <summary>Registers a customer. Existing web and mobile users share the same account records.</summary>
    [HttpPost("register")]
    [ProducesResponseType<TokenResponse>(201)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, string> errors;
        try { errors = customers.Register(request.Username, request.Email, request.Phone, request.Password); }
        catch (MySqlException e) when (e.Number == 1062) { return Error(409, "DUPLICATE_ACCOUNT", "Tên đăng nhập hoặc email đã được sử dụng."); }
        if (errors.Count > 0) return Error(409, "DUPLICATE_ACCOUNT", string.Join(" ", errors.Values));
        var (id, _) = customers.Login(request.Username, request.Password);
        var result = id.HasValue ? await tokens.IssueAsync(id.Value, cancellationToken) : null;
        return result is null ? Error(401, "UNAUTHENTICATED", "Tài khoản không còn hoạt động.") : StatusCode(201, result);
    }

    /// <summary>Signs in with username or email. Five failed attempts lock this username/IP pair for five minutes.</summary>
    [HttpPost("login")]
    [ProducesResponseType<TokenResponse>(200)]
    [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(429)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var minutes = throttle.MinutesLeft(request.Username, ip);
        if (minutes > 0)
        {
            Response.Headers.RetryAfter = (minutes * 60).ToString();
            return Error(429, "LOGIN_THROTTLED", $"Vui lòng thử lại sau {minutes} phút.");
        }
        var (id, _) = customers.Login(request.Username, request.Password);
        if (!id.HasValue)
        {
            throttle.Fail(request.Username, ip);
            return Error(401, "INVALID_CREDENTIALS", "Thông tin đăng nhập không đúng hoặc tài khoản không còn hoạt động.");
        }
        throttle.Reset(request.Username, ip);
        var result = await tokens.IssueAsync(id.Value, cancellationToken);
        return result is null ? Error(401, "INVALID_CREDENTIALS", "Tài khoản không còn hoạt động.") : Ok(result);
    }

    /// <summary>Rotates both tokens. A reused refresh token revokes its entire session.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<TokenResponse>(200)]
    [ProducesResponseType<ProblemDetails>(401)]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await tokens.RefreshAsync(request.RefreshToken, cancellationToken);
        return result is null ? Error(401, "INVALID_REFRESH_TOKEN", "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.") : Ok(result);
    }

    /// <summary>Revokes the current device session and its refresh token.</summary>
    [HttpPost("logout"), Authorize(Roles = "Customer")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await tokens.RevokeAsync(long.Parse(User.FindFirstValue("session_id")!), int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken);
        return NoContent();
    }

    IActionResult Error(int status, string code, string title) => StatusCode(status, ApiProblems.Create(HttpContext, status, code, title));
}
