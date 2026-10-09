namespace SportShop.Admin.Models;

/// <summary>Dữ liệu mẫu (chưa nối database) để dựng giao diện.</summary>
public static class SampleData
{
    static DateTime D(int daysAgo, int h = 9, int m = 0) => DateTime.Today.AddDays(-daysAgo).AddHours(h).AddMinutes(m);

    public static readonly string[] Categories = ["Quần áo", "Giày", "Vợt cầu lông", "Phụ kiện"];

    public static readonly List<Product> Products =
    [
        new(1, "Quần áo", "Áo thun thể thao Dri-Fit", "cái", true, "Vải thoáng khí, thấm hút mồ hôi."),
        new(2, "Quần áo", "Quần short chạy bộ", "cái", true, "Quần short 2 lớp, có túi khóa kéo."),
        new(3, "Giày", "Giày chạy bộ Pegasus", "đôi", true, "Đế êm, phù hợp chạy đường dài."),
        new(4, "Giày", "Giày cầu lông Yonex Power Cushion", "đôi", true, "Đế chống trơn, hỗ trợ di chuyển nhanh."),
        new(5, "Vợt cầu lông", "Vợt Yonex Astrox 88D", "cây", true, "Nặng đầu, thiên công. Độ cứng: Cứng. Điểm cân bằng: 295mm."),
        new(6, "Vợt cầu lông", "Vợt Lining Axforce 80", "cây", true, "Cân bằng, linh hoạt. Độ cứng: Trung bình."),
        new(7, "Phụ kiện", "Túi đựng vợt 6 cây", "cái", true, "Túi 3 ngăn, chống nước."),
        new(8, "Phụ kiện", "Vớ thể thao cổ cao", "đôi", false, "Cotton co giãn 4 chiều."),
    ];

    public static readonly List<Variant> Variants =
    [
        new(1, 1, "M", "Đen", 199_000, 120_000, 40), new(2, 1, "L", "Đen", 199_000, 120_000, 35),
        new(3, 1, "L", "Trắng", 199_000, 120_000, 4), new(4, 1, "XL", "Trắng", 199_000, 120_000, 22),
        new(5, 2, "M", "Xám", 159_000, 90_000, 30), new(6, 2, "L", "Xám", 159_000, 90_000, 18),
        new(7, 3, "40", "Xanh", 2_490_000, 1_700_000, 8), new(8, 3, "41", "Xanh", 2_490_000, 1_700_000, 3),
        new(9, 3, "42", "Đen", 2_490_000, 1_700_000, 12),
        new(10, 4, "41", "Trắng", 1_890_000, 1_300_000, 10), new(11, 4, "42", "Trắng", 1_890_000, 1_300_000, 6),
        new(12, 5, "3U", "Đen/Vàng", 3_290_000, 2_400_000, 9), new(13, 5, "4U", "Đen/Vàng", 3_290_000, 2_400_000, 5),
        new(14, 6, "4U", "Đỏ", 2_790_000, 2_000_000, 14),
        new(15, 7, null, "Đen", 450_000, 280_000, 25),
        new(16, 8, null, "Trắng", 49_000, 25_000, 120),
    ];

    public static readonly List<Employee> Employees =
    [
        new(1, "Trần Minh", "Quân", "Nam", new(1988, 5, 20), "Quản lý", 18_000_000, true, "quan.tm", "0908111222"),
        new(2, "Lê Thu", "Hà", "Nữ", new(1996, 9, 3), "Nhân viên bán hàng", 9_000_000, true, "ha.lt", "0908222333"),
        new(3, "Phạm Đức", "Anh", "Nam", new(1995, 1, 15), "Nhân viên kho", 9_500_000, true, "anh.pd", "0908333444"),
        new(4, "Nguyễn Thị", "Mai", "Nữ", new(1998, 11, 28), "Chăm sóc khách hàng", 9_000_000, true, "mai.nt", "0908444555"),
        new(5, "Võ Hoàng", "Long", "Nam", new(1994, 7, 9), "Nhân viên giao hàng", 8_500_000, true, "long.vh", "0908555666"),
        new(6, "Đặng Kim", "Ngân", "Nữ", new(1997, 3, 12), "Nhân viên bán hàng", 9_000_000, false, "ngan.dk", "0908666777"),
    ];

