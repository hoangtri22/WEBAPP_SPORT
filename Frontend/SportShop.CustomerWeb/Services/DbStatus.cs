namespace SportShop.CustomerWeb.Services;

/// <summary>
/// Cho biết web hiện có kết nối được cơ sở dữ liệu MySQL hay chưa.
/// Giao diện dùng cờ này để quyết định: chưa có DB thì bấm chức năng chỉ hiện thông báo,
/// có DB thì chức năng chạy thật (không hiện thông báo nữa).
/// </summary>
public class DbStatus
{
    readonly string _connectionString;

    public DbStatus(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Default") ?? "";
    }

    /// <summary>Đã cấu hình chuỗi kết nối hay chưa.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    /// <summary>
    /// Có dùng được cơ sở dữ liệu hay không.
    /// TODO (khi nối MySQL): thử mở kết nối thật tới MySQL ở đây và trả về true nếu thành công.
    /// Hiện chưa có lớp dữ liệu nên luôn trả về false, để các nút không bị mở nhầm khi chưa có gì phía sau.
    /// </summary>
    public bool IsConnected => false;
}
