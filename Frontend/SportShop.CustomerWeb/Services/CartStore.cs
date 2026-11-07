using System.Text.Json;
using Dapper;
using MySqlConnector;
using SportShop.CustomerWeb.Models;

namespace SportShop.CustomerWeb.Services;

/// <summary>Giỏ hàng lưu ở CUSTOMER_CART_ITEM, dùng chung web và mobile; mọi thay đổi khóa khách qua LockCustomer().</summary>
public class CartStore(IWebHostEnvironment env, IConfiguration config)
{
    readonly string _cs = config.GetConnectionString("Default") ?? "";
    readonly string _legacyPath = Path.GetFullPath(Path.Combine(env.ContentRootPath,
        config["Commerce:LegacyCartPath"] ?? (env.ApplicationName == "SportShop.CustomerWeb"
            ? "App_Data/carts.json" : "../../Frontend/SportShop.CustomerWeb/App_Data/carts.json")));

    // Khóa dòng KHACH_HANG (FOR UPDATE) để các thao tác giỏ chạy lần lượt; khách không còn thì báo OrderError.
    internal static void LockCustomer(MySqlConnection connection, MySqlTransaction transaction, int customerId)
    {
        var exists = connection.ExecuteScalar<int?>(
            "SELECT ma_khach_hang FROM KHACH_HANG WHERE ma_khach_hang=@customerId AND da_xoa=0 FOR UPDATE",
            new { customerId }, transaction);
        if (exists is null) throw new OrderError("Không tìm thấy khách hàng đang hoạt động.");
    }

    // Nhập giỏ JSON cũ của mỗi khách một lần (CUSTOMER_CART_STATE); giữ file gốc làm bản sao.
    internal Dictionary<int, int> ReadLocked(MySqlConnection connection, MySqlTransaction transaction, int customerId)
    {
        var imported = connection.ExecuteScalar<int?>(
            "SELECT legacy_imported FROM CUSTOMER_CART_STATE WHERE customer_id=@customerId",
            new { customerId }, transaction);
        if (imported is null)
        {
            Dictionary<int, int>? legacy = null;
            if (File.Exists(_legacyPath))
                legacy = JsonSerializer.Deserialize<Dictionary<int, Dictionary<int, int>>>(File.ReadAllText(_legacyPath))
                    ?.GetValueOrDefault(customerId);
            connection.Execute("INSERT INTO CUSTOMER_CART_STATE(customer_id,legacy_imported) VALUES (@customerId,1)",
                new { customerId }, transaction);
            if (legacy is not null)
                foreach (var item in legacy.Where(x => x.Key > 0 && x.Value is > 0 and <= 999))
                    connection.Execute("""
                        INSERT INTO CUSTOMER_CART_ITEM(customer_id,variant_id,quantity)
                        SELECT @customerId, ma_bien_the, @quantity FROM BIEN_THE_SP WHERE ma_bien_the=@variantId
                        """, new { customerId, variantId = item.Key, quantity = item.Value }, transaction);
        }
        return connection.Query<(int VariantId, int Quantity)>(
            "SELECT variant_id,quantity FROM CUSTOMER_CART_ITEM WHERE customer_id=@customerId ORDER BY variant_id",
            new { customerId }, transaction).ToDictionary(x => x.VariantId, x => x.Quantity);
    }

    // Ghi lại toàn bộ giỏ của khách vào CUSTOMER_CART_ITEM.
    internal static void WriteLocked(MySqlConnection connection, MySqlTransaction transaction, int customerId, Dictionary<int, int> cart)
    {
        connection.Execute("DELETE FROM CUSTOMER_CART_ITEM WHERE customer_id=@customerId", new { customerId }, transaction);
        foreach (var item in cart)
            connection.Execute("INSERT INTO CUSTOMER_CART_ITEM(customer_id,variant_id,quantity) VALUES (@customerId,@variantId,@quantity)",
                new { customerId, variantId = item.Key, quantity = item.Value }, transaction);
    }

    // Lấy bản sao giỏ của khách.
    public Dictionary<int, int> Get(int customerId) => Mutate(customerId, cart => new Dictionary<int, int>(cart));

    // Thay toàn bộ giỏ qua Mutate(). Gọi từ ShopStore.
    public void Save(int customerId, Dictionary<int, int> cart) => Mutate(customerId, current =>
    {
        current.Clear();
        foreach (var item in cart) current.Add(item.Key, item.Value);
        return true;
    });

    // Đọc và ghi giỏ trong một transaction có khóa khách; chỉ ghi lại khi giỏ thay đổi. ShopStore gọi hàm này.
    public T Mutate<T>(int customerId, Func<Dictionary<int, int>, T> action)
    {
        using var connection = StoreDatabase.Connection(_cs);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        LockCustomer(connection, transaction, customerId);
        var cart = ReadLocked(connection, transaction, customerId);
        var before = new Dictionary<int, int>(cart);
        var result = action(cart);
        if (before.Count != cart.Count || before.Any(x => cart.GetValueOrDefault(x.Key) != x.Value))
            WriteLocked(connection, transaction, customerId, cart);
        transaction.Commit();
        return result;
    }
}