    public static readonly List<Customer> Customers =
    [
        new() { Id = 1, LastName = "Nguyễn Văn", FirstName = "An", Gender = "Nam", Birth = new(1999, 3, 12), Phone = "0901234567", Email = "an.nguyen@gmail.com", Username = "annguyen", Interests = ["Cầu lông", "Chạy bộ"], Addresses = ["12 Nguyễn Trãi, Q.5, TP.HCM (mặc định)", "Công ty ABC, 99 Lê Lợi, Q.1, TP.HCM"] },
        new() { Id = 2, LastName = "Huỳnh Ái", FirstName = "Vy", Gender = "Nữ", Birth = new(2001, 8, 22), Phone = "0912345678", Email = "vy.huynh@gmail.com", Username = "vyhuynh", Interests = ["Cầu lông"], Addresses = ["45 Võ Văn Tần, Q.3, TP.HCM (mặc định)"] },
        new() { Id = 3, LastName = "Trần Quốc", FirstName = "Bảo", Gender = "Nam", Birth = new(1985, 12, 5), Phone = "0987654321", Email = "bao.tran@gmail.com", Username = "baotran", Interests = ["Bóng đá", "Chạy bộ"], Addresses = ["8 Hai Bà Trưng, Q.1, TP.HCM (mặc định)"] },
        new() { Id = 4, LastName = "Lê Thị", FirstName = "Cẩm", Gender = "Nữ", Birth = new(1992, 6, 30), Phone = "0933111222", Email = "cam.le@gmail.com", Username = "camle", Interests = ["Yoga", "Chạy bộ"], Addresses = ["77 Cách Mạng Tháng 8, Q.10, TP.HCM (mặc định)"] },
        new() { Id = 5, LastName = "Phạm Gia", FirstName = "Huy", Gender = "Nam", Birth = new(2005, 2, 14), Phone = "0944222333", Email = "huy.pham@gmail.com", Username = "huypham", Interests = ["Cầu lông", "Bóng đá"], Addresses = ["21 Lý Thường Kiệt, Q.11, TP.HCM (mặc định)"] },
        new() { Id = 6, LastName = "Võ Thanh", FirstName = "Tùng", Gender = "Nam", Birth = new(1978, 10, 1), Phone = "0955333444", Email = "tung.vo@gmail.com", Username = "tungvo", Locked = true, Interests = ["Bóng đá"], Addresses = ["5 Trần Hưng Đạo, Q.5, TP.HCM (mặc định)"] },
        new() { Id = 7, LastName = "Đặng Mỹ", FirstName = "Linh", Gender = "Nữ", Birth = new(2000, 4, 18), Phone = "0966444555", Email = "linh.dang@gmail.com", Username = "linhdang", Interests = ["Cầu lông", "Yoga"], Addresses = ["30 Phan Xích Long, Phú Nhuận, TP.HCM (mặc định)"] },
    ];

    static Order O(string code, int cust, int? emp, int daysAgo, string status, string pay, string method, string carrier, string track, decimal ship, decimal disc, string? voucher, params (int v, int q)[] lines)
        => new(code, cust, emp, D(daysAgo, 8 + daysAgo % 9, (daysAgo * 7) % 60), status, pay, method, carrier, track, ship, disc, voucher,
               lines.Select(l => new OrderLine(l.v, l.q)).ToList());

    public static readonly List<Order> Orders =
    [
        O("HD00001", 1, 2, 150, "hoan_thanh", "da_thanh_toan", "Tiền mặt", "Tại quầy", "", 0, 0, null, (12, 1), (15, 1)),
        O("HD00002", 3, null, 120, "hoan_thanh", "da_thanh_toan", "Chuyển khoản", "GHTK", "GHTK001", 30_000, 0, null, (9, 2)),
        O("HD00003", 2, null, 95, "hoan_thanh", "da_thanh_toan", "Ví điện tử", "GHN", "GHN002", 25_000, 99_500, "KHAITRUONG", (1, 2), (5, 1)),
        O("HD00004", 1, null, 70, "hoan_thanh", "da_thanh_toan", "Thẻ", "GHN", "GHN003", 0, 0, null, (10, 1), (14, 1)),
        O("HD00005", 4, 2, 58, "hoan_thanh", "da_thanh_toan", "Tiền mặt", "Tại quầy", "", 0, 0, null, (7, 1), (6, 1)),
        O("HD00006", 5, null, 40, "hoan_thanh", "da_thanh_toan", "Chuyển khoản", "GHTK", "GHTK004", 30_000, 0, null, (2, 1), (16, 3)),
        O("HD00007", 1, null, 33, "hoan_thanh", "da_thanh_toan", "Ví điện tử", "GHN", "GHN005", 0, 329_000, "VIP10", (13, 1)),
        O("HD00008", 7, null, 20, "hoan_thanh", "da_thanh_toan", "Ví điện tử", "GHN", "GHN006", 25_000, 0, null, (11, 1), (3, 1)),
        O("HD00009", 6, null, 14, "da_huy", "chua_thanh_toan", "Chuyển khoản", "GHTK", "", 30_000, 0, null, (8, 1)),
        O("HD00010", 3, null, 9, "hoan_thanh", "da_thanh_toan", "Tiền mặt", "GHN", "GHN007", 30_000, 0, null, (14, 1), (15, 1)),
        O("HD00011", 4, null, 5, "hoan_thanh", "da_thanh_toan", "Thẻ", "GHN", "GHN008", 0, 0, null, (4, 2), (5, 2)),
        O("HD00012", 2, null, 2, "dang_giao", "da_thanh_toan", "Ví điện tử", "GHN", "GHN009", 25_000, 0, null, (14, 1)),
        O("HD00013", 7, null, 1, "dang_chuan_bi", "da_thanh_toan", "Chuyển khoản", "GHTK", "", 30_000, 0, null, (12, 1), (1, 1)),
        O("HD00014", 5, null, 0, "cho_xac_nhan", "chua_thanh_toan", "Tiền mặt", "GHTK", "", 30_000, 0, null, (16, 2), (6, 1)),
        O("HD00015", 1, null, 0, "cho_xac_nhan", "chua_thanh_toan", "Thẻ", "GHN", "", 0, 0, null, (9, 1)),
        O("HD00016", 4, 2, 0, "hoan_thanh", "da_thanh_toan", "Tiền mặt", "Tại quầy", "", 0, 0, null, (2, 1), (5, 1)),
    ];

