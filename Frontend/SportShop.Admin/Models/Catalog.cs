namespace SportShop.Admin.Models;

public static class Catalog
{
    public const string ArchivedCategory = "[Đã xóa]";

    /// <summary>Chương trình voucher dành cho khách mới (tên có "bạn mới", "khách mới"...): chỉ dùng được ở lần mua hàng đầu tiên.</summary>
    public static bool IsNewCustomerPromo(string name) =>
        new[] { "bạn mới", "khách mới", "người mới", "thành viên mới" }.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
}
