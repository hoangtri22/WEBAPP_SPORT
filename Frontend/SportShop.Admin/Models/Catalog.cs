namespace SportShop.Admin.Models;

// Quy tắc dùng chung về loại sản phẩm lưu trữ và voucher, bản của Web Admin.
public static class Catalog
{
    public const string ArchivedCategory = "[Đã xóa]";

    /// <summary>Chương trình voucher dành cho khách mới (tên có "bạn mới", "khách mới"...): chỉ dùng được ở lần mua hàng đầu tiên.</summary>
    public static bool IsNewCustomerPromo(string name) =>
        new[] { "bạn mới", "khách mới", "người mới", "thành viên mới" }.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));

    /// <summary>Chương trình voucher sinh nhật (tên có "sinh nhật"): chỉ dùng được trong tháng sinh của khách.</summary>
    public static bool IsBirthdayPromo(string name) => name.Contains("sinh nhật", StringComparison.OrdinalIgnoreCase);

    /// <summary>Hôm nay có thuộc tháng sinh của khách không; chưa có ngày sinh thì trả về false.</summary>
    public static bool IsBirthdayMonth(DateTime? birth, DateTime today) => birth?.Month == today.Month;

    /// <summary>Số lần tối đa mỗi khách được dùng voucher: mã khách mới 1 lần, mã hạng Bạc 2 lần, mã hạng Vàng 3 lần; mã khác trả về null (theo giới hạn đặt ở chương trình).</summary>
    public static int? VoucherUseLimit(bool newCustomerOnly, string? tier) => newCustomerOnly ? 1 : tier switch { "bac" => 2, "vang" => 3, _ => null };
}
