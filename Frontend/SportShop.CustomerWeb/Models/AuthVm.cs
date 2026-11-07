namespace SportShop.CustomerWeb.Models;

// Dữ liệu trang đăng nhập, đăng ký (Views/Account/Auth.cshtml).
public class AuthVm
{
    public bool IsRegister { get; init; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public Dictionary<string, string> Errors { get; } = new();
    public string? Notice { get; set; }
    public bool NoticeIsError { get; set; }
}
