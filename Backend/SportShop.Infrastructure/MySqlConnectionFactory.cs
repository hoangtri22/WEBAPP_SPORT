using System.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using SportShop.Application;

namespace SportShop.Infrastructure;

public sealed class MySqlConnectionFactory(IConfiguration configuration)
{
    public const int CommandTimeoutSeconds = 5;

    // Opens only a connection; catalog reads never create or repair database objects.
    public async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configured = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(configured))
            throw new DatabaseUnavailableException("The catalog database connection is not configured.");

        MySqlConnectionStringBuilder settings;
        try
        {
            settings = new MySqlConnectionStringBuilder(configured)
            {
                ConnectionTimeout = 3,
                DefaultCommandTimeout = CommandTimeoutSeconds,
                CancellationTimeout = 1
            };
            if (string.IsNullOrWhiteSpace(settings.Database))
                throw new DatabaseUnavailableException("The catalog database name is not configured.");
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            throw new DatabaseUnavailableException("The catalog database configuration is invalid.", exception);
        }

        var connection = new MySqlConnection(settings.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (Exception exception)
        {
            await connection.DisposeAsync();
            if (exception is OperationCanceledException)
                throw;
            if (exception is MySqlException or TimeoutException or IOException or SocketException)
                throw new DatabaseUnavailableException("The catalog database connection is unavailable.", exception);
            throw;
        }
    }

    public static bool IsUnavailable(MySqlException exception) => exception.IsTransient || exception.ErrorCode is
        MySqlErrorCode.UnableToConnectToHost or
        MySqlErrorCode.DatabaseAccessDenied or
        MySqlErrorCode.AccessDenied or
        MySqlErrorCode.NoDatabaseSelected or
        MySqlErrorCode.UnknownDatabase or
        MySqlErrorCode.ServerShutdown or
        MySqlErrorCode.ConnectionCountError or
        MySqlErrorCode.BadFieldError or
        MySqlErrorCode.TableAccessDenied or
        MySqlErrorCode.ColumnAccessDenied or
        MySqlErrorCode.NoSuchTable or
        MySqlErrorCode.CommandTimeoutExpired ||
        exception.Number is 1129 or 1130 or 1203 or 1226 or 1251 or 2002 or 2003 or 2006 or 2013 or 2055;
}
