using System.Globalization;
using Dapper;
using MySqlConnector;
using SportShop.CustomerWeb.Models;

namespace SportShop.CustomerWeb.Services;

public record CatalogVariant(int Id, string? Size, string? Color, decimal Price, int Stock, bool Visible, string? Image);

public record CatalogProduct(int Id, int CategoryId, string Name, string Category, string Unit, string? Description, List<CatalogVariant> Variants);

public record CatalogSnapshot(List<(int Id, string Name)> Categories, List<CatalogProduct> Products);

// Bộ nhớ đệm danh mục sản phẩm dùng chung cho mọi khách (singleton): nạp loại, sản phẩm đang hiện và mọi biến thể từ MySQL một lần rồi giữ 10 phút,
// ShopStore.Search / Categories / FilterOptions lọc ngay trong bộ nhớ nên khách gõ nhiều từ khóa liên tiếp không phải truy vấn lại.
// Khách đặt / hủy đơn (làm đổi tồn kho) gọi Clear() để nạp lại ngay; sản phẩm do trang quản trị sửa sẽ hiện sau tối đa 10 phút.
public class CatalogCache(IConfiguration config)
{
    static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);
    readonly string _cs = config.GetConnectionString("Default") ?? "";
    readonly object _lock = new();
    CatalogSnapshot? _snapshot;
    long _loadedAt;

    public CatalogSnapshot Get()
    {
        lock (_lock)
        {
            if (_snapshot == null || Environment.TickCount64 - _loadedAt >= (long)MaxAge.TotalMilliseconds)
            {
                _snapshot = Load();
                _loadedAt = Environment.TickCount64;
            }
            return _snapshot;
        }
    }

    public void Clear()
    {
        lock (_lock) _snapshot = null;
    }

    CatalogSnapshot Load()
    {
        using var c = new MySqlConnection(_cs);
        var categories = c.Query("SELECT ma_loai, ten_loai FROM LOAI_SP WHERE ten_loai <> @archived ORDER BY ma_loai", new { archived = Catalog.ArchivedCategory })
            .Cast<IDictionary<string, object?>>().Select(r => (Id: I(r, "ma_loai"), Name: S(r, "ten_loai"))).ToList();
        var variants = c.Query("SELECT ma_bien_the, ma_san_pham, kich_co, mau_sac, gia_ban, so_luong_ton, trang_thai, hinh_anh FROM BIEN_THE_SP ORDER BY ma_bien_the")
            .Cast<IDictionary<string, object?>>()
            .ToLookup(r => I(r, "ma_san_pham"), r => new CatalogVariant(I(r, "ma_bien_the"), NS(r, "kich_co"), NS(r, "mau_sac"), D(r, "gia_ban"), I(r, "so_luong_ton"), S(r, "trang_thai") == "hien", NS(r, "hinh_anh")));
        var products = c.Query("""
            SELECT sp.ma_san_pham, sp.ma_loai, sp.ten_san_pham, l.ten_loai, sp.don_vi_tinh, sp.mo_ta
            FROM SAN_PHAM sp JOIN LOAI_SP l ON l.ma_loai = sp.ma_loai
            WHERE sp.trang_thai = 'hien' ORDER BY sp.ma_san_pham
            """).Cast<IDictionary<string, object?>>()
            .Select(r => new CatalogProduct(I(r, "ma_san_pham"), I(r, "ma_loai"), S(r, "ten_san_pham"), S(r, "ten_loai"), S(r, "don_vi_tinh"), NS(r, "mo_ta"), variants[I(r, "ma_san_pham")].ToList())).ToList();
        return new CatalogSnapshot(categories, products);
    }

    static bool IsNull(object? v) => v is null or DBNull;
    static int I(IDictionary<string, object?> r, string k) => Convert.ToInt32(r[k], CultureInfo.InvariantCulture);
    static decimal D(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? 0 : Convert.ToDecimal(r[k], CultureInfo.InvariantCulture);
    static string S(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? "" : Convert.ToString(r[k], CultureInfo.InvariantCulture)!;
    static string? NS(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? null : Convert.ToString(r[k], CultureInfo.InvariantCulture);
}
