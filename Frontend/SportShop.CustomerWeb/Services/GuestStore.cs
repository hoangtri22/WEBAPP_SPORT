using Dapper;
using MySqlConnector;

namespace SportShop.CustomerWeb.Services;

// Khách mua không cần tài khoản: một dòng KHACH_HANG có ma_tai_khoan = NULL, nhận diện bằng số điện thoại; đăng ký cùng số (CustomerStore.Register()) thì giỏ, đơn đã mua và địa chỉ được đồng bộ vào tài khoản.
public class GuestStore(IConfiguration config)
{
    readonly string _cs = config.GetConnectionString("Default") ?? "";
    // Mở kết nối MySQL qua StoreDatabase.Connection().
    MySqlConnection Open() => StoreDatabase.Connection(_cs);

    // Tạo hồ sơ tạm (chưa có số điện thoại) để lưu giỏ hàng của khách vào CUSTOMER_CART_ITEM khi khách thêm sản phẩm đầu tiên.
    public int CreatePlaceholder()
    {
        using var c = Open();
        return c.ExecuteScalar<int>("INSERT INTO KHACH_HANG (ma_tai_khoan, ho_lot, ten, so_dien_thoai) VALUES (NULL, 'Khách', 'Vãng lai', NULL); SELECT LAST_INSERT_ID();");
    }

    // Hồ sơ khách còn dùng được: chưa gắn tài khoản, chưa bị xóa. Khách đã đăng ký tài khoản rồi thì phiên khách cũ không được dùng tiếp.
    public bool IsActive(int customerId)
    {
        if (customerId <= 0) return false;
        using var c = Open();
        return c.ExecuteScalar<int>("SELECT COUNT(*) FROM KHACH_HANG WHERE ma_khach_hang=@customerId AND ma_tai_khoan IS NULL AND da_xoa=0", new { customerId }) == 1;
    }

    // Số điện thoại của hồ sơ khách đang dùng (chưa tài khoản); chưa có số hoặc hồ sơ không còn dùng được thì trả null.
    public string? PhoneOf(int customerId)
    {
        if (customerId <= 0) return null;
        using var c = Open();
        return c.ExecuteScalar<string?>("SELECT so_dien_thoai FROM KHACH_HANG WHERE ma_khach_hang=@customerId AND ma_tai_khoan IS NULL AND da_xoa=0", new { customerId });
    }

    // Mọi hồ sơ khách chưa tài khoản dùng số điện thoại này (khách tại quầy, khách mobile, khách web).
    public List<int> FindAllByPhone(string phone)
    {
        using var c = Open();
        return c.Query<int>("SELECT ma_khach_hang FROM KHACH_HANG WHERE ma_tai_khoan IS NULL AND da_xoa=0 AND so_dien_thoai=@phone ORDER BY ma_khach_hang", new { phone }).ToList();
    }

    // Số điện thoại đã thuộc về một tài khoản đăng ký (phải đăng nhập để xem đơn của số này).
    public bool PhoneHasAccount(string phone)
    {
        using var c = Open();
        return c.ExecuteScalar<int>("""
            SELECT COUNT(*) FROM KHACH_HANG kh LEFT JOIN TAI_KHOAN tk ON tk.ma_tai_khoan = kh.ma_tai_khoan
            WHERE kh.da_xoa = 0 AND kh.ma_tai_khoan IS NOT NULL AND (kh.so_dien_thoai = @phone OR tk.so_dien_thoai = @phone)
            """, new { phone }) > 0;
    }

