using Dapper;
using MySqlConnector;

namespace SportShop.CustomerWeb.Services;

// Đồng bộ lịch sử mua hàng không tài khoản của một số điện thoại vào khách có tài khoản; chạy trong transaction của CustomerStore.
static class GuestHistory
{
    // Gộp mọi hồ sơ chưa tài khoản dùng số phone vào customerId, rồi đặt địa chỉ mặc định theo đơn gần nhất (tài khoản mới hoặc chưa có địa chỉ).
    public static void Sync(MySqlConnection c, MySqlTransaction tx, int customerId, string? phone, bool newAccount)
    {
        if (string.IsNullOrEmpty(phone)) return;
        string device = DeviceSql(c, tx);
        // Dò không khóa để đăng nhập không chặn cả bảng KHACH_HANG, rồi chỉ khóa đúng các hồ sơ cần gộp.
        var found = c.Query<(int Id, int Device)>($"""
            SELECT kh.ma_khach_hang, {device} FROM KHACH_HANG kh
            WHERE kh.ma_tai_khoan IS NULL AND kh.da_xoa = 0 AND kh.ma_khach_hang <> @customerId
              AND (kh.so_dien_thoai = @phone OR ({device} = 1 AND EXISTS (SELECT 1 FROM DON_HANG d WHERE d.ma_khach_hang = kh.ma_khach_hang AND d.sdt_nguoi_nhan = @phone)))
            ORDER BY kh.ma_khach_hang
            """, new { customerId, phone }, tx).ToList();
        var locked = found.Count == 0 ? [] : c.Query<int>("SELECT ma_khach_hang FROM KHACH_HANG WHERE ma_khach_hang IN @ids AND (ma_khach_hang = @customerId OR (ma_tai_khoan IS NULL AND da_xoa = 0)) ORDER BY ma_khach_hang FOR UPDATE",
            new { ids = found.Select(g => g.Id).Append(customerId).ToArray(), customerId }, tx).ToHashSet();
        var guests = found.Where(g => locked.Contains(g.Id)).ToList();
        foreach (var guest in guests) Absorb(c, tx, guest.Id, guest.Device == 1, customerId, phone);
        if (guests.Count > 0) Retier(c, tx, customerId);
        if (guests.Count > 0 || newAccount) SetDefaultAddress(c, tx, customerId, phone, newAccount);
    }

    // Biểu thức SQL trả 1 nếu hồ sơ kh là của thiết bị app; bảng MOBILE_GUEST_CUSTOMER do API tạo khi có khách dùng app nên có thể chưa tồn tại.
    public static string DeviceSql(MySqlConnection c, MySqlTransaction tx) =>
        c.ExecuteScalar<int>("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'MOBILE_GUEST_CUSTOMER'", transaction: tx) > 0
            ? "IF(EXISTS (SELECT 1 FROM MOBILE_GUEST_CUSTOMER g WHERE g.customer_id = kh.ma_khach_hang), 1, 0)" : "0";

