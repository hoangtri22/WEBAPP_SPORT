namespace SportShop.Mobile.ViewModels;

public sealed record HomeProduct(string Name, string Type, string Price, string OldPrice, string Badge, string Image)
{
    public bool HasOldPrice => OldPrice.Length > 0;
    public bool HasBadge => Badge.Length > 0;
}

// Sản phẩm mẫu chỉ để dựng giao diện Trang chủ, chưa nối API (theo StoreData của web khách hàng).
public static class HomeSample
{
    public static IReadOnlyList<HomeProduct> Products { get; } =
    [
        new("Nike Air Zoom Pegasus 41", "Giày chạy bộ", "3.290.000₫", "3.690.000₫", "-11%", "img_pegasus.jpg"),
        new("Adidas Ultraboost Light", "Giày luyện tập", "4.190.000₫", "", "Mới", "img_ultraboost.jpg"),
        new("Nike Metcon", "Giày tập gym", "3.500.000₫", "", "Bán chạy", "img_metcon.jpg"),
        new("Nike Mercurial Vapor", "Giày đá bóng", "2.200.000₫", "2.490.000₫", "-12%", "img_mercurial.jpg"),
    ];
}
