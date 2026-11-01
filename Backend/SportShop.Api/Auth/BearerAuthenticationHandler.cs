using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SportShop.Api.Errors;

namespace SportShop.Api.Auth;

public sealed class BearerAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, MobileTokenStore tokens) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header)) return AuthenticateResult.NoResult();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.Fail("Bearer token required.");
        var identity = await tokens.ValidateAsync(header[7..].Trim(), Context.RequestAborted);
        if (identity is null) return AuthenticateResult.Fail("Token invalid, revoked or expired.");
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, identity.CustomerId.ToString()),
            new Claim(ClaimTypes.Role, "Customer"), new Claim("session_id", identity.SessionId.ToString()) };
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return ApiProblems.WriteAsync(Context, ApiProblems.Create(Context, 401, "UNAUTHENTICATED", "Bạn cần đăng nhập hoặc làm mới phiên đăng nhập."));
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        => ApiProblems.WriteAsync(Context, ApiProblems.Create(Context, 403, "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này."));
}
