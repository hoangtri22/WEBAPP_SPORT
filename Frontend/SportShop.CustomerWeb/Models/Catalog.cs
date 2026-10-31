namespace SportShop.CustomerWeb.Models;

public static class Catalog
{
    public const string ArchivedCategory = "[Đã xóa]";

    /// <summary>Chương trình voucher dành cho khách mới (tên có "bạn mới", "khách mới"...): chỉ dùng được ở lần mua hàng đầu tiên.</summary>
    public static bool IsNewCustomerPromo(string name) =>
        new[] { "bạn mới", "khách mới", "người mới", "thành viên mới" }.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));

    /// <summary>Số lần tối đa mỗi khách được dùng voucher: mã khách mới 1 lần, mã hạng Bạc 2 lần, mã hạng Vàng 3 lần; mã khác trả về null (theo giới hạn đặt ở chương trình).</summary>
    public static int? VoucherUseLimit(bool newCustomerOnly, string? tier) => newCustomerOnly ? 1 : tier switch { "bac" => 2, "vang" => 3, _ => null };
}
