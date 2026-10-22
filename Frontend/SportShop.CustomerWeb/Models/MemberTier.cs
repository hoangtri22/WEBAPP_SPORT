namespace SportShop.CustomerWeb.Models;

/// <summary>Hạng thành viên (KHACH_HANG.hang_thanh_vien: thuong, bac, vang, VIP).</summary>
public static class MemberTier
{
    /// <summary>Khách chưa chi tiêu đồng nào luôn là hạng thường, dù dữ liệu đang ghi hạng khác.</summary>
    public static string Effective(string? stored, decimal totalSpent)
        => totalSpent <= 0 || string.IsNullOrEmpty(stored) ? "thuong" : stored;

    /// <summary>Tên hạng hiển thị cho khách.</summary>
    public static string Label(string code) => code switch { "thuong" => "Thường", "bac" => "Bạc", "vang" => "Vàng", _ => code };
}
