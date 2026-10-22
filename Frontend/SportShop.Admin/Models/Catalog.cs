namespace SportShop.Admin.Models;

/// <summary>Quy ước về loại sản phẩm đã xóa.</summary>
public static class Catalog
{
    /// <summary>
    /// Tên loại ẩn dùng để giữ các sản phẩm đã có lịch sử (đơn hàng, phiếu nhập...) khi xóa loại sản phẩm của chúng.
    /// Loại này không hiện ở web admin và web khách hàng; sản phẩm trong đó bị ẩn khỏi cửa hàng nhưng lịch sử mua vẫn đầy đủ.
    /// </summary>
    public const string ArchivedCategory = "[Đã xóa]";
}