    // Chuyển đơn và dữ liệu của hồ sơ guest sang customerId rồi xóa hồ sơ; thiết bị app đặt cho nhiều số thì chỉ chuyển đơn của số phone và giữ lại hồ sơ.
    static void Absorb(MySqlConnection c, MySqlTransaction tx, int guest, bool device, int customerId, string phone)
    {
        var args = new { guest, customerId, phone };
        int[] ids = c.Query<int>("SELECT ma_don_hang FROM DON_HANG WHERE ma_khach_hang = @guest" + (device ? " AND sdt_nguoi_nhan = @phone" : ""), args, tx).Append(0).ToArray();
        // Số của đơn mới nhất không được chuyển; khác null nghĩa là hồ sơ thiết bị còn đơn của số khác.
        string? left = c.Query<string>("SELECT sdt_nguoi_nhan FROM DON_HANG WHERE ma_khach_hang = @guest AND ma_don_hang NOT IN @ids ORDER BY ma_don_hang DESC LIMIT 1", new { guest, ids }, tx).FirstOrDefault();

        // Gộp cả hồ sơ thì lấy toàn bộ chi tiêu và điểm, chỉ chuyển một phần đơn thì tính theo hóa đơn của các đơn đó như lúc lập hóa đơn.
        var (spent, points) = left is null
            ? c.QuerySingle<(decimal, long)>("SELECT tong_chi_tieu, CAST(diem_tich_luy AS SIGNED) FROM KHACH_HANG WHERE ma_khach_hang = @guest", args, tx)
            : c.QuerySingle<(decimal, long)>("SELECT IFNULL(SUM(tong_thanh_toan), 0), CAST(IFNULL(SUM(FLOOR(tong_thanh_toan / 10000)), 0) AS SIGNED) FROM HOA_DON WHERE ma_don_hang IN @ids", new { ids }, tx);
        c.Execute("""
            UPDATE DON_HANG SET ma_khach_hang = @customerId, ngay_cap_nhat = ngay_cap_nhat WHERE ma_don_hang IN @ids;
            UPDATE LUOT_SU_DUNG_KHUYEN_MAI SET ma_khach_hang = @customerId WHERE ma_don_hang IN @ids;
            DELETE FROM MOBILE_ORDER_REQUEST WHERE order_id IN @ids;
            UPDATE KHACH_HANG SET tong_chi_tieu = tong_chi_tieu + @spent, diem_tich_luy = diem_tich_luy + @points WHERE ma_khach_hang = @customerId;
            UPDATE SO_DIA_CHI SET ma_khach_hang = @customerId, la_mac_dinh = 0 WHERE ma_khach_hang = @guest AND (@whole OR sdt_nguoi_nhan = @phone);
            """, new { guest, customerId, phone, ids, spent, points, whole = left is null }, tx);
        if (left is not null)
        {
            c.Execute("UPDATE KHACH_HANG SET so_dien_thoai = @left, tong_chi_tieu = GREATEST(tong_chi_tieu - @spent, 0), diem_tich_luy = GREATEST(diem_tich_luy - @points, 0) WHERE ma_khach_hang = @guest",
                new { left, spent, points, guest }, tx);
            return;
        }

        c.Execute("""
            INSERT INTO CUSTOMER_CART_ITEM (customer_id, variant_id, quantity)
            SELECT @customerId, g.variant_id, g.quantity FROM CUSTOMER_CART_ITEM g WHERE g.customer_id = @guest
            ON DUPLICATE KEY UPDATE quantity = LEAST(CUSTOMER_CART_ITEM.quantity + VALUES(quantity), 999);
            DELETE FROM CUSTOMER_CART_ITEM WHERE customer_id = @guest;
            DELETE FROM CUSTOMER_CART_STATE WHERE customer_id = @guest;
            UPDATE LICH_SU_CHAM_SOC SET ma_khach_hang = @customerId WHERE ma_khach_hang = @guest;
            UPDATE PHAN_HOI SET ma_khach_hang = @customerId WHERE ma_khach_hang = @guest;
            INSERT IGNORE INTO KHACH_HANG_SO_THICH (ma_khach_hang, ma_so_thich) SELECT @customerId, ma_so_thich FROM KHACH_HANG_SO_THICH WHERE ma_khach_hang = @guest;
            DELETE FROM KHACH_HANG_SO_THICH WHERE ma_khach_hang = @guest;
            INSERT IGNORE INTO KHACH_HANG_NHOM_KH (ma_khach_hang, ma_nhom_kh) SELECT @customerId, ma_nhom_kh FROM KHACH_HANG_NHOM_KH WHERE ma_khach_hang = @guest;
            DELETE FROM KHACH_HANG_NHOM_KH WHERE ma_khach_hang = @guest;
            """, args, tx);
        if (device) c.Execute("DELETE FROM MOBILE_GUEST_CUSTOMER WHERE customer_id = @guest", args, tx);
        try { c.Execute("DELETE FROM KHACH_HANG WHERE ma_khach_hang = @guest AND ma_tai_khoan IS NULL", args, tx); }
        // Hồ sơ còn bị bảng khác tham chiếu thì chỉ xóa mềm.
        catch (MySqlException e) when (e.ErrorCode == MySqlErrorCode.RowIsReferenced2) { c.Execute("UPDATE KHACH_HANG SET da_xoa = 1 WHERE ma_khach_hang = @guest", args, tx); }
    }

    // Nâng hạng theo tổng chi tiêu sau khi gộp (2 triệu: bạc, 5 triệu: vàng như lúc lập hóa đơn), không hạ hạng đang có.
    static void Retier(MySqlConnection c, MySqlTransaction tx, int customerId) => c.Execute("""
        UPDATE KHACH_HANG SET hang_thanh_vien = ELT(GREATEST(FIELD(hang_thanh_vien, 'thuong', 'bac', 'vang'),
            CASE WHEN tong_chi_tieu >= 5000000 THEN 3 WHEN tong_chi_tieu >= 2000000 THEN 2 ELSE 1 END), 'thuong', 'bac', 'vang')
        WHERE ma_khach_hang = @customerId
        """, new { customerId }, tx);

    // Lấy người nhận và địa chỉ của đơn giao hàng gần nhất mang số phone làm địa chỉ mặc định (bỏ qua đơn mua tại quầy); tài khoản cũ đã có địa chỉ mặc định thì giữ nguyên.
    static void SetDefaultAddress(MySqlConnection c, MySqlTransaction tx, int customerId, string phone, bool newAccount)
    {
        var args = new { customerId, phone };
        if (!newAccount && c.ExecuteScalar<int>("SELECT COUNT(*) FROM SO_DIA_CHI WHERE ma_khach_hang = @customerId AND la_mac_dinh = 1", args, tx) > 0) return;
        var last = c.Query<(string Name, string Address)>("""
            SELECT ten_nguoi_nhan, dia_chi_giao_hang FROM DON_HANG
            WHERE ma_khach_hang = @customerId AND sdt_nguoi_nhan = @phone AND dia_chi_giao_hang <> 'Mua tại cửa hàng' ORDER BY ma_don_hang DESC LIMIT 1
            """, args, tx).ToList();
        if (last.Count == 0) return;
        var (name, address) = last[0];
        c.Execute("UPDATE SO_DIA_CHI SET la_mac_dinh = 0 WHERE ma_khach_hang = @customerId", args, tx);
        int? same = c.ExecuteScalar<int?>("SELECT ma_dia_chi FROM SO_DIA_CHI WHERE ma_khach_hang = @customerId AND sdt_nguoi_nhan = @phone AND dia_chi_chi_tiet = @address ORDER BY ma_dia_chi LIMIT 1",
            new { customerId, phone, address }, tx);
        if (same is int id) c.Execute("UPDATE SO_DIA_CHI SET la_mac_dinh = 1, ten_nguoi_nhan = @name WHERE ma_dia_chi = @id", new { id, name }, tx);
        else
            c.Execute("INSERT INTO SO_DIA_CHI (ma_khach_hang, ten_nguoi_nhan, sdt_nguoi_nhan, dia_chi_chi_tiet, la_mac_dinh) VALUES (@customerId, @name, @phone, @address, 1)",
                new { customerId, name, phone, address }, tx);
    }
}
