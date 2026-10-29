using MySqlConnector;

namespace SportShop.CustomerWeb.Services;

public class DbStatus
{
    readonly string _connectionString;

    static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(15);
    readonly object _lock = new();
    bool _lastResult;
    long _checkedAtMs = long.MinValue / 2;

    public DbStatus(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Default") ?? "";
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    // Kiểm tra kết nối MySQL (ConnectionStrings:Default trong appsettings.json), giữ kết quả 15 giây để không mở kết nối ở mỗi request.
    public bool IsConnected
    {
        get
        {
            if (!IsConfigured) return false;
            lock (_lock)
            {
                if (Environment.TickCount64 - _checkedAtMs < (long)CacheFor.TotalMilliseconds) return _lastResult;
                try
                {
                    var b = new MySqlConnectionStringBuilder(_connectionString) { ConnectionTimeout = 3 };
                    using var c = new MySqlConnection(b.ConnectionString);
                    c.Open();
                    _lastResult = true;
                }
                catch
                {
                    _lastResult = false;
                }
                _checkedAtMs = Environment.TickCount64;
                return _lastResult;
            }
        }
    }
}
