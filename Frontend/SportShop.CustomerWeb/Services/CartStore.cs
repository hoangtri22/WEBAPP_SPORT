using System.Text.Json;

namespace SportShop.CustomerWeb.Services;

public class CartStore(IWebHostEnvironment env)
{
    readonly string _path = Path.Combine(env.ContentRootPath, "App_Data", "carts.json");
    readonly object _lock = new();
    Dictionary<int, Dictionary<int, int>>? _all;

    Dictionary<int, Dictionary<int, int>> All() => _all ??= Load();

    Dictionary<int, Dictionary<int, int>> Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<Dictionary<int, Dictionary<int, int>>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException) { }
        return [];
    }

    // Lấy giỏ hàng của khách từ file App_Data/carts.json (đọc một lần rồi giữ trong bộ nhớ).
    public Dictionary<int, int> Get(int customerId)
    {
        lock (_lock) return All().TryGetValue(customerId, out var cart) ? new(cart) : [];
    }

    // Lưu giỏ hàng của khách vào App_Data/carts.json (ghi file tạm rồi thay thế).
    public void Save(int customerId, Dictionary<int, int> cart)
    {
        lock (_lock)
        {
            var all = All();
            if (cart.Count == 0) all.Remove(customerId); else all[customerId] = new(cart);

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(all));
            File.Move(temp, _path, overwrite: true);
        }
    }
}
