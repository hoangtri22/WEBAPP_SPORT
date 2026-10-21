using MySqlConnector;

namespace SportShop.CustomerWeb.Services;

/// <summary>
/// Cho biết web hiện có kết nối được cơ sở dữ liệu MySQL hay chưa.
/// Giao diện dùng cờ này để quyết định: chưa có DB thì bấm chức năng chỉ hiện thông báo,
/// có DB thì chức năng chạy thật (không hiện thông báo nữa).
/// </summary>
public class DbStatus
{
    readonly string _connectionString;

    // Kết quả kiểm tra được nhớ trong 15 giây để không phải mở kết nối thử ở mỗi lần tải trang
    static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(15);
    readonly object _lock = new();
    bool _lastResult;
    DateTime _checkedAt = DateTime.MinValue;

    public DbStatus(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Default") ?? "";
    }

    /// <summary>Đã cấu hình chuỗi kết nối hay chưa.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    /// <summary>
    /// Có dùng được cơ sở dữ liệu hay không: đã điền chuỗi kết nối và mở thử kết nối tới MySQL thành công.
    /// </summary>
    public bool IsConnected
    {
        get
        {
            if (!IsConfigured) return false;
            lock (_lock)
            {
                if (DateTime.UtcNow - _checkedAt < CacheFor) return _lastResult;
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
                _checkedAt = DateTime.UtcNow;
                return _lastResult;
            }
        }
    }
}
