using System.Text.Json;

namespace SportShop.CustomerWeb.Services;

/// <summary>
/// Lưu giỏ hàng của từng tài khoản trong một file JSON (App_Data/carts.json) bên cạnh web khách hàng:
/// { mã khách hàng → { mã biến thể → số lượng } }. Nhờ vậy giỏ của mỗi tài khoản còn nguyên ở các lần đăng nhập sau,
/// kể cả khi đóng trình duyệt hoặc khởi động lại web, và không cần thêm bảng nào trong CSDL.
/// Đăng ký một lần duy nhất (singleton); mọi thao tác đọc / ghi đều khóa để hai yêu cầu cùng lúc không ghi đè nhau.
/// </summary>
public class CartStore(IWebHostEnvironment env)
{
    readonly string _path = Path.Combine(env.ContentRootPath, "App_Data", "carts.json");
    readonly object _lock = new();
    Dictionary<int, Dictionary<int, int>>? _all;

    /// <summary>Toàn bộ giỏ hàng, đọc từ file ở lần dùng đầu tiên rồi giữ trong bộ nhớ.</summary>
    Dictionary<int, Dictionary<int, int>> All() => _all ??= Load();

    Dictionary<int, Dictionary<int, int>> Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<Dictionary<int, Dictionary<int, int>>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException) { }   // file hỏng hoặc đang bị khóa: coi như chưa có giỏ nào
        return [];
    }

    /// <summary>Giỏ hàng của một khách (bản sao, sửa xong phải gọi Save); chưa có thì trả về giỏ trống.</summary>
    public Dictionary<int, int> Get(int customerId)
    {
        lock (_lock) return All().TryGetValue(customerId, out var cart) ? new(cart) : [];
    }

    /// <summary>Lưu giỏ hàng của một khách; giỏ trống thì xóa khỏi file.</summary>
    public void Save(int customerId, Dictionary<int, int> cart)
    {
        lock (_lock)
        {
            var all = All();
            if (cart.Count == 0) all.Remove(customerId); else all[customerId] = new(cart);

            // Ghi ra file tạm rồi đổi tên để file chính không bao giờ bị ghi dở
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(all));
            File.Move(temp, _path, overwrite: true);
        }
    }
}
