using System.Security.Cryptography;
using System.Text;

namespace SportShop.CustomerWeb.Services;

/// <summary>Shared password encoding. Legacy demo passwords are upgraded after a successful sign-in.</summary>
public static class PasswordStorage
{
    const string Prefix = "$sportshop$";
    const int Iterations = 210000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(derived)}";
    }

    public static bool IsEncoded(string password) => password.StartsWith(Prefix, StringComparison.Ordinal);

    public static bool Verify(string stored, string entered)
    {
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
