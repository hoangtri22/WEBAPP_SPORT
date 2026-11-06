using System.Security.Cryptography;
using System.Text;

namespace SportShop.CustomerWeb.Services;

/// <summary>Mã hóa mật khẩu dùng chung bằng AES-GCM để Quản lý xem lại được; bản PBKDF2 cũ chỉ kiểm tra được, mật khẩu demo dạng chữ được mã hóa sau khi đăng nhập đúng.</summary>
public static class PasswordStorage
{
    const string Prefix = "$sportshop$";
    const string Reversible = Prefix + "aes$";
    const int NonceSize = 12, TagSize = 16;
    static readonly byte[] Key = SHA256.HashData(Encoding.UTF8.GetBytes("SportShop.PasswordStorage.v1"));

    // Mã hóa mật khẩu bằng AES-GCM với nonce ngẫu nhiên; khóa nằm trong code nên ai có cả code và CSDL đều giải được.
    public static string Hash(string password)
    {
        var plain = Encoding.UTF8.GetBytes(password);
        var box = new byte[NonceSize + TagSize + plain.Length];
        RandomNumberGenerator.Fill(box.AsSpan(0, NonceSize));
        using var aes = new AesGcm(Key, TagSize);
        aes.Encrypt(box.AsSpan(0, NonceSize), plain, box.AsSpan(NonceSize + TagSize), box.AsSpan(NonceSize, TagSize));
        return Reversible + Convert.ToBase64String(box);
    }

    // Giải mã bản AES-GCM; dữ liệu hỏng thì trả null.
    static string? Decrypt(string stored)
    {
        try
        {
            var box = Convert.FromBase64String(stored[Reversible.Length..]);
            var plain = new byte[box.Length - NonceSize - TagSize];
            using var aes = new AesGcm(Key, TagSize);
            aes.Decrypt(box.AsSpan(0, NonceSize), box.AsSpan(NonceSize + TagSize), box.AsSpan(NonceSize, TagSize), plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception e) when (e is FormatException or CryptographicException or OverflowException) { return null; }
    }

    // Trả mật khẩu gốc cho Quản lý xem (ShopData.CustomerPassword()): dạng chữ hoặc AES-GCM; bản PBKDF2 cũ không giải được thì trả null.
    public static string? Reveal(string stored)
        => !IsEncoded(stored) ? stored : stored.StartsWith(Reversible, StringComparison.Ordinal) ? Decrypt(stored) : null;

    public static bool IsEncoded(string password) => password.StartsWith(Prefix, StringComparison.Ordinal);

    // So mật khẩu nhập với bản lưu: bản AES-GCM thì giải mã, bản PBKDF2 cũ thì tính lại, mật khẩu demo thì so trực tiếp.
    public static bool Verify(string stored, string entered)
    {
        if (stored.StartsWith(Reversible, StringComparison.Ordinal))
            return Decrypt(stored) is { } plain && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(plain), Encoding.UTF8.GetBytes(entered));
        if (!IsEncoded(stored))
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(entered));
        try
        {
            var parts = stored[Prefix.Length..].Split('$');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations) || iterations is < 100000 or > 1000000) return false;
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            return CryptographicOperations.FixedTimeEquals(expected,
                Rfc2898DeriveBytes.Pbkdf2(entered, salt, iterations, HashAlgorithmName.SHA256, expected.Length));
        }
        catch (FormatException) { return false; }
    }
}
