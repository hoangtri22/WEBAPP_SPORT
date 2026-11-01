using System.Data;
using Dapper;
using MySqlConnector;
using SportShop.Application;
using SportShop.Application.Health;

namespace SportShop.Infrastructure;

public sealed class DatabaseReadiness(MySqlConnectionFactory connections) : IDatabaseReadiness
{
    // LIMIT 0 checks catalog and customer transaction prerequisites without reading personal data.
    private const string RequiredSchema = """
        SELECT ma_loai, ten_loai FROM LOAI_SP LIMIT 0;
        SELECT ma_san_pham, ma_loai, ten_san_pham, don_vi_tinh, mo_ta, trang_thai FROM SAN_PHAM LIMIT 0;
        SELECT ma_bien_the, ma_san_pham, kich_co, mau_sac, gia_ban, so_luong_ton, trang_thai, hinh_anh
            FROM BIEN_THE_SP LIMIT 0;
        SELECT ma_ctkm, ten_ctkm, loai_khuyen_mai, ngay_bat_dau, ngay_ket_thuc, gia_tri_giam_toi_da,
               hang_ap_dung, so_luong_gioi_han, so_luot_da_dung, gioi_han_khach_hang, trang_thai
            FROM CTKM LIMIT 0;
        SELECT ma_ctkm, ma_voucher, phan_tram_giam FROM KM_THEO_VOUCHER LIMIT 0;
        SELECT ma_so_thich, ten_so_thich FROM SO_THICH LIMIT 0;
        SELECT ma_phuong_thuc_thanh_toan, ten_phuong_thuc_thanh_toan, trang_thai
            FROM PHUONG_THUC_THANH_TOAN LIMIT 0;
        SELECT ma_tai_khoan, ten_dang_nhap, mat_khau, email, trang_thai FROM TAI_KHOAN LIMIT 0;
        SELECT ma_khach_hang, ma_tai_khoan, da_xoa FROM KHACH_HANG LIMIT 0;
        SELECT ma_don_hang, ma_khach_hang, trang_thai_don_hang FROM DON_HANG LIMIT 0;
        SELECT customer_id, access_hash, refresh_hash, password_fingerprint, revoked FROM MOBILE_AUTH_SESSION LIMIT 0;
        SELECT refresh_hash, session_id FROM MOBILE_AUTH_REFRESH_USED LIMIT 0;
        SELECT customer_id, legacy_imported FROM CUSTOMER_CART_STATE LIMIT 0;
        SELECT customer_id, variant_id, quantity FROM CUSTOMER_CART_ITEM LIMIT 0;
        SELECT customer_id, request_key, request_hash, order_id FROM MOBILE_ORDER_REQUEST LIMIT 0;
        SELECT ma_vai_tro, ten_vai_tro FROM VAI_TRO LIMIT 0;
        SELECT ma_khach_hang, ma_so_thich FROM KHACH_HANG_SO_THICH LIMIT 0;
        SELECT ma_khach_hang, ten_nguoi_nhan, sdt_nguoi_nhan, dia_chi_chi_tiet, la_mac_dinh FROM SO_DIA_CHI LIMIT 0;
        SELECT ma_don_hang, ma_bien_the, so_luong, don_gia, thanh_tien FROM CHI_TIET_DON_HANG LIMIT 0;
        SELECT ma_don_hang, phi_van_chuyen, ten_don_vi_van_chuyen, ma_van_don FROM VAN_CHUYEN LIMIT 0;
        SELECT ma_don_hang, so_hoa_don FROM HOA_DON LIMIT 0;
        SELECT ma_ctkm, ma_khach_hang, ma_don_hang FROM LUOT_SU_DUNG_KHUYEN_MAI LIMIT 0;
        SELECT ma_phan_hoi, ma_khach_hang, ma_san_pham, loai, so_sao, noi_dung, ngay_gui, trang_thai FROM PHAN_HOI LIMIT 0;
        SELECT ma_tra_loi, ma_phan_hoi, noi_dung FROM TRA_LOI_PHAN_HOI LIMIT 0;
        SELECT ma_khao_sat, tieu_de, mo_ta, ngay_bat_dau, ngay_ket_thuc, trang_thai FROM KHAO_SAT LIMIT 0;
        SELECT ma_khao_sat, ma_khach_hang, trang_thai, ngay_hoan_thanh FROM KHAO_SAT_KHACH_HANG LIMIT 0;
        SELECT ma_cau_hoi, ma_khao_sat, noi_dung, loai_cau_hoi, bat_buoc, thu_tu FROM CAU_HOI_KHAO_SAT LIMIT 0;
        SELECT ma_cau_hoi, ma_lua_chon, noi_dung FROM LUA_CHON_CAU_HOI LIMIT 0;
        SELECT ma_khao_sat, ma_khach_hang, ma_cau_hoi, ma_lua_chon, noi_dung_tra_loi FROM TRA_LOI_KHAO_SAT LIMIT 0;
        """;

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead, isReadOnly: true, cancellationToken);
            using var results = await connection.QueryMultipleAsync(new CommandDefinition(
                RequiredSchema, transaction: transaction,
                commandTimeout: MySqlConnectionFactory.CommandTimeoutSeconds,
                cancellationToken: cancellationToken));
            while (!results.IsConsumed)
                await results.ReadAsync();
            await transaction.CommitAsync(cancellationToken);
        }
        catch (MySqlException exception) when (MySqlConnectionFactory.IsUnavailable(exception))
        {
            throw new DatabaseUnavailableException("The mobile API database schema is unavailable.", exception);
        }
    }
}
