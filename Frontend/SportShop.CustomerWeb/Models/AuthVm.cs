namespace SportShop.CustomerWeb.Models;

/// <summary>Dữ liệu cho trang đăng nhập / đăng ký (dùng chung một view).</summary>
public class AuthVm
{
    public bool IsRegister { get; init; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    /// <summary>Lỗi theo từng ô nhập: khóa là username / email / phone / password.</summary>
    public Dictionary<string, string> Errors { get; } = new();
    /// <summary>Thông báo hiện phía trên form (nếu có).</summary>
    public string? Notice { get; set; }
    public bool NoticeIsError { get; set; }
}
