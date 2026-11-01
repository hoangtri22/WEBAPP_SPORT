using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using SportShop.Api.Errors;
using SportShop.Contracts.Customer;
using SportShop.CustomerWeb.Services;

namespace SportShop.Api.Controllers;

[ApiController, Authorize(Roles = "Customer"), Route("api/v1/me")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
public sealed class MeController(CustomerStore customers, LoginThrottle throttle) : ControllerBase
{
    int CustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Returns the current profile, membership, points and selected sports interests.</summary>
    [HttpGet]
    [ProducesResponseType<CustomerMeResponse>(200)]
    public IActionResult Get()
    {
        var profile = customers.GetProfile(CustomerId);
        if (profile is null) return Error(404, "RESOURCE_NOT_FOUND", "Không tìm thấy khách hàng.");
        var interests = customers.Interests(CustomerId);
        return Ok(new CustomerMeResponse(CustomerId, new CustomerProfileResponse(profile.FullName, profile.Initials, profile.Username,
            profile.Email, profile.Phone, profile.Birth, profile.Gender, profile.Tier, profile.Orders, profile.Points), interests.Selected));
    }

    /// <summary>Replaces profile fields and selected interests. Interest names come from GET /interests.</summary>
    [HttpPut]
    [ProducesResponseType<CustomerMeResponse>(200), ProducesResponseType<ProblemDetails>(409)]
    public IActionResult Update(UpdateProfileRequest request)
    {
        if (request.Birth > DateTime.Today || request.Interests.Any(string.IsNullOrWhiteSpace))
            return Error(400, "VALIDATION_ERROR", "Ngày sinh hoặc sở thích không hợp lệ.");
        string? error;
        try { error = customers.UpdateProfile(CustomerId, request.FullName.Trim(), request.Email.Trim(), request.Phone.Trim(), request.Birth?.Date, request.Gender, request.Interests); }
        catch (MySqlException e) when (e.Number == 1062) { return Error(409, "DUPLICATE_EMAIL", "Email đã được sử dụng."); }
        return error is null ? Get() : Error(error.Contains("Email") ? 409 : 400, "PROFILE_UPDATE_FAILED", error);
    }

    /// <summary>Changes the password. Every existing mobile session is invalidated, including this session.</summary>
    [HttpPost("password")]
    [ProducesResponseType(204)]
    public IActionResult Password(ChangePasswordRequest request)
    {
        if (request.NewPassword == request.CurrentPassword) return Error(400, "VALIDATION_ERROR", "Mật khẩu mới phải khác mật khẩu hiện tại.");
        var key = "pw:" + CustomerId;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var minutes = throttle.MinutesLeft(key, ip);
        if (minutes > 0)
        {
            Response.Headers.RetryAfter = (minutes * 60).ToString();
            return Error(429, "LOGIN_THROTTLED", $"Vui lòng thử lại sau {minutes} phút.");
        }
        var error = customers.ChangePassword(CustomerId, request.CurrentPassword, request.NewPassword);
        if (error is not null) { throttle.Fail(key, ip); return Error(400, "PASSWORD_CHANGE_FAILED", error); }
        throttle.Reset(key, ip);
        return NoContent();
    }

    IActionResult Error(int status, string code, string title) => StatusCode(status, ApiProblems.Create(HttpContext, status, code, title));
}
