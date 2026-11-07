using System.Collections.Concurrent;

namespace SportShop.CustomerWeb.Services;

// Giới hạn số lần đăng nhập sai trong bộ nhớ, dùng chung cho web khách và API.
public class LoginThrottle
{
    const int MaxFailures = 5;
    static readonly long LockForMs = (long)TimeSpan.FromMinutes(5).TotalMilliseconds;
    const long NotLocked = long.MinValue;

    // Số lần sai và thời điểm hết khóa của một khóa đăng nhập.
    record Attempt(int Failures, long LockedUntilMs);
    readonly ConcurrentDictionary<string, Attempt> _attempts = new();

    // Tạo khóa đếm từ tên đăng nhập và IP.
    static string Key(string username, string? ip) => username.Trim().ToLowerInvariant() + "|" + ip;
    static long Now => Environment.TickCount64;

    // Số phút còn bị khóa của cặp tên đăng nhập + IP (bộ đếm lưu trong bộ nhớ máy chủ).
    public int MinutesLeft(string username, string? ip)
    {
        if (!_attempts.TryGetValue(Key(username, ip), out var a) || a.LockedUntilMs == NotLocked || a.LockedUntilMs <= Now) return 0;
        return (int)Math.Ceiling((a.LockedUntilMs - Now) / 60000.0);
    }

    // Ghi một lần nhập sai; sai đủ MaxFailures (5) lần thì khóa 5 phút.
    public void Fail(string username, string? ip)
    {
        _attempts.AddOrUpdate(Key(username, ip),
            _ => new Attempt(1, NotLocked),
            (_, a) =>
            {
                bool lockExpired = a.LockedUntilMs != NotLocked && a.LockedUntilMs <= Now;
                int failures = lockExpired ? 1 : a.Failures + 1;
                return new Attempt(failures, failures >= MaxFailures ? Now + LockForMs : NotLocked);
            });
    }

    // Xóa bộ đếm khi nhập đúng.
    public void Reset(string username, string? ip) => _attempts.TryRemove(Key(username, ip), out _);
}
