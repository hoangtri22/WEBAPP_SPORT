namespace SportShop.CustomerWeb.Models;

/// <summary>Quy ước về loại sản phẩm đã xóa (do web admin tạo khi xóa loại có sản phẩm đã có lịch sử mua).</summary>
public static class Catalog
{
    /// <summary>Tên loại ẩn chứa các sản phẩm đã xóa nhưng còn lịch sử mua; không hiện trong danh mục cửa hàng.</summary>
    public const string ArchivedCategory = "[Đã xóa]";
}
