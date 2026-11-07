namespace SportShop.Admin.Models;

// Quy tắc hạng thành viên, bản của Web Admin.
public static class MemberTier
{
    // Hạng hiệu lực: chưa chi tiêu hoặc chưa lưu hạng thì là thuong, còn lại lấy hạng đã lưu.
    public static string Effective(string? stored, decimal totalSpent)
        => totalSpent <= 0 || string.IsNullOrEmpty(stored) ? "thuong" : stored;

    // Đổi mã hạng sang tên hiển thị.
    public static string Label(string code) => code switch { "thuong" => "Thường", "bac" => "Bạc", "vang" => "Vàng", _ => code };
}
