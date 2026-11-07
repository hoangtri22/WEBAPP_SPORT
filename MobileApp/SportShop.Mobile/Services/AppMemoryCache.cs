using System.Collections.Concurrent;

namespace SportShop.Mobile.Services;

// Bộ nhớ đệm dùng chung trên app (singleton, mặc định 10 phút = LongAge): dùng lại danh sách trong RAM thay vì gọi lại API; hết hạn hoặc khi đặt, hủy đơn thì tải lại.
public sealed class AppMemoryCache(Func<long>? clockMs = null)
{
    public static readonly TimeSpan DefaultAge = TimeSpan.FromMinutes(10);

    private readonly Func<long> _now = clockMs ?? (() => Environment.TickCount64);
    private readonly ConcurrentDictionary<string, CacheEntry> _items = new();

    public const string OrdersKey = "orders_all";
    public const string ProductsKey = "products_catalog";
    public const string InvoiceStatsPrefix = "invoice_stats:";

    // Lưu dữ liệu vào bộ nhớ đệm theo key và trả lại chính giá trị đó.
    public T Put<T>(string key, T value, TimeSpan? maxAge = null) where T : class
    {
        var ttl = (long)(maxAge ?? DefaultAge).TotalMilliseconds;
        _items[key] = new CacheEntry(_now(), ttl, value);
        return value;
    }

    // Lấy dữ liệu còn hạn, hết hạn hoặc chưa có thì trả null.
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

    // Thời gian còn lại của dữ liệu, hết hạn hoặc chưa có thì trả null.
    public TimeSpan? Remaining(string key)
    {
        if (!_items.TryGetValue(key, out var entry)) return null;
        var rem = entry.MaxAgeMs - (_now() - entry.LoadedAtMs);
        return rem > 0 ? TimeSpan.FromMilliseconds(rem) : null;
    }

    // Xóa một key.
    public void Remove(string key) => _items.TryRemove(key, out _);

    // Xóa bộ nhớ đệm của đơn hàng, sản phẩm và thống kê hóa đơn ngay khi khách Đặt đơn hoặc Hủy đơn.
    public void InvalidateCommerce()
    {
        _items.TryRemove(OrdersKey, out _);
        _items.TryRemove(ProductsKey, out _);
        foreach (var k in _items.Keys.Where(k => k.StartsWith(InvoiceStatsPrefix, StringComparison.Ordinal)).ToArray())
            _items.TryRemove(k, out _);
    }

    // Xóa toàn bộ bộ nhớ đệm.
    public void Clear() => _items.Clear();

    // Một mục trong bộ nhớ đệm.
    private sealed record CacheEntry(long LoadedAtMs, long MaxAgeMs, object Value);
}
