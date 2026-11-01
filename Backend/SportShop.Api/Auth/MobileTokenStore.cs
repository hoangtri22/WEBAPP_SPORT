using System.Security.Cryptography;
using System.Text;
using Dapper;
using SportShop.Contracts.Customer;
using SportShop.Infrastructure;

namespace SportShop.Api.Auth;

/// <summary>Persists only token digests. Rotation and replay revocation are atomic across API processes.</summary>
public class MobileTokenStore(MySqlConnectionFactory connections, TimeProvider clock)
{
    const int AccessSeconds = 900;
    static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public virtual async Task<TokenResponse?> IssueAsync(int customerId, CancellationToken cancellationToken)
    {
        await using var c = await connections.OpenAsync(cancellationToken);
        var password = await c.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
            SELECT tk.mat_khau FROM KHACH_HANG kh JOIN TAI_KHOAN tk ON tk.ma_tai_khoan=kh.ma_tai_khoan
            JOIN VAI_TRO vt ON vt.ma_vai_tro=tk.ma_vai_tro
            WHERE kh.ma_khach_hang=@customerId AND kh.da_xoa=0 AND tk.trang_thai='hoat_dong' AND vt.ten_vai_tro='Khách hàng'
            """, new { customerId }, cancellationToken: cancellationToken));
        if (password is null) return null;
        var response = Tokens(customerId);
        await c.ExecuteAsync(new CommandDefinition("""
            INSERT INTO MOBILE_AUTH_SESSION(customer_id,access_hash,refresh_hash,password_fingerprint,access_expires_utc,refresh_expires_utc,created_utc)
            VALUES(@customerId,@access,@refresh,@fingerprint,@accessExpires,@refreshExpires,@now)
            """, new { customerId, access = Digest(response.AccessToken), refresh = Digest(response.RefreshToken), fingerprint = Digest(password),
            accessExpires = response.AccessTokenExpiresAt.UtcDateTime, refreshExpires = response.RefreshTokenExpiresAt.UtcDateTime,
            now = clock.GetUtcNow().UtcDateTime }, cancellationToken: cancellationToken));
        return response;
    }

    public virtual async Task<TokenIdentity?> ValidateAsync(string accessToken, CancellationToken cancellationToken)
    {
        if (accessToken.Length != 64) return null;
        await using var c = await connections.OpenAsync(cancellationToken);
        var row = await c.QuerySingleOrDefaultAsync<TokenRow>(new CommandDefinition("""
            SELECT s.id AS SessionId,s.customer_id AS CustomerId,s.password_fingerprint AS PasswordFingerprint,tk.mat_khau AS Password
            FROM MOBILE_AUTH_SESSION s JOIN KHACH_HANG kh ON kh.ma_khach_hang=s.customer_id
            JOIN TAI_KHOAN tk ON tk.ma_tai_khoan=kh.ma_tai_khoan JOIN VAI_TRO vt ON vt.ma_vai_tro=tk.ma_vai_tro
            WHERE s.access_hash=@hash AND s.revoked=0 AND s.access_expires_utc>@now
              AND s.refresh_expires_utc>@now AND kh.da_xoa=0 AND tk.trang_thai='hoat_dong' AND vt.ten_vai_tro='Khách hàng'
            """, new { hash = Digest(accessToken), now = clock.GetUtcNow().UtcDateTime }, cancellationToken: cancellationToken));
        return row is not null && Digest(row.Password) == row.PasswordFingerprint
            ? new(row.CustomerId, row.SessionId) : null;
    }

    public virtual async Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (refreshToken.Length != 64) return null;
        await using var c = await connections.OpenAsync(cancellationToken);
        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        var hash = Digest(refreshToken);
        var row = await c.QuerySingleOrDefaultAsync<TokenRow>(new CommandDefinition("""
            SELECT s.id AS SessionId,s.customer_id AS CustomerId,s.password_fingerprint AS PasswordFingerprint,tk.mat_khau AS Password,
                   s.revoked AS Revoked,s.refresh_expires_utc AS RefreshExpires,kh.da_xoa AS Deleted,tk.trang_thai AS AccountStatus,vt.ten_vai_tro AS Role
            FROM MOBILE_AUTH_SESSION s JOIN KHACH_HANG kh ON kh.ma_khach_hang=s.customer_id
            JOIN TAI_KHOAN tk ON tk.ma_tai_khoan=kh.ma_tai_khoan JOIN VAI_TRO vt ON vt.ma_vai_tro=tk.ma_vai_tro
            WHERE s.refresh_hash=@hash FOR UPDATE
            """, new { hash }, tx, cancellationToken: cancellationToken));
        if (row is null)
        {
            await c.ExecuteAsync(new CommandDefinition("""
                UPDATE MOBILE_AUTH_SESSION s JOIN MOBILE_AUTH_REFRESH_USED u ON u.session_id=s.id SET s.revoked=1 WHERE u.refresh_hash=@hash
                """, new { hash }, tx, cancellationToken: cancellationToken));
            await tx.CommitAsync(cancellationToken);
            return null;
        }
        if (row.Revoked || row.Deleted || row.AccountStatus != "hoat_dong" || row.Role != "Khách hàng" ||
            row.RefreshExpires <= clock.GetUtcNow().UtcDateTime || Digest(row.Password) != row.PasswordFingerprint)
            return null;
        var absoluteExpiry = new DateTimeOffset(DateTime.SpecifyKind(row.RefreshExpires, DateTimeKind.Utc));
        var response = Tokens(row.CustomerId) with { RefreshTokenExpiresAt = absoluteExpiry };
        if (response.AccessTokenExpiresAt > absoluteExpiry)
            response = response with { AccessTokenExpiresAt = absoluteExpiry, ExpiresIn = (int)Math.Max(0, (absoluteExpiry - clock.GetUtcNow()).TotalSeconds) };
        await c.ExecuteAsync(new CommandDefinition("INSERT INTO MOBILE_AUTH_REFRESH_USED(refresh_hash,session_id) VALUES(@hash,@id)",
            new { hash, id = row.SessionId }, tx, cancellationToken: cancellationToken));
        await c.ExecuteAsync(new CommandDefinition("""
            UPDATE MOBILE_AUTH_SESSION SET access_hash=@access,refresh_hash=@refresh,access_expires_utc=@expires WHERE id=@id
            """, new { access = Digest(response.AccessToken), refresh = Digest(response.RefreshToken), expires = response.AccessTokenExpiresAt.UtcDateTime, id = row.SessionId }, tx, cancellationToken: cancellationToken));
        await tx.CommitAsync(cancellationToken);
        return response;
    }

    public virtual async Task RevokeAsync(long sessionId, int customerId, CancellationToken cancellationToken)
    {
        await using var c = await connections.OpenAsync(cancellationToken);
        await c.ExecuteAsync(new CommandDefinition("UPDATE MOBILE_AUTH_SESSION SET revoked=1 WHERE id=@sessionId AND customer_id=@customerId",
            new { sessionId, customerId }, cancellationToken: cancellationToken));
    }

    TokenResponse Tokens(int customerId)
    {
        var now = clock.GetUtcNow();
        return new("Bearer", NewToken(), NewToken(), AccessSeconds, now.AddSeconds(AccessSeconds), now.AddDays(30), customerId);
    }

    sealed class TokenRow
    {
        public long SessionId { get; set; }
        public int CustomerId { get; set; }
        public string PasswordFingerprint { get; set; } = "";
        public string Password { get; set; } = "";
        public bool Revoked { get; set; }
        public bool Deleted { get; set; }
        public DateTime RefreshExpires { get; set; }
        public string AccountStatus { get; set; } = "";
        public string Role { get; set; } = "";
    }
}

public sealed record TokenIdentity(int CustomerId, long SessionId);
