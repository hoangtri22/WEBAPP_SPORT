using System.Collections.Concurrent;

namespace SportShop.Admin.Models;

// Bộ nhớ đệm dùng chung giữa các request (singleton): ShopData gọi Put() cất các danh sách vừa nạp từ MySQL để ReportsController dùng lại trong 2 phút; ShopCommands.Run() gọi Clear() sau mỗi lần ghi thành công.
public class DataCache
{
    static readonly long MaxAgeMs = (long)TimeSpan.FromMinutes(2).TotalMilliseconds;
    readonly ConcurrentDictionary<string, (long At, object Value)> _items = new();

    public T Put<T>(string key, T value) where T : class
    {
        _items[key] = (Environment.TickCount64, value);
        return value;
    }

    public T? Get<T>(string key) where T : class =>
        _items.TryGetValue(key, out var e) && Environment.TickCount64 - e.At < MaxAgeMs ? e.Value as T : null;

    public void Clear() => _items.Clear();
}
