using System.Security.Cryptography;
using System.Text;
using Dapper;
using SportShop.CustomerWeb.Services;
using SportShop.Infrastructure;

namespace SportShop.Api.Auth;

/// <summary>Mã OTP quên mật khẩu trong MOBILE_PASSWORD_RESET: chỉ lưu hash, sống 10 phút, sai 5 lần thì mất hiệu lực.</summary>
public class PasswordResetStore(MySqlConnectionFactory connections, TimeProvider clock)
{
    const int MaxAttempts = 5;
    static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    static readonly TimeSpan ResendAfter = TimeSpan.FromSeconds(60);
    const string CustomerByEmail = """
        SELECT tk.ma_tai_khoan AS AccountId, tk.email AS Email
        FROM TAI_KHOAN tk JOIN KHACH_HANG kh ON kh.ma_tai_khoan=tk.ma_tai_khoan JOIN VAI_TRO vt ON vt.ma_vai_tro=tk.ma_vai_tro
        WHERE tk.email=@email AND kh.da_xoa=0 AND tk.trang_thai='hoat_dong' AND vt.ten_vai_tro='Khách hàng'
        """;

    static string Digest(int accountId, string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{accountId}:{code}")));

    // Tạo mã mới cho khách có email này; trả null nếu không có tài khoản hoặc vừa gửi trong 60 giây (chống gửi mail hàng loạt).
    public virtual async Task<PasswordResetCode?> CreateAsync(string email, CancellationToken cancellationToken)
    {
        await using var c = await connections.OpenAsync(cancellationToken);
        var account = await c.QuerySingleOrDefaultAsync<AccountRow>(new CommandDefinition(CustomerByEmail, new { email }, cancellationToken: cancellationToken));
        if (account is null) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
        var lastSent = await c.QuerySingleOrDefaultAsync<DateTime?>(new CommandDefinition("SELECT sent_utc FROM MOBILE_PASSWORD_RESET WHERE account_id=@id",
            new { id = account.AccountId }, cancellationToken: cancellationToken));
        if (lastSent > now - ResendAfter) return null;
        await c.ExecuteAsync(new CommandDefinition("""
            INSERT INTO MOBILE_PASSWORD_RESET(account_id,code_hash,expires_utc,sent_utc,attempts)
            VALUES(@id,@hash,@expires,@now,0)
            ON DUPLICATE KEY UPDATE code_hash=VALUES(code_hash),expires_utc=VALUES(expires_utc),sent_utc=VALUES(sent_utc),attempts=0
            """, new { id = account.AccountId, hash = Digest(account.AccountId, code), expires = now + Lifetime, now },
            cancellationToken: cancellationToken));
        return new PasswordResetCode(account.Email, code);
    }

    // Mã đúng thì đổi mật khẩu và xóa mã. Đổi mật khẩu làm lệch password_fingerprint nên mọi phiên mobile hết hiệu lực.
    public virtual async Task<bool> ResetAsync(string email, string code, string newPassword, CancellationToken cancellationToken)
    {
        await using var c = await connections.OpenAsync(cancellationToken);
        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        var account = await c.QuerySingleOrDefaultAsync<AccountRow>(new CommandDefinition(CustomerByEmail, new { email }, tx, cancellationToken: cancellationToken));
        if (account is null) return false;
        var row = await c.QuerySingleOrDefaultAsync<ResetRow>(new CommandDefinition("""
            SELECT code_hash AS CodeHash, expires_utc AS Expires, attempts AS Attempts
            FROM MOBILE_PASSWORD_RESET WHERE account_id=@id FOR UPDATE
            """, new { id = account.AccountId }, tx, cancellationToken: cancellationToken));
        if (row is null || row.Attempts >= MaxAttempts || row.Expires <= clock.GetUtcNow().UtcDateTime) return false;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(row.CodeHash), Encoding.ASCII.GetBytes(Digest(account.AccountId, code))))
        {
            await c.ExecuteAsync(new CommandDefinition("UPDATE MOBILE_PASSWORD_RESET SET attempts=attempts+1 WHERE account_id=@id",
                new { id = account.AccountId }, tx, cancellationToken: cancellationToken));
            await tx.CommitAsync(cancellationToken);
            return false;
        }
        await c.ExecuteAsync(new CommandDefinition("UPDATE TAI_KHOAN SET mat_khau=@password WHERE ma_tai_khoan=@id",
            new { password = PasswordStorage.Hash(newPassword), id = account.AccountId }, tx, cancellationToken: cancellationToken));
        await c.ExecuteAsync(new CommandDefinition("DELETE FROM MOBILE_PASSWORD_RESET WHERE account_id=@id",
            new { id = account.AccountId }, tx, cancellationToken: cancellationToken));
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    sealed class AccountRow
    {
        public int AccountId { get; set; }
        public string Email { get; set; } = "";
    }

    sealed class ResetRow
    {
        public string CodeHash { get; set; } = "";
        public DateTime Expires { get; set; }
        public int Attempts { get; set; }
    }
}

public sealed record PasswordResetCode(string Email, string Code);
