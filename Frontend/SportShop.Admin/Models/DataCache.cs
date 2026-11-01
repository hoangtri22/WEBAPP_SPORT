using System.Collections.Concurrent;

namespace SportShop.Admin.Models;

// Bộ nhớ đệm dùng chung giữa các request (singleton): ShopData cất các danh sách vừa nạp từ MySQL để lần sau dùng lại mà không truy vấn nữa; mỗi mục có hạn riêng
// (mặc định 2 phút, danh sách loại / sản phẩm và dữ liệu hóa đơn cho thống kê giữ 10 phút). ShopCommands.Run() gọi Clear() sau mỗi lần ghi thành công nên dữ liệu không bị cũ.
public class DataCache
{
    public static readonly TimeSpan DefaultAge = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan LongAge = TimeSpan.FromMinutes(10);

    readonly ConcurrentDictionary<string, (long At, long MaxAgeMs, object Value)> _items = new();

    public T Put<T>(string key, T value, TimeSpan? maxAge = null) where T : class
    {
        _items[key] = (Environment.TickCount64, (long)(maxAge ?? DefaultAge).TotalMilliseconds, value);
        return value;
    }

    public T? Get<T>(string key) where T : class =>
        _items.TryGetValue(key, out var e) && Environment.TickCount64 - e.At < e.MaxAgeMs ? e.Value as T : null;

    public void Clear() => _items.Clear();
}
