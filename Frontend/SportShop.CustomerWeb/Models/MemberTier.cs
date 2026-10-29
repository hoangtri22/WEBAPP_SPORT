namespace SportShop.CustomerWeb.Models;

public static class MemberTier
{
    public static string Effective(string? stored, decimal totalSpent)
        => totalSpent <= 0 || string.IsNullOrEmpty(stored) ? "thuong" : stored;

    public static string Label(string code) => code switch { "thuong" => "Thường", "bac" => "Bạc", "vang" => "Vàng", _ => code };
}
