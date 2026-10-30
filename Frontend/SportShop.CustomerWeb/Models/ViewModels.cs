namespace SportShop.CustomerWeb.Models;

public record HomeVm(bool Live, List<ShopCategory> Categories, List<StoreCategory> SampleCategories, List<StoreProduct> Featured);

public record ProfileVm(bool Live, CustomerProfile Me, List<string> AllInterests, HashSet<string> Chosen);

public record FeedbackPageVm(bool Live, List<(int Id, string Name)> Products, List<FeedbackRow> Feedbacks);
