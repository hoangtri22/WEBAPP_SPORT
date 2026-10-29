namespace SportShop.CustomerWeb.Models;

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