    public static readonly List<CareNote> CareNotes =
    [
        new(1, 4, "goi_dien", "Hỏi thăm sau khi mua vợt, khách hài lòng. Gợi ý mua thêm cước.", D(30), DateTime.Today.AddDays(3)),
        new(3, 4, "tin_nhan", "Gửi thông tin khuyến mãi giày chạy bộ.", D(12), null),
        new(2, 4, "email", "Xin phản hồi về đơn HD00003.", D(8), DateTime.Today),
        new(7, 2, "gap_truc_tiep", "Tư vấn chọn vợt tại quầy, khách để lại số điện thoại.", D(21), DateTime.Today.AddDays(2)),
    ];

    // Phản hồi của khách (PHAN_HOI + TRA_LOI_PHAN_HOI). Status: moi / dang_xu_ly / da_tra_loi.
    static DateTime Ago(int minutes) => DateTime.Now.AddMinutes(-minutes);

    public static readonly List<FeedbackItem> FeedbackItems =
    [
        new(1, 2, "khieu_nai", null, "Thời gian giao hàng hơi lâu", "Chào shop, mình đặt giày từ thứ hai nhưng đến thứ sáu mới nhận được... Mình mong shop có thể cải thiện phần vận chuyển để trải nghiệm lần sau tốt hơn. Cảm ơn shop!", Ago(10), "HD00012", "moi", true, null, null, null),
        new(2, 5, "danh_gia", 5, "Tư vấn size rất nhiệt tình", "Nhân viên hỗ trợ mình chọn đúng size, sản phẩm rất vừa chân. Sẽ ủng hộ shop lâu dài.", Ago(60), "Giày chạy bộ Pegasus", "moi", true, null, null, null),
        new(3, 4, "gop_y", null, "Mong shop có thêm đồ yoga", "Shop có nhiều đồ chạy bộ rất tốt, mong shop bổ sung thêm quần áo và phụ kiện cho yoga.", Ago(60 * 26), null, "moi", true, null, null, null),
        new(4, 7, "danh_gia", 4, "Áo mặc thoáng, size hơi rộng", "Áo thấm mồ hôi tốt, mặc rất thoáng. Tuy nhiên size L hơi rộng so với bảng size của shop.", Ago(60 * 30), "Áo thun thể thao Dri-Fit", "dang_xu_ly", true, null, null, null),
        new(5, 3, "khieu_nai", null, "Hộp giày bị móp khi nhận hàng", "Giày vẫn nguyên vẹn nhưng hộp bị móp khá nhiều, mong shop đóng gói cẩn thận hơn.", Ago(60 * 50), "HD00010", "moi", true, null, null, null),
        new(6, 1, "danh_gia", 5, "Vợt đánh rất đầm tay", "Vợt cân bằng tốt, giao hàng nhanh, đóng gói kỹ. Rất hài lòng!", Ago(60 * 24 * 4), "Vợt Yonex Astrox 88D", "da_tra_loi", true, "Cảm ơn bạn đã tin tưởng shop! Chúc bạn có những trận cầu thật hay.", Ago(60 * 24 * 3), 4),
        new(7, 6, "khieu_nai", null, "Nội dung vi phạm quy định", "(Nội dung đã bị ẩn do vi phạm quy định cộng đồng.)", Ago(60 * 24 * 7), "HD00009", "da_tra_loi", false, "Shop đã ghi nhận và xử lý hóa đơn bị hủy. Mong bạn thông cảm.", Ago(60 * 24 * 6), 1),
        new(8, 2, "gop_y", null, "Nên có thêm phương thức ví điện tử", "Mình mong shop hỗ trợ thêm nhiều ví điện tử khi thanh toán online.", Ago(60 * 24 * 10), null, "da_tra_loi", true, "Shop đã hỗ trợ thanh toán bằng ví điện tử, bạn thử lại nhé!", Ago(60 * 24 * 9), 4),
    ];

    public static Variant Variant(int id) => Variants.First(v => v.Id == id);
    public static Product Product(int id) => Products.First(p => p.Id == id);
    public static Customer Customer(int id) => Customers.First(c => c.Id == id);
    public static Employee Employee(int id) => Employees.First(e => e.Id == id);
    public static string VariantName(int id)
    {
        var v = Variant(id);
        var extra = string.Join(" / ", new[] { v.Size, v.Color }.Where(s => !string.IsNullOrEmpty(s)));
        return $"{Product(v.ProductId).Name} ({extra})";
    }
}
