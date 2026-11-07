using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SportShop.Api.Auth;
using SportShop.Api.Errors;
using SportShop.Contracts.Customer;

namespace SportShop.Api.Controllers;

/// <summary>Đăng nhập bằng Google: app mở start trong trình duyệt, Google quay về callback, API trả vé một lần qua sportshop://auth.</summary>
[ApiController]
[Route("api/v1/auth/google")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GoogleAuthController(IGoogleOAuthClient google, GoogleLoginTickets tickets, GoogleAccountStore accounts,
    MobileTokenStore tokens, ILogger<GoogleAuthController> logger) : ControllerBase
{
    const string AppCallback = "sportshop://auth";

    string RedirectUri => $"{Request.Scheme}://{Request.Host}/api/v1/auth/google/callback";

    // Kiểm tra challenge rồi chuyển sang trang đăng nhập Google qua IGoogleOAuthClient.AuthorizeUrl().
    [HttpGet("start")]
    [ProducesResponseType(302), ProducesResponseType<ProblemDetails>(400)]
    public IActionResult Start([FromQuery] string challenge)
    {
        if (!Regex.IsMatch(challenge, "^[A-Za-z0-9_-]{43}$")) return Error(400, "VALIDATION_ERROR", "Yêu cầu đăng nhập không hợp lệ.");
        return Redirect(google.IsConfigured ? google.AuthorizeUrl(RedirectUri, tickets.NewState(challenge)) : ToApp("error", "not_configured"));
    }

    // Nhận mã từ Google, đổi lấy GoogleUser, tìm hoặc tạo khách rồi chuyển về app kèm ticket hoặc lỗi.
    [HttpGet("callback")]
    [ProducesResponseType(302)]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, CancellationToken cancellationToken)
    {
        var challenge = state is null ? null : tickets.TakeState(state);
        // Khách bấm hủy ở trang Google cũng về đây: có state nhưng không có code.
        if (challenge is null || string.IsNullOrEmpty(code)) return Redirect(ToApp("error", "cancelled"));
        try
        {
            var user = await google.ExchangeAsync(code, RedirectUri, cancellationToken);
            var customerId = user is null ? null : await accounts.FindOrCreateAsync(user, cancellationToken);
            return Redirect(customerId is null ? ToApp("error", "rejected") : ToApp("ticket", tickets.NewTicket(customerId.Value, challenge)));
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Không kết nối được Google.");
            return Redirect(ToApp("error", "unavailable"));
        }
    }

    /// <summary>Đổi vé một lần lấy token; cần đúng verifier của app đã mở start.</summary>
    [HttpPost("exchange")]
    [ProducesResponseType<TokenResponse>(200), ProducesResponseType<ProblemDetails>(401)]
    public async Task<IActionResult> Exchange(GoogleExchangeRequest request, CancellationToken cancellationToken)
    {
        var customerId = tickets.TakeTicket(request.Ticket, request.Verifier);
        var result = customerId is null ? null : await tokens.IssueAsync(customerId.Value, cancellationToken);
        return result is null ? Error(401, "INVALID_GOOGLE_TICKET", "Tài khoản không đăng nhập được. Vui lòng thử lại.") : Ok(result);
    }

    // Tạo đường dẫn quay về app kèm một tham số.
    static string ToApp(string key, string value) => $"{AppCallback}?{key}={Uri.EscapeDataString(value)}";

    // Trả lỗi dạng ProblemDetails qua ApiProblems.Create().
    IActionResult Error(int status, string code, string title) => StatusCode(status, ApiProblems.Create(HttpContext, status, code, title));
}
