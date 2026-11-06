using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dapper;
using SportShop.CustomerWeb.Services;
using SportShop.Infrastructure;

namespace SportShop.Api.Auth;

public sealed record GoogleUser(string Sub, string Email, bool EmailVerified, string? GivenName, string? FamilyName, string? Name);

public interface IGoogleOAuthClient
{
    bool IsConfigured { get; }
    string AuthorizeUrl(string redirectUri, string state);
    Task<GoogleUser?> ExchangeAsync(string code, string redirectUri, CancellationToken cancellationToken);
}

/// <summary>Đổi mã của Google lấy thông tin người dùng, theo mục Google trong appsettings.Local.json.</summary>
public sealed class GoogleOAuthClient(HttpClient http, IConfiguration configuration) : IGoogleOAuthClient
{
    string ClientId => configuration["Google:ClientId"] ?? "";
    string ClientSecret => configuration["Google:ClientSecret"] ?? "";

    public bool IsConfigured => ClientId.Length > 0 && ClientSecret.Length > 0;

    public string AuthorizeUrl(string redirectUri, string state)
        => "https://accounts.google.com/o/oauth2/v2/auth?response_type=code&scope=openid%20email%20profile&prompt=select_account" +
           $"&client_id={Uri.EscapeDataString(ClientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={Uri.EscapeDataString(state)}";

    public async Task<GoogleUser?> ExchangeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        using var tokenResponse = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["redirect_uri"] = redirectUri
        }), cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode) return null;
        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenBody>(cancellationToken);
        if (string.IsNullOrEmpty(token?.AccessToken)) return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var userResponse = await http.SendAsync(request, cancellationToken);
        if (!userResponse.IsSuccessStatusCode) return null;
        var user = await userResponse.Content.ReadFromJsonAsync<UserBody>(cancellationToken);
        return string.IsNullOrEmpty(user?.Sub) || string.IsNullOrEmpty(user.Email) ? null
            : new GoogleUser(user.Sub, user.Email, user.EmailVerified, user.GivenName, user.FamilyName, user.Name);
    }

    sealed record TokenBody([property: JsonPropertyName("access_token")] string? AccessToken);

    sealed record UserBody(
        [property: JsonPropertyName("sub")] string? Sub,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_verified")] bool EmailVerified,
        [property: JsonPropertyName("given_name")] string? GivenName,
        [property: JsonPropertyName("family_name")] string? FamilyName,
        [property: JsonPropertyName("name")] string? Name);
}

/// <summary>
/// State gửi cho Google và vé một lần trả về app, giữ trong bộ nhớ máy chủ như LoginThrottle.
/// Vé gắn với code_challenge của app (PKCE): app khác bắt được link sportshop:// cũng không đổi được token.
/// </summary>
public sealed class GoogleLoginTickets(TimeProvider clock)
{
    static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(2);
    readonly ConcurrentDictionary<string, (string Challenge, DateTimeOffset Expires)> _states = new();
    readonly ConcurrentDictionary<string, (int CustomerId, string Challenge, DateTimeOffset Expires)> _tickets = new();

    static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Challenge(string verifier)
        => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public string NewState(string challenge)
    {
        var now = clock.GetUtcNow();
        foreach (var (key, value) in _states) if (value.Expires <= now) _states.TryRemove(key, out _);
        foreach (var (key, value) in _tickets) if (value.Expires <= now) _tickets.TryRemove(key, out _);
        var state = NewKey();
        _states[state] = (challenge, now + StateLifetime);
        return state;
    }

    public string? TakeState(string state)
        => _states.TryRemove(state, out var entry) && entry.Expires > clock.GetUtcNow() ? entry.Challenge : null;

    public string NewTicket(int customerId, string challenge)
    {
        var ticket = NewKey();
        _tickets[ticket] = (customerId, challenge, clock.GetUtcNow() + TicketLifetime);
        return ticket;
    }

    // Vé chỉ dùng được một lần, kể cả khi verifier sai.
    public int? TakeTicket(string ticket, string verifier)
    {
        if (!_tickets.TryRemove(ticket, out var entry) || entry.Expires <= clock.GetUtcNow()) return null;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Challenge(verifier)), Encoding.ASCII.GetBytes(entry.Challenge))
            ? entry.CustomerId : null;
    }
}

