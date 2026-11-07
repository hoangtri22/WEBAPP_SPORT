namespace SportShop.Admin.Models;

// Dữ liệu trang lỗi chung.
public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
