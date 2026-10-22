using System.Collections.Concurrent;

namespace SportShop.CustomerWeb.Services;

/// <summary>
/// Chống dò mật khẩu: một tên đăng nhập (kèm địa chỉ IP) sai quá số lần cho phép thì bị khóa đăng nhập tạm thời.
/// Lưu trong bộ nhớ của ứng dụng nên tự xóa khi khởi động lại.
/// Thời gian khóa đo bằng đồng hồ đếm của hệ thống (Environment.TickCount64), nên chỉnh giờ máy lùi hay tiến
/// không làm khóa dài ra, ngắn đi hoặc mở khóa sớm.
/// </summary>
public class LoginThrottle
{
    const int MaxFailures = 5;
    static readonly long LockForMs = (long)TimeSpan.FromMinutes(5).TotalMilliseconds;
    const long NotLocked = long.MinValue;

    record Attempt(int Failures, long LockedUntilMs);
    readonly ConcurrentDictionary<string, Attempt> _attempts = new();

    static string Key(string username, string? ip) => username.Trim().ToLowerInvariant() + "|" + ip;
    static long Now => Environment.TickCount64;

    /// <summary>Đang bị khóa thì trả về số phút còn lại (làm tròn lên), ngược lại trả về 0.</summary>
    public int MinutesLeft(string username, string? ip)
    {
        if (!_attempts.TryGetValue(Key(username, ip), out var a) || a.LockedUntilMs == NotLocked || a.LockedUntilMs <= Now) return 0;
        return (int)Math.Ceiling((a.LockedUntilMs - Now) / 60000.0);
    }

    /// <summary>Ghi nhận một lần đăng nhập sai; đủ số lần thì bắt đầu khóa.</summary>
    public void Fail(string username, string? ip)
    {
        _attempts.AddOrUpdate(Key(username, ip),
            _ => new Attempt(1, NotLocked),
            (_, a) =>
            {
                // Hết thời gian khóa thì đếm lại từ đầu
                bool lockExpired = a.LockedUntilMs != NotLocked && a.LockedUntilMs <= Now;
                int failures = lockExpired ? 1 : a.Failures + 1;
                return new Attempt(failures, failures >= MaxFailures ? Now + LockForMs : NotLocked);
            });
    }

    /// <summary>Đăng nhập đúng thì xóa bộ đếm.</summary>
    public void Reset(string username, string? ip) => _attempts.TryRemove(Key(username, ip), out _);
}
