using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SportShop.Api.Errors;

namespace SportShop.Api.Auth;

public sealed class BearerAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, MobileTokenStore tokens, GuestDeviceStore guests) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string GuestDeviceHeader = "X-Guest-Device-Key";

    // Đọc header Authorization, kiểm tra qua MobileTokenStore.ValidateAsync() rồi tạo claim mã khách, vai trò, session_id.
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(header))
        {
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.Fail("Bearer token required.");
            var identity = await tokens.ValidateAsync(header[7..].Trim(), Context.RequestAborted);
            if (identity is null) return AuthenticateResult.Fail("Token invalid, revoked or expired.");
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, identity.CustomerId.ToString()),
                new Claim(ClaimTypes.Role, "Customer"), new Claim("session_id", identity.SessionId.ToString()) };
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
        }

        var guestKey = Request.Headers[GuestDeviceHeader].ToString().Trim();
        if (!string.IsNullOrEmpty(guestKey) && IsGuestCommercePath(Request.Path))
        {
            var guestCustomerId = await guests.ResolveOrCreateGuestCustomerAsync(guestKey, Context.RequestAborted);
            if (guestCustomerId is null)
                return AuthenticateResult.Fail("Invalid guest device key.");
            var guestClaims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, guestCustomerId.Value.ToString()),
                new Claim(ClaimTypes.Role, "Customer"),
                new Claim("is_guest", "true"),
                new Claim("device_key", guestKey)
            };
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(guestClaims, Scheme.Name)), Scheme.Name));
        }

        return AuthenticateResult.NoResult();
    }

    private static bool IsGuestCommercePath(PathString path) =>
        path.StartsWithSegments("/api/v1/cart", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/v1/checkout", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/v1/orders", StringComparison.OrdinalIgnoreCase);

    // Trả lỗi 401 dạng ProblemDetails qua ApiProblems khi thiếu hoặc sai token.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return ApiProblems.WriteAsync(Context, ApiProblems.Create(Context, 401, "UNAUTHENTICATED", "Bạn cần đăng nhập hoặc làm mới phiên đăng nhập."));
    }

    // Trả lỗi 403 dạng ProblemDetails qua ApiProblems khi sai vai trò.
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        => ApiProblems.WriteAsync(Context, ApiProblems.Create(Context, 403, "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này."));
}
