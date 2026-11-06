using MySqlConnector;
using SportShop.Application;

namespace SportShop.CustomerWeb.Services;

internal static class StoreDatabase
{
    // Áp cùng cấu hình kết nối có giới hạn cho các thao tác dùng chung web và mobile.
    internal static MySqlConnection Connection(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new DatabaseUnavailableException("The shop database connection is not configured.");
        try
        {
            var settings = new MySqlConnectionStringBuilder(connectionString)
            {
                ConnectionTimeout = 3, DefaultCommandTimeout = 5, CancellationTimeout = 1
            };
            if (string.IsNullOrWhiteSpace(settings.Database))
                throw new DatabaseUnavailableException("The shop database name is not configured.");
            return new MySqlConnection(settings.ConnectionString);
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException)
        {
            throw new DatabaseUnavailableException("The shop database configuration is invalid.", e);
        }
    }
}