    // Chọn hồ sơ nhận đơn theo số điện thoại: đã có khách chưa tài khoản dùng số này (mã nhỏ nhất) thì dùng lại để giữ lịch sử mua; chưa có thì gắn số và tên vào hồ sơ tạm đang dùng, hoặc tạo hồ sơ mới nếu hồ sơ đang dùng đã mang số khác.
    public int Resolve(int guestId, string name, string phone)
    {
        using var c = Open();
        var existing = c.ExecuteScalar<int?>("SELECT ma_khach_hang FROM KHACH_HANG WHERE ma_tai_khoan IS NULL AND da_xoa=0 AND so_dien_thoai=@phone ORDER BY ma_khach_hang LIMIT 1", new { phone });
        if (existing is int found) return found;

        var (last, first) = SplitName(name);
        var current = c.ExecuteScalar<string?>("SELECT so_dien_thoai FROM KHACH_HANG WHERE ma_khach_hang=@guestId AND ma_tai_khoan IS NULL AND da_xoa=0", new { guestId });
        if (guestId > 0 && string.IsNullOrEmpty(current) && IsActive(guestId))
        {
            c.Execute("UPDATE KHACH_HANG SET ho_lot=@last, ten=@first, so_dien_thoai=@phone WHERE ma_khach_hang=@guestId AND ma_tai_khoan IS NULL",
                new { last, first, phone, guestId });
            return guestId;
        }
        return c.ExecuteScalar<int>("INSERT INTO KHACH_HANG (ma_tai_khoan, ho_lot, ten, so_dien_thoai) VALUES (NULL, @last, @first, @phone); SELECT LAST_INSERT_ID();",
            new { last, first, phone });
    }

    // Chuyển các dòng giỏ của hồ sơ from sang hồ sơ to (cộng dồn số lượng, tối đa 999) rồi xóa giỏ của from.
    public void MoveCart(int from, int to)
    {
        using var c = Open();
        c.Open();
        using var tx = c.BeginTransaction();
        c.Execute("""
            INSERT INTO CUSTOMER_CART_ITEM (customer_id, variant_id, quantity)
            SELECT @to, g.variant_id, g.quantity FROM CUSTOMER_CART_ITEM g WHERE g.customer_id = @from
            ON DUPLICATE KEY UPDATE quantity = LEAST(CUSTOMER_CART_ITEM.quantity + VALUES(quantity), 999)
            """, new { from, to }, tx);
        c.Execute("DELETE FROM CUSTOMER_CART_ITEM WHERE customer_id = @from", new { from }, tx);
        tx.Commit();
    }

    // Xóa hồ sơ tạm không còn dùng (chưa số điện thoại, chưa có đơn): xóa dòng giỏ rồi dòng khách; hồ sơ thật hoặc bị ràng buộc khác thì giữ nguyên.
    public void DiscardPlaceholder(int customerId)
    {
        try
        {
            using var c = Open();
            c.Open();
            using var tx = c.BeginTransaction();
            bool placeholder = c.ExecuteScalar<int>("""
                SELECT COUNT(*) FROM KHACH_HANG kh
                WHERE kh.ma_khach_hang=@customerId AND kh.ma_tai_khoan IS NULL AND kh.so_dien_thoai IS NULL AND kh.ho_lot='Khách' AND kh.ten='Vãng lai'
                  AND NOT EXISTS (SELECT 1 FROM DON_HANG d WHERE d.ma_khach_hang = kh.ma_khach_hang)
                """, new { customerId }, tx) == 1;
            if (!placeholder) return;
            c.Execute("DELETE FROM CUSTOMER_CART_ITEM WHERE customer_id=@customerId", new { customerId }, tx);
            c.Execute("DELETE FROM CUSTOMER_CART_STATE WHERE customer_id=@customerId", new { customerId }, tx);
            c.Execute("DELETE FROM KHACH_HANG WHERE ma_khach_hang=@customerId", new { customerId }, tx);
            tx.Commit();
        }
        catch (MySqlException) { }
    }

    // Tách họ tên thành họ lót và tên (từ cuối), cắt theo độ dài cột.
    internal static (string Last, string First) SplitName(string? fullName)
    {
        var words = (fullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return ("", "Khách");
        string first = words[^1], last = string.Join(' ', words[..^1]);
        return (last.Length > 70 ? last[..70] : last, first.Length > 30 ? first[..30] : first);
    }
}
