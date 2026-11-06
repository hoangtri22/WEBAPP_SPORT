using System.Collections.Concurrent;

namespace SportShop.Mobile.Services;

// Bộ nhớ đệm dùng chung trên Mobile App (singleton, giới hạn mặc định 10 phút = LongAge):
// Khi người dùng gõ n từ khóa tìm kiếm trong 1 phút hoặc chuyển đổi các bảng thống kê hóa đơn liên tiếp,
// ứng dụng tái sử dụng danh sách đã lưu trong RAM mà không tải lại câu truy vấn API / MySQL.
// Sau 10 phút hoặc khi Đặt đơn / Hủy đơn làm thay đổi dữ liệu thì tự động xóa cache để tải lại.
public sealed class AppMemoryCache(Func<long>? clockMs = null)
{
    public static readonly TimeSpan DefaultAge = TimeSpan.FromMinutes(10);

    private readonly Func<long> _now = clockMs ?? (() => Environment.TickCount64);
    private readonly ConcurrentDictionary<string, CacheEntry> _items = new();

    public const string OrdersKey = "orders_all";
    public const string ProductsKey = "products_catalog";
    public const string InvoiceStatsPrefix = "invoice_stats:";

    public T Put<T>(string key, T value, TimeSpan? maxAge = null) where T : class
    {
        var ttl = (long)(maxAge ?? DefaultAge).TotalMilliseconds;
        _items[key] = new CacheEntry(_now(), ttl, value);
        return value;
    }

    public T? Get<T>(string key) where T : class
    {
        if (!_items.TryGetValue(key, out var entry)) return null;
        var elapsed = _now() - entry.LoadedAtMs;
        if (elapsed >= entry.MaxAgeMs)
        {
            _items.TryRemove(key, out _);
            return null;
        }
        return entry.Value as T;
    }

    public TimeSpan? Remaining(string key)
    {
        if (!_items.TryGetValue(key, out var entry)) return null;
        var rem = entry.MaxAgeMs - (_now() - entry.LoadedAtMs);
        return rem > 0 ? TimeSpan.FromMilliseconds(rem) : null;
    }

    public void Remove(string key) => _items.TryRemove(key, out _);

    // Xóa bộ nhớ đệm của đơn hàng, sản phẩm và thống kê hóa đơn ngay khi khách Đặt đơn hoặc Hủy đơn.
    public void InvalidateCommerce()
    {
        _items.TryRemove(OrdersKey, out _);
        _items.TryRemove(ProductsKey, out _);
        foreach (var k in _items.Keys.Where(k => k.StartsWith(InvoiceStatsPrefix, StringComparison.Ordinal)).ToArray())
            _items.TryRemove(k, out _);
    }

    public void Clear() => _items.Clear();

    private sealed record CacheEntry(long LoadedAtMs, long MaxAgeMs, object Value);
}
