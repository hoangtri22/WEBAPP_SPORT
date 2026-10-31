namespace SportShop.CustomerWeb.Models;

public record HomeVm(bool Live, List<ShopCategory> Categories, List<StoreCategory> SampleCategories, List<StoreProduct> Featured);

public record ProfileVm(bool Live, CustomerProfile Me, List<string> AllInterests, HashSet<string> Chosen);

public record FeedbackPageVm(bool Live, List<(int Id, string Name)> Products, List<FeedbackRow> Feedbacks);

/// <summary>Ô nhập từ khóa tìm sản phẩm dùng lại ở nhiều nơi (thanh tìm kiếm đầu trang, bộ lọc cửa hàng, tìm kiếm nâng cao): Views/Shared/_SearchInput.cshtml.</summary>
public record SearchInputVm(string? Value, string Placeholder, string? Id = null, int IconSize = 20);

/// <summary>Dữ liệu cho khung tìm kiếm nâng cao ở trang cửa hàng: Views/Shared/_AdvancedSearch.cshtml.</summary>
public record AdvancedSearchVm(ProductQuery Query, List<ShopCategory> Categories, List<string> Sizes, List<string> Colors);