/// <summary>Tìm khách theo liên kết Google, rồi theo email đã xác minh; chưa có thì tạo tài khoản mới.</summary>
public class GoogleAccountStore(MySqlConnectionFactory connections, TimeProvider clock)
{
    public virtual async Task<int?> FindOrCreateAsync(GoogleUser user, CancellationToken cancellationToken)
    {
        // Email chưa xác minh có thể là của người khác, không được gắn vào tài khoản sẵn có.
        if (!user.EmailVerified) return null;
        await using var c = await connections.OpenAsync(cancellationToken);
        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        var linked = await c.QuerySingleOrDefaultAsync<int?>(new CommandDefinition("""
            SELECT kh.ma_khach_hang FROM MOBILE_GOOGLE_LOGIN g JOIN KHACH_HANG kh ON kh.ma_tai_khoan=g.account_id WHERE g.google_sub=@sub
            """, new { sub = user.Sub }, tx, cancellationToken: cancellationToken));
        if (linked is not null) return linked;

        var existing = await c.QuerySingleOrDefaultAsync<ExistingRow>(new CommandDefinition("""
            SELECT tk.ma_tai_khoan AS AccountId, kh.ma_khach_hang AS CustomerId, vt.ten_vai_tro AS Role
            FROM TAI_KHOAN tk JOIN VAI_TRO vt ON vt.ma_vai_tro=tk.ma_vai_tro
            LEFT JOIN KHACH_HANG kh ON kh.ma_tai_khoan=tk.ma_tai_khoan WHERE tk.email=@email
            """, new { email = user.Email }, tx, cancellationToken: cancellationToken));
        // Email của nhân viên hoặc quản trị không được đăng nhập vào app khách.
        if (existing is not null && (existing.Role != "Khách hàng" || existing.CustomerId is null)) return null;

        var accountId = existing?.AccountId ?? await CreateAccountAsync(c, tx, user, cancellationToken);
        await c.ExecuteAsync(new CommandDefinition("INSERT INTO MOBILE_GOOGLE_LOGIN(google_sub,account_id,created_utc) VALUES(@sub,@accountId,@now)",
            new { sub = user.Sub, accountId, now = clock.GetUtcNow().UtcDateTime }, tx, cancellationToken: cancellationToken));
        var customerId = existing?.CustomerId ?? await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT ma_khach_hang FROM KHACH_HANG WHERE ma_tai_khoan=@accountId", new { accountId }, tx, cancellationToken: cancellationToken));
        await tx.CommitAsync(cancellationToken);
        return customerId;
    }

    // Tài khoản mới không có mật khẩu dùng được; khách muốn đăng nhập thường thì dùng Quên mật khẩu.
    static async Task<int> CreateAccountAsync(System.Data.Common.DbConnection c, System.Data.Common.DbTransaction tx, GoogleUser user, CancellationToken cancellationToken)
    {
        var (lastName, firstName) = SplitName(user);
        var accountId = await c.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TAI_KHOAN (ten_dang_nhap, email, mat_khau, ma_vai_tro, trang_thai)
            VALUES (@username, @email, @password, (SELECT ma_vai_tro FROM VAI_TRO WHERE ten_vai_tro='Khách hàng'), 'hoat_dong');
            SELECT LAST_INSERT_ID();
            """, new { username = NewUsername(user.Email), email = user.Email,
                password = PasswordStorage.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))) }, tx, cancellationToken: cancellationToken));
        await c.ExecuteAsync(new CommandDefinition("INSERT INTO KHACH_HANG (ma_tai_khoan, ho_lot, ten) VALUES (@accountId, @lastName, @firstName)",
            new { accountId, lastName, firstName }, tx, cancellationToken: cancellationToken));
        return accountId;
    }

    public static string NewUsername(string email)
    {
        var name = Regex.Replace(email.Split('@')[0], "[^a-zA-Z0-9_]", "");
        return $"gg_{name[..Math.Min(name.Length, 18)]}_{Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant()}";
    }

    public static (string LastName, string FirstName) SplitName(GoogleUser user)
    {
        var first = user.GivenName?.Trim();
        var last = user.FamilyName?.Trim();
        if (string.IsNullOrEmpty(first))
        {
            var words = (user.Name ?? user.Email.Split('@')[0]).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            first = words.Length == 0 ? "Khách" : words[^1];
            last = string.Join(' ', words.SkipLast(1));
        }
        return ((last ?? "")[..Math.Min((last ?? "").Length, 70)], first[..Math.Min(first.Length, 30)]);
    }

    sealed class ExistingRow
    {
        public int AccountId { get; set; }
        public int? CustomerId { get; set; }
        public string Role { get; set; } = "";
    }
}
