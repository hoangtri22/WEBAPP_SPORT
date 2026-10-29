using System.Security.Cryptography;
using System.Text;
using Dapper;
using MySqlConnector;
using SportShop.CustomerWeb.Models;

namespace SportShop.CustomerWeb.Services;

public class CustomerStore(IConfiguration config)
{
    readonly string _cs = config.GetConnectionString("Default") ?? "";

    MySqlConnection Open() => new(_cs);

    static bool SamePassword(string stored, string entered)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(entered));


    // Đăng nhập: tìm TAI_KHOAN theo tên đăng nhập hoặc email (JOIN VAI_TRO, KHACH_HANG), so mật khẩu bằng SamePassword(); chỉ nhận tài khoản khách đang hoạt động.
    public (int? CustomerId, string? Error) Login(string username, string password)
    {
        using var c = Open();
        var r = c.QueryFirstOrDefault<LoginRow>("""
            SELECT tk.mat_khau AS Password, tk.trang_thai AS AccountStatus, vt.ten_vai_tro AS Role,
                   kh.ma_khach_hang AS CustomerId
            FROM TAI_KHOAN tk
            JOIN VAI_TRO vt ON vt.ma_vai_tro = tk.ma_vai_tro
            LEFT JOIN KHACH_HANG kh ON kh.ma_tai_khoan = tk.ma_tai_khoan
            WHERE tk.ten_dang_nhap = @u OR tk.email = @u
            LIMIT 1
            """, new { u = username.Trim() });

        if (r == null || !SamePassword(r.Password, password))
            return (null, "Sai tên đăng nhập hoặc mật khẩu.");
        if (r.AccountStatus != "hoat_dong")
            return (null, "Tài khoản đã bị khóa. Vui lòng liên hệ cửa hàng để được hỗ trợ.");
        if (r.CustomerId == null)
            return (null, "Đây là tài khoản nhân viên, vui lòng đăng nhập ở trang quản trị.");
        return (r.CustomerId, null);
    }

    // Đăng ký: kiểm tra trùng tên đăng nhập / email trong TAI_KHOAN, rồi trong một transaction thêm TAI_KHOAN (vai trò "Khách hàng") và KHACH_HANG.
    public Dictionary<string, string> Register(string username, string email, string phone, string password)
    {
        var errors = new Dictionary<string, string>();
        using var c = Open();
        c.Open();
        if (c.ExecuteScalar<int>("SELECT COUNT(*) FROM TAI_KHOAN WHERE ten_dang_nhap = @username", new { username }) > 0)
            errors["username"] = "Tên đăng nhập đã được sử dụng.";
        if (c.ExecuteScalar<int>("SELECT COUNT(*) FROM TAI_KHOAN WHERE email = @email", new { email }) > 0)
            errors["email"] = "Email đã được sử dụng.";
        if (errors.Count > 0) return errors;

        using var tx = c.BeginTransaction();
        var accountId = c.ExecuteScalar<long>("""
            INSERT INTO TAI_KHOAN (ten_dang_nhap, email, so_dien_thoai, mat_khau, ma_vai_tro, trang_thai)
            VALUES (@username, @email, @phone, @password,
                    (SELECT ma_vai_tro FROM VAI_TRO WHERE ten_vai_tro = 'Khách hàng'), 'hoat_dong');
            SELECT LAST_INSERT_ID();
            """, new { username, email, phone, password }, tx);
        c.Execute("""
            INSERT INTO KHACH_HANG (ma_tai_khoan, ho_lot, ten, so_dien_thoai) VALUES (@accountId, '', @username, @phone)
            """, new { accountId, username, phone }, tx);
        tx.Commit();
        return errors;
    }


    CustomerProfile? _current;
    // Hồ sơ khách đang đăng nhập theo customer_id trong Session (gọi GetProfile(), giữ lại trong request).
    public CustomerProfile? Current(ISession session)
    {
        var id = session.GetInt32("customer_id");
        return id == null ? null : _current ??= GetProfile(id.Value);
    }

    // Đọc hồ sơ khách từ KHACH_HANG + TAI_KHOAN: họ tên, email, SĐT, hạng thành viên (MemberTier), điểm và số đơn trong DON_HANG.
    public CustomerProfile? GetProfile(int customerId)
    {
        using var c = Open();
        var r = c.QueryFirstOrDefault<ProfileRow>("""
            SELECT kh.ho_lot AS LastName, kh.ten AS FirstName, tk.ten_dang_nhap AS Username, tk.email AS Email,
                   COALESCE(kh.so_dien_thoai, tk.so_dien_thoai) AS Phone, kh.ngay_sinh AS Birth, kh.gioi_tinh AS Gender,
                   kh.hang_thanh_vien AS Tier, kh.tong_chi_tieu AS Spent, kh.diem_tich_luy AS Points,
                   (SELECT COUNT(*) FROM DON_HANG d WHERE d.ma_khach_hang = kh.ma_khach_hang) AS Orders
            FROM KHACH_HANG kh LEFT JOIN TAI_KHOAN tk ON tk.ma_tai_khoan = kh.ma_tai_khoan
            WHERE kh.ma_khach_hang = @customerId
            """, new { customerId });
        if (r == null) return null;

        string full = $"{r.LastName} {r.FirstName}".Trim();
        var words = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string initials = words.Length == 0 ? "?" : (words.Length == 1 ? words[0][..1] : words[0][..1] + words[^1][..1]).ToUpper();
        string tier = MemberTier.Label(MemberTier.Effective(r.Tier, r.Spent));
        return new CustomerProfile(full, initials, r.Username ?? "", r.Email ?? "", r.Phone ?? "",
            r.Birth?.ToString("yyyy-MM-dd") ?? "", r.Gender ?? "", tier, (int)r.Orders, r.Points);
    }

    // Lấy toàn bộ sở thích (SO_THICH) và các sở thích khách đã chọn (KHACH_HANG_SO_THICH).
    public (List<string> All, HashSet<string> Selected) Interests(int customerId)
    {
        using var c = Open();
        var all = c.Query<string>("SELECT ten_so_thich FROM SO_THICH ORDER BY ma_so_thich").ToList();
        var mine = c.Query<string>("""
            SELECT st.ten_so_thich FROM KHACH_HANG_SO_THICH x JOIN SO_THICH st ON st.ma_so_thich = x.ma_so_thich
            WHERE x.ma_khach_hang = @customerId
            """, new { customerId }).ToHashSet();
        return (all, mine);
    }

    // Cập nhật hồ sơ: kiểm tra email trùng, rồi trong transaction cập nhật KHACH_HANG, TAI_KHOAN và ghi lại KHACH_HANG_SO_THICH.
    public string? UpdateProfile(int customerId, string fullName, string email, string phone, DateTime? birth, string gender, IEnumerable<string> interests)
    {
        var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string first = words[^1], last = string.Join(' ', words[..^1]);

        using var c = Open();
        c.Open();
        int accountId = c.ExecuteScalar<int>("SELECT ma_tai_khoan FROM KHACH_HANG WHERE ma_khach_hang = @customerId", new { customerId });
        if (c.ExecuteScalar<int>("SELECT COUNT(*) FROM TAI_KHOAN WHERE email = @email AND ma_tai_khoan <> @accountId", new { email, accountId }) > 0)
            return "Email đã được tài khoản khác sử dụng.";

        using var tx = c.BeginTransaction();
        c.Execute("""
            UPDATE KHACH_HANG SET ho_lot = @last, ten = @first, so_dien_thoai = @phone, ngay_sinh = @birth, gioi_tinh = @gender
            WHERE ma_khach_hang = @customerId
            """, new { last, first, phone, birth, gender, customerId }, tx);
        c.Execute("UPDATE TAI_KHOAN SET email = @email, so_dien_thoai = @phone WHERE ma_tai_khoan = @accountId",
            new { email, phone, accountId }, tx);
        c.Execute("DELETE FROM KHACH_HANG_SO_THICH WHERE ma_khach_hang = @customerId", new { customerId }, tx);
        foreach (var name in interests.Distinct())
            c.Execute("""
                INSERT INTO KHACH_HANG_SO_THICH (ma_khach_hang, ma_so_thich)
                SELECT @customerId, ma_so_thich FROM SO_THICH WHERE ten_so_thich = @name
                """, new { customerId, name }, tx);
        tx.Commit();
        return null;
    }


    // Sản phẩm nổi bật cho trang chủ (Views/Home/Index.cshtml): các sản phẩm mới nhất còn hàng từ SAN_PHAM, LOAI_SP, BIEN_THE_SP.
    public List<StoreProduct> FeaturedProducts(int count)
    {
        using var c = Open();
        var vi = new System.Globalization.CultureInfo("vi-VN");
        return c.Query<(int Id, string Name, string Type, decimal? Price, string? Image)>("""
            SELECT sp.ma_san_pham, sp.ten_san_pham, l.ten_loai, MIN(bt.gia_ban),
                   (SELECT b2.hinh_anh FROM BIEN_THE_SP b2 WHERE b2.ma_san_pham = sp.ma_san_pham AND b2.hinh_anh IS NOT NULL
                    ORDER BY b2.trang_thai = 'an', b2.ma_bien_the LIMIT 1) AS hinh_anh
            FROM SAN_PHAM sp
            JOIN LOAI_SP l ON l.ma_loai = sp.ma_loai
            LEFT JOIN BIEN_THE_SP bt ON bt.ma_san_pham = sp.ma_san_pham AND bt.trang_thai = 'hien'
            WHERE sp.trang_thai = 'hien'
            GROUP BY sp.ma_san_pham, sp.ten_san_pham, l.ten_loai
            HAVING IFNULL(SUM(bt.so_luong_ton), 0) > 0
            ORDER BY sp.ma_san_pham DESC
            LIMIT @count
            """, new { count })
            .Select(p => new StoreProduct(p.Id, p.Name, p.Type,
                p.Price is decimal v ? string.Format(vi, "{0:N0}₫", v) : "Liên hệ", "", "", p.Image ?? ""))
            .ToList();
    }


    // Đổi mật khẩu: kiểm tra mật khẩu hiện tại trong TAI_KHOAN rồi cập nhật mật khẩu mới.
    public string? ChangePassword(int customerId, string currentPassword, string newPassword)
    {
        using var c = Open();
        var r = c.QueryFirstOrDefault<PasswordRow>("""
            SELECT tk.ma_tai_khoan AS AccountId, tk.mat_khau AS Password
            FROM KHACH_HANG kh JOIN TAI_KHOAN tk ON tk.ma_tai_khoan = kh.ma_tai_khoan
            WHERE kh.ma_khach_hang = @customerId
            """, new { customerId });
        if (r == null) return "Không tìm thấy tài khoản.";
        if (!SamePassword(r.Password, currentPassword)) return "Mật khẩu hiện tại không đúng.";
        c.Execute("UPDATE TAI_KHOAN SET mat_khau = @newPassword WHERE ma_tai_khoan = @accountId", new { newPassword, accountId = r.AccountId });
        return null;
    }


    // Danh sách sản phẩm đang hiện trong SAN_PHAM (dùng kiểm tra mã sản phẩm khi gửi phản hồi).
    public List<(int Id, string Name)> Products()
    {
        using var c = Open();
        return c.Query<(int, string)>("SELECT ma_san_pham, ten_san_pham FROM SAN_PHAM WHERE trang_thai = 'hien' ORDER BY ten_san_pham").ToList();
    }

    // Sản phẩm khách đã mua trong các đơn "da_giao" (DON_HANG, CHI_TIET_DON_HANG, BIEN_THE_SP, SAN_PHAM).
    public List<(int Id, string Name)> PurchasedProducts(int customerId)
    {
        using var c = Open();
        return c.Query<(int, string)>("""
            SELECT DISTINCT sp.ma_san_pham, sp.ten_san_pham
            FROM DON_HANG dh
            JOIN CHI_TIET_DON_HANG ct ON ct.ma_don_hang = dh.ma_don_hang
            JOIN BIEN_THE_SP bt ON bt.ma_bien_the = ct.ma_bien_the
            JOIN SAN_PHAM sp ON sp.ma_san_pham = bt.ma_san_pham
            WHERE dh.ma_khach_hang = @customerId AND dh.trang_thai_don_hang = 'da_giao'
            ORDER BY sp.ten_san_pham
            """, new { customerId }).ToList();
    }

    // Thêm phản hồi đánh giá (loai = danh_gia, trạng thái moi) vào bảng PHAN_HOI.
    public void SendFeedback(int customerId, int productId, int stars, string content)
    {
        using var c = Open();
        c.Execute("""
            INSERT INTO PHAN_HOI (ma_khach_hang, ma_san_pham, loai, so_sao, noi_dung, trang_thai, an_hien)
            VALUES (@customerId, @productId, 'danh_gia', @stars, @content, 'moi', 1)
            """, new { customerId, productId, stars, content });
    }

    // Lịch sử phản hồi của khách từ PHAN_HOI, kèm câu trả lời mới nhất trong TRA_LOI_PHAN_HOI.
    public List<FeedbackRow> Feedbacks(int customerId)
    {
        using var c = Open();
        return c.Query<FeedbackDbRow>("""
            SELECT ph.ma_phan_hoi AS Id, COALESCE(sp.ten_san_pham, ph.tieu_de, 'Phản hồi chung') AS Product,
                   ph.noi_dung AS Text, ph.ngay_gui AS SentAt, ph.trang_thai AS Status,
                   (SELECT t.noi_dung FROM TRA_LOI_PHAN_HOI t WHERE t.ma_phan_hoi = ph.ma_phan_hoi
                     ORDER BY t.ma_tra_loi DESC LIMIT 1) AS Reply
            FROM PHAN_HOI ph LEFT JOIN SAN_PHAM sp ON sp.ma_san_pham = ph.ma_san_pham
            WHERE ph.ma_khach_hang = @customerId
            ORDER BY ph.ma_phan_hoi DESC
            """, new { customerId })
            .Select(r =>
            {
                var (status, tone) = r.Status switch
                {
                    // Đã có câu trả lời của cửa hàng thì hiện "Đã trả lời" (giống trang quản trị), kể cả khi cột trang_thai còn ghi "đang xử lý"
                    _ when r.Status == "da_tra_loi" || !string.IsNullOrEmpty(r.Reply) => ("Đã trả lời", "success"),
                    "dang_xu_ly" => ("Đang xử lý", "warning"),
                    _ => ("Mới", "info"),
                };
                return new FeedbackRow($"PH{r.Id:D4}", r.Product, r.Text, r.SentAt.ToString("dd/MM/yyyy"), status, tone, r.Reply);
            }).ToList();
    }


    // Khảo sát đã gửi đến khách (KHAO_SAT_KHACH_HANG + KHAO_SAT), kèm số câu hỏi và cờ còn hạn.
    public List<SurveyItem> Surveys(int customerId)
    {
        using var c = Open();
        return c.Query<SurveyItem>("""
            SELECT ks.ma_khao_sat AS Id, ks.tieu_de AS Title, COALESCE(ks.mo_ta, '') AS Description,
                   ks.ngay_ket_thuc AS Deadline, k.trang_thai AS Status, k.ngay_hoan_thanh AS CompletedAt,
                   ks.trang_thai = 'da_gui' AND NOW() <= ks.ngay_ket_thuc AS IsOpenFlag,
                   (SELECT COUNT(*) FROM CAU_HOI_KHAO_SAT q WHERE q.ma_khao_sat = ks.ma_khao_sat) AS QuestionCount
            FROM KHAO_SAT_KHACH_HANG k JOIN KHAO_SAT ks ON ks.ma_khao_sat = k.ma_khao_sat
            WHERE k.ma_khach_hang = @customerId AND ks.trang_thai <> 'nhap'
            ORDER BY (k.trang_thai = 'hoan_thanh'), ks.ngay_ket_thuc
            """, new { customerId }).ToList();
    }

    // Câu hỏi (CAU_HOI_KHAO_SAT) và lựa chọn (LUA_CHON_CAU_HOI) của một khảo sát.
    public List<SurveyQ> Questions(int surveyId)
    {
        using var c = Open();
        var options = c.Query<(int QuestionId, int Id, string Text)>("""
            SELECT lc.ma_cau_hoi, lc.ma_lua_chon, lc.noi_dung FROM LUA_CHON_CAU_HOI lc
            JOIN CAU_HOI_KHAO_SAT q ON q.ma_cau_hoi = lc.ma_cau_hoi
            WHERE q.ma_khao_sat = @surveyId ORDER BY lc.ma_lua_chon
            """, new { surveyId }).ToLookup(o => o.QuestionId, o => new SurveyOption(o.Id, o.Text));
        return c.Query<(int Id, string Text, string Type, bool Required)>("""
            SELECT ma_cau_hoi, noi_dung, loai_cau_hoi, bat_buoc FROM CAU_HOI_KHAO_SAT
            WHERE ma_khao_sat = @surveyId ORDER BY thu_tu, ma_cau_hoi
            """, new { surveyId })
            .Select(q => new SurveyQ(q.Id, q.Text,
                q.Type switch { "mot_lua_chon" => "one", "nhieu_lua_chon" => "many", "thang_diem" => "scale", _ => "text" },
                q.Required, options[q.Id].ToList())).ToList();
    }

    // Nộp khảo sát: kiểm tra còn hạn và câu bắt buộc; trong transaction ghi TRA_LOI_KHAO_SAT và đánh dấu KHAO_SAT_KHACH_HANG là hoan_thanh.
    public string? SubmitSurvey(int surveyId, int customerId, Dictionary<int, (List<int> Options, string? Text)> answers)
    {
        var survey = Surveys(customerId).FirstOrDefault(s => s.Id == surveyId);
        if (survey == null) return "Khảo sát này không được gửi đến tài khoản của bạn.";
        if (survey.Status == "hoan_thanh") return "Bạn đã hoàn thành khảo sát này rồi.";
        if (!survey.IsOpen) return "Khảo sát đã hết hạn.";

        var questions = Questions(surveyId);
        foreach (var q in questions.Where(q => q.Required))
            if (!answers.TryGetValue(q.Id, out var a) || (a.Options.Count == 0 && string.IsNullOrWhiteSpace(a.Text)))
                return "Vui lòng trả lời đầy đủ các câu hỏi bắt buộc.";

        using var c = Open();
        c.Open();
        using var tx = c.BeginTransaction();
        foreach (var q in questions)
        {
            if (!answers.TryGetValue(q.Id, out var a)) continue;
            var valid = a.Options.Where(o => q.Options.Any(x => x.Id == o)).ToList();
            foreach (var o in valid)
                c.Execute("""
                    INSERT INTO TRA_LOI_KHAO_SAT (ma_khao_sat, ma_khach_hang, ma_cau_hoi, ma_lua_chon) VALUES (@surveyId, @customerId, @qid, @o)
                    """, new { surveyId, customerId, qid = q.Id, o }, tx);
            if (valid.Count == 0 && !string.IsNullOrWhiteSpace(a.Text))
                c.Execute("""
                    INSERT INTO TRA_LOI_KHAO_SAT (ma_khao_sat, ma_khach_hang, ma_cau_hoi, noi_dung_tra_loi) VALUES (@surveyId, @customerId, @qid, @text)
                    """, new { surveyId, customerId, qid = q.Id, text = a.Text!.Trim() }, tx);
        }
        c.Execute("""
            UPDATE KHAO_SAT_KHACH_HANG SET trang_thai = 'hoan_thanh', ngay_hoan_thanh = NOW()
            WHERE ma_khao_sat = @surveyId AND ma_khach_hang = @customerId
            """, new { surveyId, customerId }, tx);
        tx.Commit();
        return null;
    }

    class PasswordRow { public int AccountId { get; set; } public string Password { get; set; } = ""; }
    class LoginRow { public string Password { get; set; } = ""; public string AccountStatus { get; set; } = ""; public string Role { get; set; } = ""; public int? CustomerId { get; set; } }
    class ProfileRow { public string LastName { get; set; } = ""; public string FirstName { get; set; } = ""; public string? Username { get; set; } public string? Email { get; set; } public string? Phone { get; set; } public DateTime? Birth { get; set; } public string? Gender { get; set; } public string? Tier { get; set; } public decimal Spent { get; set; } public int Points { get; set; } public long Orders { get; set; } }
    class FeedbackDbRow { public int Id { get; set; } public string Product { get; set; } = ""; public string Text { get; set; } = ""; public DateTime SentAt { get; set; } public string Status { get; set; } = ""; public string? Reply { get; set; } }
}
