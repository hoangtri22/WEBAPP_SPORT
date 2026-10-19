namespace SportShop.CustomerWeb.Models;

// ============================================================
//  Dữ liệu mẫu để dựng giao diện (chưa có cơ sở dữ liệu).
//  Khi nối MySQL sẽ thay bằng dữ liệu đọc từ database.
// ============================================================

public record StoreProduct(int Id, string Name, string Type, string Price, string OldPrice, string Badge, string Image);

public record StoreCategory(string Name, string Sub, string Css, string Icon);

public record FeedbackRow(string Id, string Product, string Text, string Date, string Status, string Tone);

public record SurveyQuestion(string Title, string Type, string[] Options);

/// <summary>Thông tin khách hàng đang đăng nhập (mẫu).</summary>
public record CustomerProfile(string FullName, string Initials, string Username, string Email, string Phone, string Birth, string Gender, string Tier, int Orders, int Points);

public static class StoreData
{
    // Ảnh minh họa lấy từ Unsplash (giống bản thiết kế Figma)
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
        new("PH0214", "Áo Adidas Own The Run", "Chất liệu thoáng, nhưng mình cần hỗ trợ đổi size.", "18/05/2025", "Đang xử lý", "warning"),
        new("PH0189", "Túi Nike Brasilia 9.5", "Ngăn đựng rộng và đường may chắc chắn.", "03/05/2025", "Mới", "info"),
    ];

    // Bốn dạng câu hỏi của khảo sát: một lựa chọn, nhiều lựa chọn, thang điểm, văn bản (theo PDF)
    public static readonly List<SurveyQuestion> SurveyQuestions =
    [
        new("Bạn thường mua sắm đồ thể thao với tần suất nào?", "one", ["Hàng tháng", "Mỗi 2–3 tháng", "Mỗi 6 tháng", "Khi có nhu cầu"]),
        new("Bạn quan tâm đến những nhóm sản phẩm nào?", "many", ["Giày chạy bộ", "Quần áo tập luyện", "Dụng cụ thể thao", "Phụ kiện"]),
        new("Mức độ hài lòng của bạn với trải nghiệm mua sắm?", "scale", ["1", "2", "3", "4", "5"]),
        new("Điều gì SPORTSHOP có thể làm tốt hơn?", "text", []),
    ];
}
