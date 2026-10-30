namespace SportShop.CustomerWeb.Models;


public record StoreProduct(int Id, string Name, string Type, string Price, string OldPrice, string Badge, string Image);

public record StoreCategory(string Name, string Sub, string Css, string Icon);

public record FeedbackRow(string Id, string Product, string Text, string Date, string Status, string Tone, string? Reply = null);

public record SurveyQuestion(string Title, string Type, string[] Options);

public record CustomerProfile(string FullName, string Initials, string Username, string Email, string Phone, string Birth, string Gender, string Tier, int Orders, int Points)
{
    public string ShortName => string.Join(' ', FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).TakeLast(2));
}

public class SurveyItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Deadline { get; set; }
    public string Status { get; set; } = "";
    public DateTime? CompletedAt { get; set; }
    public long IsOpenFlag { get; set; }
    public long QuestionCount { get; set; }
    public bool IsOpen => IsOpenFlag == 1;
    public bool Done => Status == "hoan_thanh";
}

public record SurveyOption(int Id, string Text);

public record SurveyQ(int Id, string Title, string Type, bool Required, List<SurveyOption> Options);

public class SurveyPageVm
{
    public List<SurveyItem> Items { get; init; } = [];
    public SurveyItem? Current { get; init; }
    public List<SurveyQ> Questions { get; init; } = [];
    public bool JustDone { get; init; }
    public string? Error { get; init; }
}

public static class StoreData
{
    public const string HeroImage = "https://images.unsplash.com/photo-1739138053507-0f312a938451?crop=entropy&cs=tinysrgb&fit=crop&fm=jpg&q=90&w=1400";
    public const string CampaignImage = "https://images.unsplash.com/photo-1772909849803-e1c298df3fd9?crop=entropy&cs=tinysrgb&fit=crop&fm=jpg&q=85&w=900";
    public const string AuthImage = "https://images.unsplash.com/photo-1771402899305-2066905a74f1?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&q=80&w=1400";

    public static readonly List<StoreProduct> Products =
    [
        new(1, "Nike Air Zoom Pegasus 41", "Giày chạy bộ", "3.290.000₫", "3.690.000₫", "-11%", "https://images.unsplash.com/photo-1739138053507-0f312a938451?crop=entropy&cs=tinysrgb&fit=crop&fm=jpg&q=85&w=900"),
        new(2, "Adidas Ultraboost Light", "Giày luyện tập", "4.190.000₫", "", "Mới", "https://images.unsplash.com/photo-1772909849803-e1c298df3fd9?crop=entropy&cs=tinysrgb&fit=crop&fm=jpg&q=85&w=900"),
        new(3, "Puma Velocity Nitro 3", "Giày chạy bộ", "2.690.000₫", "2.990.000₫", "-10%", "https://images.unsplash.com/photo-1760302318631-a8d342cd4951?crop=entropy&cs=tinysrgb&fit=crop&fm=jpg&q=85&w=900"),
        new(4, "Nike Air Max Pulse", "Giày thời trang", "3.850.000₫", "", "Bán chạy", "https://images.unsplash.com/photo-1739138053507-0f312a938451?crop=entropy&cs=tinysrgb&fit=crop&fm=jpg&q=85&w=900"),
    ];

    public static readonly List<StoreCategory> Categories =
    [
        new("Giày chạy bộ", "86 sản phẩm", "running", "zap"),
        new("Đồ tập nam", "124 sản phẩm", "men", "user"),
        new("Đồ tập nữ", "108 sản phẩm", "women", "sparkles"),
        new("Dụng cụ", "72 sản phẩm", "gear", "activity"),
    ];

    public static readonly CustomerProfile Customer =
        new("Nguyễn Minh Anh", "MA", "minhanh_98", "minhanh98@gmail.com", "0912 345 678", "1998-08-20", "Nữ", "Bạc", 12, 1240);

    public static readonly string[] Interests = ["Chạy bộ", "Cầu lông", "Gym", "Bóng đá", "Yoga"];

    public static readonly List<FeedbackRow> Feedbacks =
    [
        new("PH0238", "Nike Air Zoom Pegasus 41", "Giày nhẹ, đệm êm và rất phù hợp để chạy dài.", "22/05/2025", "Đã trả lời", "success"),
        new("PH0214", "Áo Adidas Own The Run", "Chất liệu thoáng, nhưng mình cần hỗ trợ đổi size.", "18/05/2025", "Chưa trả lời", "warning"),
        new("PH0189", "Túi Nike Brasilia 9.5", "Ngăn đựng rộng và đường may chắc chắn.", "03/05/2025", "Mới", "info"),
    ];

    public static readonly List<SurveyQuestion> SurveyQuestions =
    [
        new("Bạn thường mua sắm đồ thể thao với tần suất nào?", "one", ["Hàng tháng", "Mỗi 2–3 tháng", "Mỗi 6 tháng", "Khi có nhu cầu"]),
        new("Bạn quan tâm đến những nhóm sản phẩm nào?", "many", ["Giày chạy bộ", "Quần áo tập luyện", "Dụng cụ thể thao", "Phụ kiện"]),
        new("Mức độ hài lòng của bạn với trải nghiệm mua sắm?", "scale", ["1", "2", "3", "4", "5"]),
        new("Điều gì SPORTSHOP có thể làm tốt hơn?", "text", []),
    ];
}
