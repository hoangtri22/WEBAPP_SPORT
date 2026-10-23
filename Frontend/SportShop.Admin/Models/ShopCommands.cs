using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Http;
using MySqlConnector;

namespace SportShop.Admin.Models;

// ============================================================
//  Ghi dữ liệu vào MySQL cho web admin: thêm, sửa, xóa, đổi trạng thái.
//  (Việc ĐỌC dữ liệu để hiển thị nằm ở ShopData.)
//  Mỗi thao tác chạy trong một transaction: lỗi giữa chừng thì hoàn tác toàn bộ.
//  Lỗi nghiệp vụ (nhập thiếu, không đủ tồn kho...) ném UserError, lỗi MySQL được dịch ra tiếng Việt.
// ============================================================

/// <summary>Kết quả một thao tác ghi: Ok = thành công, Message = nội dung báo cho người dùng.</summary>
public record Result(bool Ok, string Message);

/// <summary>Người đang thao tác (lấy từ cookie đăng nhập), dùng để ghi "nhân viên thực hiện" và chặn tự khóa mình.</summary>
public record Ctx(int EmployeeId, string Username, string Role)
{
    public bool IsManager => Role == "Quản lý";
}

/// <summary>Một lựa chọn trong ô select. Data là thông tin phụ (ví dụ giá và tồn của biến thể) cho JavaScript.</summary>
public record Opt(string Value, string Label, string? Data = null);

/// <summary>Lỗi nghiệp vụ: nội dung được hiện nguyên văn cho người dùng.</summary>
public class UserError(string message) : Exception(message);

/// <summary>Đọc dữ liệu gửi lên từ biểu mẫu.</summary>
public class Form(IFormCollection f)
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public string S(string k) => f[k].ToString().Trim();
    /// <summary>File người dùng chọn tải lên (ví dụ ảnh sản phẩm); null nếu không chọn.</summary>
    public IFormFile? File(string k) => f.Files.GetFile(k);
    public string? N(string k) { var v = S(k); return v.Length == 0 ? null : v; }
    public int I(string k) => int.TryParse(S(k), NumberStyles.Integer, Inv, out var n) ? n : 0;
    public int? NI(string k) => int.TryParse(S(k), NumberStyles.Integer, Inv, out var n) ? n : null;
    public decimal D(string k) => ND(k) ?? 0;
    public decimal? ND(string k) => decimal.TryParse(S(k).Replace(',', '.'), NumberStyles.Number, Inv, out var d) ? d : null;
    public DateTime? Date(string k) => DateTime.TryParse(S(k), Inv, DateTimeStyles.None, out var d) ? d : null;
    public string[] Many(string k) => f[k].Select(v => (v ?? "").Trim()).Where(v => v.Length > 0).ToArray();
    /// <summary>Các ô lặp (nhiều dòng) gửi lên cùng tên: lấy theo thứ tự.</summary>
    public string[] All(string k) => f[k].Select(v => (v ?? "").Trim()).ToArray();
}

public class ShopCommands
{
    readonly string _cs;
    readonly string _imagesDir;
    readonly string _uploadsDir;

    public ShopCommands(IConfiguration config, IWebHostEnvironment env)
    {
        _cs = config.GetConnectionString("Default") ?? "";
        _imagesDir = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Paths:Images"] ?? "../../Backend/Images"));
        // Ảnh sản phẩm tải lên từ thiết bị lưu ở Frontend/Uploads, cả hai web cùng phục vụ tại đường dẫn /uploads
        _uploadsDir = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Paths:Uploads"] ?? "../Uploads"));
    }

    // ============================================================
    //  Danh sách lựa chọn cho các ô select
    // ============================================================

    /// <summary>Lấy danh sách lựa chọn từ chuỗi "a|Chữ A;b|Chữ B" hoặc "lookup:tên".</summary>
    public List<Opt> Options(string? spec)
    {
        if (string.IsNullOrEmpty(spec)) return [];
        if (!spec.StartsWith("lookup:"))
            return spec.Split(';').Select(p => { var a = p.Split('|', 2); return new Opt(a[0], a.Length > 1 ? a[1] : a[0]); }).ToList();

        using var c = new MySqlConnection(_cs);
        List<Opt> Q(string sql, string v, string l, string? d = null, bool blank = false)
        {
            var list = c.Query(sql).Cast<IDictionary<string, object?>>()
                .Select(r => new Opt(Convert.ToString(r[v])!, Convert.ToString(r[l])!, d == null ? null : Convert.ToString(r[d]))).ToList();
            if (blank) list.Insert(0, new Opt("", "(Không chọn)"));
            return list;
        }
        return spec[7..] switch
        {
            "vaitro" => Q("SELECT ma_vai_tro v, ten_vai_tro l FROM VAI_TRO ORDER BY ma_vai_tro", "v", "l"),
            "vaitro_nv" => Q("SELECT ma_vai_tro v, ten_vai_tro l FROM VAI_TRO WHERE ten_vai_tro <> 'Khách hàng' ORDER BY ma_vai_tro", "v", "l"),
            "loai" => Q($"SELECT ma_loai v, ten_loai l FROM LOAI_SP WHERE ten_loai <> '{Catalog.ArchivedCategory}' ORDER BY ten_loai", "v", "l"),
            "sanpham" => Q($"SELECT sp.ma_san_pham v, sp.ten_san_pham l FROM SAN_PHAM sp JOIN LOAI_SP lo ON lo.ma_loai = sp.ma_loai WHERE lo.ten_loai <> '{Catalog.ArchivedCategory}' ORDER BY sp.ten_san_pham", "v", "l"),
            "ncc" => Q("SELECT ma_ncc v, ten_ncc l FROM NHA_CUNG_CAP WHERE trang_thai = 'hop_tac' ORDER BY ten_ncc", "v", "l"),
            "sothich" => Q("SELECT ma_so_thich v, ten_so_thich l FROM SO_THICH ORDER BY ten_so_thich", "v", "l", blank: true),
            "ctkm" => Q("SELECT ma_ctkm v, ten_ctkm l FROM CTKM ORDER BY ngay_bat_dau DESC", "v", "l", blank: true),
            "nhom" => [new Opt("", "Tất cả khách"), .. Q("SELECT ma_nhom_kh v, ten_nhom l FROM NHOM_KHACH_HANG ORDER BY ten_nhom", "v", "l")],
            "pttt" => Q("SELECT ma_phuong_thuc_thanh_toan v, ten_phuong_thuc_thanh_toan l FROM PHUONG_THUC_THANH_TOAN WHERE trang_thai = 'hoat_dong' ORDER BY 1", "v", "l"),
            // Khách hàng: Data = "tên|sđt|địa chỉ mặc định" để tự điền thông tin người nhận
            "khach" => Q("""
                SELECT kh.ma_khach_hang v, CONCAT(kh.ho_lot, ' ', kh.ten, ' - ', IFNULL(kh.so_dien_thoai, '')) l,
                       CONCAT(kh.ho_lot, ' ', kh.ten, '|', IFNULL(kh.so_dien_thoai, ''), '|',
                              IFNULL((SELECT d.dia_chi_chi_tiet FROM SO_DIA_CHI d WHERE d.ma_khach_hang = kh.ma_khach_hang
                                      ORDER BY d.la_mac_dinh DESC, d.ma_dia_chi LIMIT 1), '')) d
                FROM KHACH_HANG kh ORDER BY kh.ten, kh.ho_lot
                """, "v", "l", "d"),
            // Biến thể: Data = "giá bán|tồn kho|giá vốn"
            "bienthe" => Q($"""
                SELECT bt.ma_bien_the v,
                       CONCAT(sp.ten_san_pham, IF(CONCAT_WS('', bt.kich_co, bt.mau_sac) = '', '', CONCAT(' (', CONCAT_WS(' / ', bt.kich_co, bt.mau_sac), ')'))) l,
                       CONCAT(bt.gia_ban, '|', bt.so_luong_ton, '|', bt.gia_von) d
                FROM BIEN_THE_SP bt JOIN SAN_PHAM sp ON sp.ma_san_pham = bt.ma_san_pham
                JOIN LOAI_SP lo ON lo.ma_loai = sp.ma_loai AND lo.ten_loai <> '{Catalog.ArchivedCategory}'
                ORDER BY sp.ten_san_pham, bt.kich_co
                """, "v", "l", "d"),
            // Dòng phiếu nhập: các biến thể, cộng thêm các sản phẩm CHƯA có biến thể nào (giá trị "p<mã sản phẩm>", Data = "0|0|0|new")
            // để sản phẩm vừa thêm có ngay trong danh sách; sản phẩm bị xóa thì tự mất khỏi danh sách
            "bienthe_nhap" => Q($"""
                SELECT CAST(bt.ma_bien_the AS CHAR) v,
                       CONCAT(sp.ten_san_pham, IF(CONCAT_WS('', bt.kich_co, bt.mau_sac) = '', '', CONCAT(' (', CONCAT_WS(' / ', bt.kich_co, bt.mau_sac), ')'))) l,
                       CONCAT(bt.gia_ban, '|', bt.so_luong_ton, '|', bt.gia_von) d
                FROM BIEN_THE_SP bt JOIN SAN_PHAM sp ON sp.ma_san_pham = bt.ma_san_pham
                JOIN LOAI_SP lo ON lo.ma_loai = sp.ma_loai AND lo.ten_loai <> '{Catalog.ArchivedCategory}'
                UNION ALL
                SELECT CONCAT('p', sp.ma_san_pham) v, CONCAT(sp.ten_san_pham, ' (chưa có biến thể)') l, '0|0|0|new' d
                FROM SAN_PHAM sp JOIN LOAI_SP lo ON lo.ma_loai = sp.ma_loai AND lo.ten_loai <> '{Catalog.ArchivedCategory}'
                WHERE NOT EXISTS (SELECT 1 FROM BIEN_THE_SP b WHERE b.ma_san_pham = sp.ma_san_pham)
                ORDER BY l
                """, "v", "l", "d"),
            "images" => ImageOptions(),
            _ => [],
        };
    }

    /// <summary>Danh sách ảnh để chọn: ảnh có sẵn trong Backend/Images ("/images/tên-file") và ảnh đã tải lên từ thiết bị ("/uploads/tên-file").</summary>
    List<Opt> ImageOptions()
    {
        var list = new List<Opt> { new("", "(Chưa có ảnh)") };
        list.AddRange(ImagesIn(_imagesDir, "/images/"));
        list.AddRange(ImagesIn(_uploadsDir, "/uploads/"));
        return list;
    }

    static IEnumerable<Opt> ImagesIn(string dir, string urlPrefix) =>
        !Directory.Exists(dir) ? [] : Directory.EnumerateFiles(dir)
            .Where(p => Regex.IsMatch(p, @"\.(jpe?g|png|webp|gif)$", RegexOptions.IgnoreCase))
            .OrderBy(p => p).Select(p => new Opt(urlPrefix + Path.GetFileName(p), Path.GetFileName(p)));

    const long MaxUploadBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Lưu ảnh sản phẩm tải lên từ thiết bị vào thư mục Uploads với tên ngẫu nhiên, trả về đường dẫn "/uploads/tên-file".
    /// Chỉ nhận ảnh JPG, PNG, WEBP, GIF tối đa 5 MB; kiểm tra cả phần đầu file để chắc đó đúng là ảnh.
    /// </summary>
    string SaveUploadedImage(IFormFile file)
    {
        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")) throw new UserError("Chỉ nhận ảnh định dạng JPG, PNG, WEBP hoặc GIF.");
        if (file.Length > MaxUploadBytes) throw new UserError("Ảnh quá lớn, tối đa 5 MB.");

        byte[] head = new byte[12];
        using (var s = file.OpenReadStream()) s.ReadExactly(head, 0, (int)Math.Min(head.Length, file.Length));
        bool isImage = head is [0xFF, 0xD8, 0xFF, ..]                                   // JPEG
                    || head is [0x89, 0x50, 0x4E, 0x47, ..]                            // PNG
                    || head is [0x47, 0x49, 0x46, 0x38, ..]                            // GIF
                    || head is [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50]; // WEBP
        if (!isImage) throw new UserError("File tải lên không phải là ảnh hợp lệ.");

        Directory.CreateDirectory(_uploadsDir);
        string name = $"sp-{Guid.NewGuid():N}{ext}";
        using (var fs = File.Create(Path.Combine(_uploadsDir, name))) file.CopyTo(fs);
        return "/uploads/" + name;
    }

    // ============================================================
    //  Nạp giá trị hiện có để điền sẵn vào biểu mẫu sửa / xem
    // ============================================================

    public Dictionary<string, string> Load(string entity, int id)
    {
        using var c = new MySqlConnection(_cs);
        IDictionary<string, object?>? row = entity switch
        {
            "account" => One(c, "SELECT ten_dang_nhap, email, so_dien_thoai, ma_vai_tro, trang_thai FROM TAI_KHOAN WHERE ma_tai_khoan=@id", new { id }),
            "customer" => One(c, """
                SELECT kh.ho_lot, kh.ten, kh.gioi_tinh, kh.ngay_sinh, kh.so_dien_thoai, tk.email,
                       (SELECT d.dia_chi_chi_tiet FROM SO_DIA_CHI d WHERE d.ma_khach_hang = kh.ma_khach_hang ORDER BY d.la_mac_dinh DESC, d.ma_dia_chi LIMIT 1) AS dia_chi
                FROM KHACH_HANG kh LEFT JOIN TAI_KHOAN tk ON tk.ma_tai_khoan = kh.ma_tai_khoan WHERE kh.ma_khach_hang=@id
                """, new { id }),
            "group" => One(c, "SELECT ten_nhom, mo_ta, loai_tieu_chi, gia_tri_tu, gia_tri_den, gioi_tinh, ma_so_thich FROM NHOM_KHACH_HANG WHERE ma_nhom_kh=@id", new { id }),
            "employee" => One(c, "SELECT ho_lot, ten, gioi_tinh, ngay_sinh, chuc_vu, luong_co_ban, trang_thai FROM NHAN_VIEN WHERE ma_nhan_vien=@id", new { id }),
            "category" => One(c, "SELECT ten_loai FROM LOAI_SP WHERE ma_loai=@id", new { id }),
            "product" => One(c, "SELECT ma_loai, ten_san_pham, don_vi_tinh, trang_thai, mo_ta FROM SAN_PHAM WHERE ma_san_pham=@id", new { id }),
            "variant" => One(c, "SELECT ma_san_pham, kich_co, mau_sac, gia_ban, gia_von, so_luong_ton, trang_thai, hinh_anh FROM BIEN_THE_SP WHERE ma_bien_the=@id", new { id }),
            "supplier" => One(c, "SELECT ten_ncc, so_dien_thoai, email, dia_chi, trang_thai FROM NHA_CUNG_CAP WHERE ma_ncc=@id", new { id }),
            "import" => One(c, "SELECT ma_ncc, ngay_nhap FROM PHIEU_NHAP_HANG WHERE ma_phieu_nhap=@id", new { id }),
            "order" => One(c, """
                SELECT dh.trang_thai_don_hang, dh.ly_do_huy, dh.trang_thai_thanh_toan, vc.ten_don_vi_van_chuyen, vc.ma_van_don
                FROM DON_HANG dh LEFT JOIN VAN_CHUYEN vc ON vc.ma_don_hang = dh.ma_don_hang WHERE dh.ma_don_hang=@id
                """, new { id }),
            "promotion" => One(c, """
                SELECT ten_ctkm, loai_khuyen_mai, trang_thai, ngay_bat_dau, ngay_ket_thuc, gia_tri_giam_toi_da, hang_ap_dung,
                       so_luong_gioi_han, gioi_han_khach_hang FROM CTKM WHERE ma_ctkm=@id
                """, new { id }),
            "campaign" => One(c, "SELECT tieu_de, noi_dung, kenh_gui, ma_ctkm, ma_nhom_kh, trang_thai FROM CHIEN_DICH_THONG_BAO WHERE ma_chien_dich=@id", new { id }),
            "survey" => One(c, "SELECT tieu_de, mo_ta, ngay_bat_dau, ngay_ket_thuc, trang_thai FROM KHAO_SAT WHERE ma_khao_sat=@id", new { id }),
            _ => null,
        };
        var values = new Dictionary<string, string>();
        if (row != null)
            foreach (var (k, v) in row)
                values[k] = v is null or DBNull ? "" : v is DateTime d ? (d.TimeOfDay == TimeSpan.Zero && k != "ngay_bat_dau" && k != "ngay_ket_thuc" && k != "ngay_nhap"
                    ? d.ToString("yyyy-MM-dd") : d.ToString("yyyy-MM-ddTHH:mm"))
                    : v is decimal m ? m.ToString("0.##", CultureInfo.InvariantCulture) : Convert.ToString(v, CultureInfo.InvariantCulture)!;

        // Chi tiết mức giảm của khuyến mãi (theo loại)
        if (entity == "promotion")
        {
            var sp = One(c, "SELECT ma_san_pham, phan_tram_giam FROM KM_THEO_SP WHERE ma_ctkm=@id LIMIT 1", new { id });
            var dh = One(c, "SELECT muc_tien_don_hang, phan_tram_giam FROM KM_THEO_DH WHERE ma_ctkm=@id ORDER BY muc_tien_don_hang LIMIT 1", new { id });
            var vc = One(c, "SELECT ma_voucher, phan_tram_giam FROM KM_THEO_VOUCHER WHERE ma_ctkm=@id LIMIT 1", new { id });
            if (sp != null) { values["d_san_pham"] = S(sp, "ma_san_pham"); values["d_phan_tram"] = Dec(sp, "phan_tram_giam"); }
            if (dh != null) { values["d_muc_tien"] = Dec(dh, "muc_tien_don_hang"); values["d_phan_tram"] = Dec(dh, "phan_tram_giam"); }
            if (vc != null) { values["d_voucher"] = S(vc, "ma_voucher"); values["d_phan_tram"] = Dec(vc, "phan_tram_giam"); }
        }
        return values;
    }

    /// <summary>Sở thích đã chọn của một khách hàng (mã SO_THICH).</summary>
    public HashSet<string> CustomerInterests(int customerId)
    {
        using var c = new MySqlConnection(_cs);
        return c.Query<int>("SELECT ma_so_thich FROM KHACH_HANG_SO_THICH WHERE ma_khach_hang=@customerId", new { customerId }).Select(i => i.ToString()).ToHashSet();
    }

    /// <summary>Các dòng của một phiếu nhập (để xem chi tiết).</summary>
    public List<(string Name, int Qty, decimal Price, decimal Amount)> ImportLines(int id)
    {
        using var c = new MySqlConnection(_cs);
        return c.Query("""
            SELECT CONCAT(sp.ten_san_pham, IF(CONCAT_WS('', bt.kich_co, bt.mau_sac) = '', '', CONCAT(' (', CONCAT_WS(' / ', bt.kich_co, bt.mau_sac), ')'))) AS ten,
                   ct.so_luong, ct.don_gia, ct.thanh_tien
            FROM CHI_TIET_PHIEU_NHAP_HANG ct JOIN BIEN_THE_SP bt ON bt.ma_bien_the = ct.ma_bien_the
            JOIN SAN_PHAM sp ON sp.ma_san_pham = bt.ma_san_pham WHERE ct.ma_phieu_nhap = @id
            """, new { id }).Cast<IDictionary<string, object?>>()
            .Select(r => (S(r, "ten"), int.Parse(S(r, "so_luong")), decimal.Parse(Dec(r, "don_gia"), CultureInfo.InvariantCulture), decimal.Parse(Dec(r, "thanh_tien"), CultureInfo.InvariantCulture))).ToList();
    }

    /// <summary>Các câu hỏi của khảo sát để điền sẵn khi sửa: (nội dung, loại, bắt buộc, các lựa chọn mỗi dòng một cái).</summary>
    public List<(string Text, string Type, bool Required, string Options)> SurveyQuestions(int id)
    {
        using var c = new MySqlConnection(_cs);
        var opts = c.Query("""
            SELECT lc.ma_cau_hoi, lc.noi_dung FROM LUA_CHON_CAU_HOI lc JOIN CAU_HOI_KHAO_SAT q ON q.ma_cau_hoi = lc.ma_cau_hoi
            WHERE q.ma_khao_sat = @id ORDER BY lc.ma_lua_chon
            """, new { id }).Cast<IDictionary<string, object?>>().ToLookup(r => S(r, "ma_cau_hoi"), r => S(r, "noi_dung"));
        return c.Query("SELECT ma_cau_hoi, noi_dung, loai_cau_hoi, bat_buoc FROM CAU_HOI_KHAO_SAT WHERE ma_khao_sat=@id ORDER BY thu_tu, ma_cau_hoi", new { id })
            .Cast<IDictionary<string, object?>>()
            .Select(r => (S(r, "noi_dung"), S(r, "loai_cau_hoi"), S(r, "bat_buoc") is "1" or "True", string.Join("\n", opts[S(r, "ma_cau_hoi")]))).ToList();
    }

    /// <summary>Khảo sát đã có khách trả lời thì không đổi được câu hỏi.</summary>
    public bool SurveyHasAnswers(int id)
    {
        using var c = new MySqlConnection(_cs);
        return c.ExecuteScalar<int>("SELECT COUNT(*) FROM KHAO_SAT_KHACH_HANG WHERE ma_khao_sat=@id AND trang_thai <> 'chua_lam'", new { id }) > 0;
    }

    // ============================================================
    //  Lưu (thêm mới / sửa)
    // ============================================================

    public Result Save(string entity, int id, Form f, Ctx me) => Run((c, tx) => entity switch
    {
        "account" => SaveAccount(c, tx, id, f, me),
        "customer" => SaveCustomer(c, tx, id, f),
        "group" => SaveGroup(c, tx, id, f),
        "care" => SaveCare(c, tx, f, me),
        "employee" => SaveEmployee(c, tx, id, f),
        "category" => SaveCategory(c, tx, id, f),
        "product" => SaveProduct(c, tx, id, f),
        "variant" => SaveVariant(c, tx, id, f),
        "supplier" => SaveSupplier(c, tx, id, f),
        "import" => SaveImport(c, tx, id, f, me),
        "order" => id == 0 ? CreateOrder(c, tx, f, me) : UpdateOrder(c, tx, id, f),
        "promotion" => SavePromotion(c, tx, id, f),
        "campaign" => SaveCampaign(c, tx, id, f, me),
        "survey" => SaveSurvey(c, tx, id, f, me),
        "survey_send" => SendSurvey(c, tx, id, f),
        _ => throw new UserError("Không hỗ trợ thao tác này."),
    });

    // ---------- Tài khoản ----------
    string SaveAccount(MySqlConnection c, MySqlTransaction tx, int id, Form f, Ctx me)
    {
        string email = f.S("email"), phone = f.S("so_dien_thoai"), pw = f.S("mat_khau");
        int role = f.I("ma_vai_tro");
        CheckEmail(email); CheckPhone(phone, false);
        if (role == 0) throw new UserError("Vui lòng chọn vai trò.");
        if (id == 0)
        {
            string u = f.S("ten_dang_nhap");
            if (!Regex.IsMatch(u, @"^[A-Za-z0-9._-]{3,50}$")) throw new UserError("Tên đăng nhập gồm 3–50 ký tự chữ, số, dấu chấm, gạch ngang hoặc gạch dưới (không dấu, không khoảng trắng).");
            if (pw.Length < 6) throw new UserError("Mật khẩu cần có ít nhất 6 ký tự.");
            c.Execute("INSERT INTO TAI_KHOAN (ten_dang_nhap, email, so_dien_thoai, mat_khau, ma_vai_tro, trang_thai) VALUES (@u, @email, @phone, @pw, @role, 'hoat_dong')",
                new { u, email, phone = phone.NullIfEmpty(), pw, role }, tx);
            return "Đã thêm tài khoản " + u + ".";
        }
        string status = f.S("trang_thai");
        if (status is not ("hoat_dong" or "bi_khoa" or "da_xoa")) status = "hoat_dong";
        var username = c.ExecuteScalar<string>("SELECT ten_dang_nhap FROM TAI_KHOAN WHERE ma_tai_khoan=@id", new { id }, tx) ?? throw new UserError("Không tìm thấy tài khoản.");
        if (username == me.Username && status != "hoat_dong") throw new UserError("Không thể khóa hoặc xóa tài khoản đang đăng nhập.");
        if (pw.Length > 0 && pw.Length < 6) throw new UserError("Mật khẩu mới cần có ít nhất 6 ký tự.");
        c.Execute("UPDATE TAI_KHOAN SET email=@email, so_dien_thoai=@phone, ma_vai_tro=@role, trang_thai=@status WHERE ma_tai_khoan=@id",
            new { email, phone = phone.NullIfEmpty(), role, status, id }, tx);
        if (pw.Length >= 6) c.Execute("UPDATE TAI_KHOAN SET mat_khau=@pw WHERE ma_tai_khoan=@id", new { pw, id }, tx);
        return "Đã cập nhật tài khoản " + username + ".";
    }

    // ---------- Khách hàng ----------
    string SaveCustomer(MySqlConnection c, MySqlTransaction tx, int id, Form f)
    {
        string last = f.S("ho_lot"), first = f.S("ten"), phone = f.S("so_dien_thoai");
        string? gender = f.N("gioi_tinh"); var birth = f.Date("ngay_sinh");
        if (last.Length == 0 || first.Length == 0) throw new UserError("Vui lòng nhập họ lót và tên.");
        CheckPhone(phone, false);
        if (birth > DateTime.Today) throw new UserError("Ngày sinh không hợp lệ.");

        if (id == 0)
        {
            int? accountId = null;
            string u = f.S("ten_dang_nhap");
            if (u.Length > 0)
            {
                string email = f.S("email"), pw = f.S("mat_khau");
                if (!Regex.IsMatch(u, @"^[A-Za-z0-9._-]{3,50}$")) throw new UserError("Tên đăng nhập gồm 3–50 ký tự chữ, số, dấu chấm, gạch ngang hoặc gạch dưới.");
                CheckEmail(email);
                if (pw.Length < 6) throw new UserError("Mật khẩu cần có ít nhất 6 ký tự.");
                accountId = c.ExecuteScalar<int>("""
                    INSERT INTO TAI_KHOAN (ten_dang_nhap, email, so_dien_thoai, mat_khau, ma_vai_tro, trang_thai)
                    VALUES (@u, @email, @phone, @pw, (SELECT ma_vai_tro FROM VAI_TRO WHERE ten_vai_tro = 'Khách hàng'), 'hoat_dong');
                    SELECT LAST_INSERT_ID();
                    """, new { u, email, phone = phone.NullIfEmpty(), pw }, tx);
            }
            id = c.ExecuteScalar<int>("""
                INSERT INTO KHACH_HANG (ma_tai_khoan, ho_lot, ten, gioi_tinh, ngay_sinh, so_dien_thoai)
                VALUES (@accountId, @last, @first, @gender, @birth, @phone);
                SELECT LAST_INSERT_ID();
                """, new { accountId, last, first, gender, birth, phone = phone.NullIfEmpty() }, tx);
        }
        else
        {
            if (c.Execute("UPDATE KHACH_HANG SET ho_lot=@last, ten=@first, gioi_tinh=@gender, ngay_sinh=@birth, so_dien_thoai=@phone WHERE ma_khach_hang=@id",
                    new { last, first, gender, birth, phone = phone.NullIfEmpty(), id }, tx) == 0
                && c.ExecuteScalar<int>("SELECT COUNT(*) FROM KHACH_HANG WHERE ma_khach_hang=@id", new { id }, tx) == 0)
                throw new UserError("Không tìm thấy khách hàng.");
            UpdateCustomerAccountAndAddress(c, tx, id, f, last, first, phone);
        }

        // Sở thích: thay toàn bộ bằng các ô được tích
        c.Execute("DELETE FROM KHACH_HANG_SO_THICH WHERE ma_khach_hang=@id", new { id }, tx);
        foreach (var s in f.Many("so_thich").Select(v => int.TryParse(v, out var n) ? n : 0).Where(n => n > 0).Distinct())
            c.Execute("INSERT INTO KHACH_HANG_SO_THICH (ma_khach_hang, ma_so_thich) VALUES (@id, @s)", new { id, s }, tx);
        return $"Đã lưu khách hàng {last} {first}.";
    }

    /// <summary>
    /// Sửa thông tin khách ngoài hồ sơ: email và số điện thoại của tài khoản (giống khi khách tự sửa ở web khách) và địa chỉ giao hàng mặc định.
    /// Không bao giờ đổi mật khẩu: mật khẩu do khách tự đổi, web admin chỉ xem lại giá trị hiện có trong CSDL.
    /// </summary>
    void UpdateCustomerAccountAndAddress(MySqlConnection c, MySqlTransaction tx, int id, Form f, string last, string first, string phone)
    {
        // Email và số điện thoại của tài khoản đăng nhập (khách mua tại quầy chưa có tài khoản thì bỏ qua)
        int? accountId = c.ExecuteScalar<int?>("SELECT ma_tai_khoan FROM KHACH_HANG WHERE ma_khach_hang=@id", new { id }, tx);
        string email = f.S("email");
        if (accountId != null)
        {
            if (email.Length == 0) throw new UserError("Vui lòng nhập email của khách.");
            CheckEmail(email);
            if (c.ExecuteScalar<int>("SELECT COUNT(*) FROM TAI_KHOAN WHERE email=@email AND ma_tai_khoan<>@accountId", new { email, accountId }, tx) > 0)
                throw new UserError("Email đã được tài khoản khác sử dụng.");
            c.Execute("UPDATE TAI_KHOAN SET email=@email, so_dien_thoai=@phone WHERE ma_tai_khoan=@accountId", new { email, phone = phone.NullIfEmpty(), accountId }, tx);
        }

        // Địa chỉ giao hàng mặc định: sửa địa chỉ mặc định hiện có, chưa có thì thêm mới (để trống thì giữ nguyên)
        string address = f.S("dia_chi");
        if (address.Length == 0) return;
        if (address.Length > 255) throw new UserError("Địa chỉ tối đa 255 ký tự.");
        int? addressId = c.ExecuteScalar<int?>("SELECT ma_dia_chi FROM SO_DIA_CHI WHERE ma_khach_hang=@id ORDER BY la_mac_dinh DESC, ma_dia_chi LIMIT 1", new { id }, tx);
        if (addressId != null)
            c.Execute("UPDATE SO_DIA_CHI SET dia_chi_chi_tiet=@address WHERE ma_dia_chi=@addressId", new { address, addressId }, tx);
        else
        {
            if (phone.Length == 0) throw new UserError("Cần có số điện thoại của khách để lưu địa chỉ giao hàng.");
            c.Execute("INSERT INTO SO_DIA_CHI (ma_khach_hang, ten_nguoi_nhan, sdt_nguoi_nhan, dia_chi_chi_tiet, la_mac_dinh) VALUES (@id, @name, @phone, @address, 1)",
                new { id, name = $"{last} {first}".Trim(), phone, address }, tx);
        }
    }

    // ---------- Nhóm khách hàng ----------
    string SaveGroup(MySqlConnection c, MySqlTransaction tx, int id, Form f)
    {
        string name = f.S("ten_nhom"), criterion = f.S("loai_tieu_chi");
        if (name.Length == 0) throw new UserError("Vui lòng nhập tên nhóm.");
        decimal? from = null, to = null; string? gender = null; int? interest = null;
        switch (criterion)
        {
            case "tuoi" or "chi_tieu":
                from = f.ND("gia_tri_tu"); to = f.ND("gia_tri_den");
                if (from == null && to == null) throw new UserError("Vui lòng nhập ít nhất một giá trị Từ hoặc Đến.");
                if (from != null && to != null && from > to) throw new UserError("Giá trị \"Từ\" không được lớn hơn \"Đến\".");
                break;
            case "gioi_tinh":
                gender = f.N("gioi_tinh") ?? throw new UserError("Vui lòng chọn giới tính."); break;
            case "so_thich":
                interest = f.NI("ma_so_thich") ?? throw new UserError("Vui lòng chọn sở thích."); break;
            default: throw new UserError("Vui lòng chọn tiêu chí phân nhóm.");
        }
        var p = new { name, desc = f.N("mo_ta"), criterion, from, to, gender, interest, id };
        if (id == 0)
            id = c.ExecuteScalar<int>("""
                INSERT INTO NHOM_KHACH_HANG (ten_nhom, mo_ta, loai_tieu_chi, gia_tri_tu, gia_tri_den, gioi_tinh, ma_so_thich)
                VALUES (@name, @desc, @criterion, @from, @to, @gender, @interest); SELECT LAST_INSERT_ID();
                """, p, tx);
        else
            c.Execute("""
                UPDATE NHOM_KHACH_HANG SET ten_nhom=@name, mo_ta=@desc, loai_tieu_chi=@criterion, gia_tri_tu=@from, gia_tri_den=@to,
                       gioi_tinh=@gender, ma_so_thich=@interest WHERE ma_nhom_kh=@id
                """, p, tx);

        // Xếp lại thành viên của nhóm theo tiêu chí vừa lưu
        c.Execute("DELETE FROM KHACH_HANG_NHOM_KH WHERE ma_nhom_kh=@id", new { id }, tx);
        string where = criterion switch
        {
            "tuoi" => "kh.ngay_sinh IS NOT NULL AND TIMESTAMPDIFF(YEAR, kh.ngay_sinh, CURDATE()) BETWEEN IFNULL(@from, 0) AND IFNULL(@to, 200)",
            "chi_tieu" => "kh.tong_chi_tieu BETWEEN IFNULL(@from, 0) AND IFNULL(@to, 999999999999)",
            "gioi_tinh" => "kh.gioi_tinh = @gender",
            _ => "EXISTS (SELECT 1 FROM KHACH_HANG_SO_THICH s WHERE s.ma_khach_hang = kh.ma_khach_hang AND s.ma_so_thich = @interest)",
        };
        int n = c.Execute($"INSERT INTO KHACH_HANG_NHOM_KH (ma_khach_hang, ma_nhom_kh) SELECT kh.ma_khach_hang, @id FROM KHACH_HANG kh WHERE {where}", new { id, from, to, gender, interest }, tx);
        return $"Đã lưu nhóm \"{name}\" ({n} khách thuộc nhóm).";
    }

    // ---------- Ghi chú chăm sóc ----------
    string SaveCare(MySqlConnection c, MySqlTransaction tx, Form f, Ctx me)
    {
        int customer = f.I("parent");
        string content = f.S("noi_dung");
        if (content.Length == 0) throw new UserError("Vui lòng nhập nội dung chăm sóc.");
        if (content.Length > 500) throw new UserError("Nội dung tối đa 500 ký tự.");
        c.Execute("""
            INSERT INTO LICH_SU_CHAM_SOC (ma_khach_hang, ma_nhan_vien, hinh_thuc, noi_dung, ngay_cham_soc, ngay_hen_lien_he_lai)
            VALUES (@customer, @emp, @kind, @content, NOW(), @follow)
            """, new { customer, emp = me.EmployeeId, kind = f.S("hinh_thuc"), content, follow = f.Date("ngay_hen_lien_he_lai") }, tx);
        return "Đã thêm ghi chú chăm sóc.";
    }

    // ---------- Nhân viên ----------
    string SaveEmployee(MySqlConnection c, MySqlTransaction tx, int id, Form f)
    {
        string last = f.S("ho_lot"), first = f.S("ten");
        if (last.Length == 0 || first.Length == 0) throw new UserError("Vui lòng nhập họ lót và tên.");
        var birth = f.Date("ngay_sinh"); decimal salary = f.D("luong_co_ban");
        if (salary < 0) throw new UserError("Lương cơ bản không được âm.");
        var p = new { last, first, gender = f.N("gioi_tinh"), birth, position = f.N("chuc_vu"), salary, id };
        if (id == 0)
        {
            string u = f.S("ten_dang_nhap"), email = f.S("email"), pw = f.S("mat_khau"); int role = f.I("ma_vai_tro");
            if (!Regex.IsMatch(u, @"^[A-Za-z0-9._-]{3,50}$")) throw new UserError("Tên đăng nhập gồm 3–50 ký tự chữ, số, dấu chấm, gạch ngang hoặc gạch dưới.");
            CheckEmail(email); CheckPhone(f.S("so_dien_thoai"), false);
            if (pw.Length < 6) throw new UserError("Mật khẩu cần có ít nhất 6 ký tự.");
            if (role == 0) throw new UserError("Vui lòng chọn vai trò.");
            int acc = c.ExecuteScalar<int>("""
                INSERT INTO TAI_KHOAN (ten_dang_nhap, email, so_dien_thoai, mat_khau, ma_vai_tro, trang_thai)
                VALUES (@u, @email, @phone, @pw, @role, 'hoat_dong'); SELECT LAST_INSERT_ID();
                """, new { u, email, phone = f.S("so_dien_thoai").NullIfEmpty(), pw, role }, tx);
            c.Execute("""
                INSERT INTO NHAN_VIEN (ma_tai_khoan, ho_lot, ten, gioi_tinh, ngay_sinh, luong_co_ban, chuc_vu, trang_thai)
                VALUES (@acc, @last, @first, @gender, @birth, @salary, @position, 'dang_lam')
                """, new { acc, p.last, p.first, p.gender, p.birth, p.salary, p.position }, tx);
            return $"Đã thêm nhân viên {last} {first} kèm tài khoản {u}.";
        }
        string status = f.S("trang_thai") == "nghi_viec" ? "nghi_viec" : "dang_lam";
        c.Execute("UPDATE NHAN_VIEN SET ho_lot=@last, ten=@first, gioi_tinh=@gender, ngay_sinh=@birth, luong_co_ban=@salary, chuc_vu=@position, trang_thai=@status WHERE ma_nhan_vien=@id",
            new { p.last, p.first, p.gender, p.birth, p.salary, p.position, status, id }, tx);
        return $"Đã cập nhật nhân viên {last} {first}.";
    }

    // ---------- Loại sản phẩm ----------
    string SaveCategory(MySqlConnection c, MySqlTransaction tx, int id, Form f)
    {
        string name = f.S("ten_loai");
        if (name.Length == 0) throw new UserError("Vui lòng nhập tên loại sản phẩm.");
        if (id == 0) c.Execute("INSERT INTO LOAI_SP (ten_loai) VALUES (@name)", new { name }, tx);
        else c.Execute("UPDATE LOAI_SP SET ten_loai=@name WHERE ma_loai=@id", new { name, id }, tx);
        return $"Đã lưu loại sản phẩm \"{name}\".";
    }

    // ---------- Sản phẩm ----------
    string SaveProduct(MySqlConnection c, MySqlTransaction tx, int id, Form f)
    {
        string name = f.S("ten_san_pham"), unit = f.S("don_vi_tinh");
        int cat = f.I("ma_loai");
        if (cat == 0) throw new UserError("Vui lòng chọn loại sản phẩm.");
        if (name.Length == 0 || unit.Length == 0) throw new UserError("Vui lòng nhập tên sản phẩm và đơn vị tính.");
        string status = f.S("trang_thai") == "an" ? "an" : "hien";
        // Ảnh nằm ở từng biến thể (BIEN_THE_SP.hinh_anh), không lưu ở sản phẩm
        var p = new { cat, name, unit, desc = f.N("mo_ta"), status, id };
        if (id == 0) c.Execute("INSERT INTO SAN_PHAM (ma_loai, ten_san_pham, don_vi_tinh, mo_ta, trang_thai) VALUES (@cat, @name, @unit, @desc, @status)", p, tx);
        else c.Execute("UPDATE SAN_PHAM SET ma_loai=@cat, ten_san_pham=@name, don_vi_tinh=@unit, mo_ta=@desc, trang_thai=@status WHERE ma_san_pham=@id", p, tx);
        return $"Đã lưu sản phẩm \"{name}\".";
    }

    // ---------- Biến thể ----------
    string SaveVariant(MySqlConnection c, MySqlTransaction tx, int id, Form f)
    {
        int product = f.I("ma_san_pham"); decimal price = f.D("gia_ban"), cost = f.D("gia_von"); int stock = f.I("so_luong_ton");
        if (product == 0) throw new UserError("Vui lòng chọn sản phẩm.");
        if (price <= 0) throw new UserError("Giá bán phải lớn hơn 0.");
        if (cost < 0 || stock < 0) throw new UserError("Giá vốn và số lượng tồn không được âm.");
        // Có chọn ảnh từ thiết bị thì dùng ảnh đó, không thì dùng ảnh chọn trong danh sách (ảnh theo màu của biến thể)
        string? img = f.File("hinh_anh_file") is { Length: > 0 } upload ? SaveUploadedImage(upload) : f.N("hinh_anh");
        var p = new { product, size = f.N("kich_co"), color = f.N("mau_sac"), price, cost, stock, status = f.S("trang_thai") == "an" ? "an" : "hien", img, id };
        if (id == 0) c.Execute("INSERT INTO BIEN_THE_SP (ma_san_pham, kich_co, mau_sac, gia_ban, gia_von, so_luong_ton, trang_thai, hinh_anh) VALUES (@product, @size, @color, @price, @cost, @stock, @status, @img)", p, tx);
        else c.Execute("UPDATE BIEN_THE_SP SET ma_san_pham=@product, kich_co=@size, mau_sac=@color, gia_ban=@price, gia_von=@cost, so_luong_ton=@stock, trang_thai=@status, hinh_anh=@img WHERE ma_bien_the=@id", p, tx);
        return "Đã lưu biến thể sản phẩm.";
    }

    // ---------- Nhà cung cấp ----------
    string SaveSupplier(MySqlConnection c, MySqlTransaction tx, int id, Form f)
    {
        string name = f.S("ten_ncc"), email = f.S("email");
        if (name.Length == 0) throw new UserError("Vui lòng nhập tên nhà cung cấp.");
        if (email.Length > 0) CheckEmail(email);
        CheckPhone(f.S("so_dien_thoai"), false);
        var p = new { name, phone = f.N("so_dien_thoai"), email = email.NullIfEmpty(), addr = f.N("dia_chi"), status = f.S("trang_thai") == "ngung_hop_tac" ? "ngung_hop_tac" : "hop_tac", id };
        if (id == 0) c.Execute("INSERT INTO NHA_CUNG_CAP (ten_ncc, so_dien_thoai, email, dia_chi, trang_thai) VALUES (@name, @phone, @email, @addr, @status)", p, tx);
        else c.Execute("UPDATE NHA_CUNG_CAP SET ten_ncc=@name, so_dien_thoai=@phone, email=@email, dia_chi=@addr, trang_thai=@status WHERE ma_ncc=@id", p, tx);
        return $"Đã lưu nhà cung cấp \"{name}\".";
    }

    // ---------- Phiếu nhập hàng: cộng tồn kho, cập nhật giá vốn theo đơn giá nhập (PDF 3.1.10) ----------
    string SaveImport(MySqlConnection c, MySqlTransaction tx, int id, Form f, Ctx me)
    {
        if (id != 0) throw new UserError("Phiếu nhập đã cập nhật tồn kho nên không sửa được.");
        int supplier = f.I("ma_ncc"); var date = f.Date("ngay_nhap") ?? DateTime.Now;
        if (supplier == 0) throw new UserError("Vui lòng chọn nhà cung cấp.");

        // Các dòng: biến thể + số lượng + đơn giá (bỏ qua dòng bỏ trống, gộp dòng trùng biến thể)
        var v = f.All("dong_bien_the"); var q = f.All("dong_so_luong"); var pr = f.All("dong_don_gia"); var sell = f.All("dong_gia_ban");
        var lines = new Dictionary<int, (int Qty, decimal Price)>();
        var createdVariants = new Dictionary<int, int>();   // mã sản phẩm → biến thể mặc định vừa tạo trong phiếu này
        for (int i = 0; i < v.Length; i++)
        {
            int variant;
            decimal Num(string[] a) => i < a.Length && decimal.TryParse(a[i].Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : -1;
            int qty = i < q.Length && int.TryParse(q[i], out var qq) ? qq : 0;
            decimal price = Num(pr);
            if (v[i].StartsWith('p') && int.TryParse(v[i][1..], out int productId))
            {
                // Sản phẩm chưa có biến thể: tự tạo biến thể mặc định (không size, không màu) với giá bán nhập ở dòng này
                if (!createdVariants.TryGetValue(productId, out variant))
                {
                    decimal sellPrice = Num(sell);
                    if (sellPrice <= 0) throw new UserError("Sản phẩm chưa có biến thể nên cần nhập giá bán cho dòng đó.");
                    variant = c.ExecuteScalar<int>("INSERT INTO BIEN_THE_SP (ma_san_pham, gia_ban, gia_von, so_luong_ton, trang_thai) VALUES (@productId, @sellPrice, @cost, 0, 'hien'); SELECT LAST_INSERT_ID();",
                        new { productId, sellPrice, cost = Math.Max(0, price) }, tx);
                    createdVariants[productId] = variant;
                }
            }
            else if (!int.TryParse(v[i], out variant) || variant == 0) continue;
            if (qty <= 0) throw new UserError("Số lượng nhập phải lớn hơn 0.");
            if (price < 0) throw new UserError("Vui lòng nhập đơn giá nhập cho mọi dòng.");
            lines[variant] = lines.TryGetValue(variant, out var old) ? (old.Qty + qty, price) : (qty, price);
        }
        if (lines.Count == 0) throw new UserError("Phiếu nhập cần có ít nhất một dòng hàng.");

        int slip = c.ExecuteScalar<int>("INSERT INTO PHIEU_NHAP_HANG (ngay_nhap, ma_nhan_vien, ma_ncc, tong_tien) VALUES (@date, @emp, @supplier, 0); SELECT LAST_INSERT_ID();",
            new { date, emp = me.EmployeeId, supplier }, tx);
        decimal total = 0;
        foreach (var (variant, (qty, price)) in lines)
        {
            decimal amount = qty * price; total += amount;
            c.Execute("INSERT INTO CHI_TIET_PHIEU_NHAP_HANG (ma_phieu_nhap, ma_bien_the, so_luong, don_gia, thanh_tien) VALUES (@slip, @variant, @qty, @price, @amount)",
                new { slip, variant, qty, price, amount }, tx);
            // Tồn tăng theo số nhập, giá vốn lấy theo đơn giá nhập gần nhất
            c.Execute("UPDATE BIEN_THE_SP SET so_luong_ton = so_luong_ton + @qty, gia_von = @price WHERE ma_bien_the = @variant", new { qty, price, variant }, tx);
        }
        c.Execute("UPDATE PHIEU_NHAP_HANG SET tong_tien=@total WHERE ma_phieu_nhap=@slip", new { total, slip }, tx);
        return $"Đã lập phiếu nhập PN{slip:D3}: {lines.Count} mặt hàng, tổng {total:N0}đ. Tồn kho và giá vốn đã được cập nhật.";
    }

    // ---------- Đơn hàng: lập mới tại quầy ----------
    string CreateOrder(MySqlConnection c, MySqlTransaction tx, Form f, Ctx me)
    {
        int customer = f.I("ma_khach_hang");
        string name = f.S("ten_nguoi_nhan"), phone = f.S("sdt_nguoi_nhan"), addr = f.S("dia_chi_giao_hang");
        int pay = f.I("ma_phuong_thuc_thanh_toan"); decimal ship = f.D("phi_van_chuyen");
        string status = f.S("trang_thai_don_hang"); if (status is not ("cho_xac_nhan" or "dang_chuan_bi" or "dang_giao" or "da_giao")) status = "cho_xac_nhan";
        string paid = f.S("trang_thai_thanh_toan") == "da_thanh_toan" ? "da_thanh_toan" : "chua_thanh_toan";
        if (paid == "chua_thanh_toan" && status != "cho_xac_nhan") throw new UserError("Đơn chưa thanh toán nên chỉ ở trạng thái \"Chờ xác nhận\". Chọn \"Đã thanh toán\" nếu khách đã trả tiền.");
        if (customer == 0) throw new UserError("Vui lòng chọn khách hàng.");
        if (name.Length == 0 || phone.Length == 0 || addr.Length == 0) throw new UserError("Vui lòng nhập đủ thông tin người nhận.");
        if (pay == 0) throw new UserError("Vui lòng chọn phương thức thanh toán.");
        if (ship < 0) throw new UserError("Phí vận chuyển không được âm.");

        // Gộp các dòng sản phẩm
        var v = f.All("dong_bien_the"); var q = f.All("dong_so_luong");
        var lines = new Dictionary<int, int>();
        for (int i = 0; i < v.Length; i++)
        {
            if (!int.TryParse(v[i], out int variant) || variant == 0) continue;
            int qty = i < q.Length && int.TryParse(q[i], out var qq) ? qq : 0;
            if (qty <= 0) throw new UserError("Số lượng mỗi sản phẩm phải lớn hơn 0.");
            lines[variant] = lines.GetValueOrDefault(variant) + qty;
        }
        if (lines.Count == 0) throw new UserError("Đơn hàng cần có ít nhất một sản phẩm.");

        // Giá bán lấy tại thời điểm đặt
        decimal subtotal = 0;
        var priced = new List<(int Variant, int Qty, decimal Price)>();
        foreach (var (variant, qty) in lines)
        {
            var price = c.ExecuteScalar<decimal?>("SELECT gia_ban FROM BIEN_THE_SP WHERE ma_bien_the=@variant AND trang_thai='hien'", new { variant }, tx)
                ?? throw new UserError("Có sản phẩm không tồn tại hoặc đang bị ẩn.");
            priced.Add((variant, qty, price)); subtotal += qty * price;
        }

        // Voucher (nếu có): kiểm tra thời hạn, trạng thái, hạng, số lượt dùng (PDF 3.1.7)
        int? ctkm = null; decimal discount = 0;
        string code = f.S("ma_voucher");
        if (code.Length > 0)
        {
            var km = One(c, """
                SELECT k.ma_ctkm, k.phan_tram_giam, ct.gia_tri_giam_toi_da, ct.hang_ap_dung, ct.so_luong_gioi_han, ct.so_luot_da_dung, ct.gioi_han_khach_hang
                FROM KM_THEO_VOUCHER k JOIN CTKM ct ON ct.ma_ctkm = k.ma_ctkm
                WHERE k.ma_voucher = @code AND ct.trang_thai = 'hoat_dong' AND NOW() BETWEEN ct.ngay_bat_dau AND ct.ngay_ket_thuc
                """, new { code }, tx) ?? throw new UserError("Mã voucher không hợp lệ hoặc đã hết hạn.");
            var member = One(c, "SELECT hang_thanh_vien, tong_chi_tieu FROM KHACH_HANG WHERE ma_khach_hang=@customer", new { customer }, tx);
            string tier = member == null ? "thuong" : MemberTier.Effective(S(member, "hang_thanh_vien"), decimal.Parse(Dec(member, "tong_chi_tieu"), CultureInfo.InvariantCulture));
            if (S(km, "hang_ap_dung") is { Length: > 0 } need && need != tier) throw new UserError($"Voucher chỉ áp dụng cho khách hạng {need}.");
            if (S(km, "so_luong_gioi_han") is { Length: > 0 } lim && int.Parse(S(km, "so_luot_da_dung")) >= int.Parse(lim)) throw new UserError("Voucher đã hết lượt sử dụng.");
            int ctId = int.Parse(S(km, "ma_ctkm"));
            if (S(km, "gioi_han_khach_hang") is { Length: > 0 } per
                && c.ExecuteScalar<int>("SELECT COUNT(*) FROM LUOT_SU_DUNG_KHUYEN_MAI WHERE ma_ctkm=@ctId AND ma_khach_hang=@customer", new { ctId, customer }, tx) >= int.Parse(per))
                throw new UserError("Khách đã dùng hết số lần cho phép của voucher này.");
            discount = Math.Round(subtotal * decimal.Parse(Dec(km, "phan_tram_giam"), CultureInfo.InvariantCulture) / 100, 0);
            if (S(km, "gia_tri_giam_toi_da") is { Length: > 0 }) discount = Math.Min(discount, decimal.Parse(Dec(km, "gia_tri_giam_toi_da"), CultureInfo.InvariantCulture));
            ctkm = ctId;
        }
        decimal total = subtotal - discount + ship;

        int order = c.ExecuteScalar<int>("""
            INSERT INTO DON_HANG (ma_khach_hang, ma_nhan_vien, ngay_dat, ten_nguoi_nhan, sdt_nguoi_nhan, dia_chi_giao_hang, ma_ctkm,
                                  tam_tinh, tong_tien_giam_gia, tong_thanh_toan, ma_phuong_thuc_thanh_toan, trang_thai_thanh_toan, trang_thai_don_hang)
            VALUES (@customer, @emp, NOW(), @name, @phone, @addr, @ctkm, @subtotal, @discount, @total, @pay, 'chua_thanh_toan', @status);
            SELECT LAST_INSERT_ID();
            """, new { customer, emp = me.EmployeeId, name, phone, addr, ctkm, subtotal, discount, total, pay, status }, tx);

        foreach (var (variant, qty, price) in priced)
        {
            // Trừ tồn kho; không đủ hàng thì báo lỗi (hoàn tác cả đơn)
            if (c.Execute("UPDATE BIEN_THE_SP SET so_luong_ton = so_luong_ton - @qty WHERE ma_bien_the=@variant AND so_luong_ton >= @qty", new { qty, variant }, tx) == 0)
                throw new UserError("Không đủ tồn kho cho: " + c.ExecuteScalar<string>("SELECT CONCAT(sp.ten_san_pham, IF(CONCAT_WS('', bt.kich_co, bt.mau_sac) = '', '', CONCAT(' (', CONCAT_WS(' / ', bt.kich_co, bt.mau_sac), ')'))) FROM BIEN_THE_SP bt JOIN SAN_PHAM sp ON sp.ma_san_pham = bt.ma_san_pham WHERE bt.ma_bien_the=@variant", new { variant }, tx) + ".");
            c.Execute("INSERT INTO CHI_TIET_DON_HANG (ma_don_hang, ma_bien_the, so_luong, don_gia, giam_gia, thanh_tien) VALUES (@order, @variant, @qty, @price, 0, @amount)",
                new { order, variant, qty, price, amount = qty * price }, tx);
        }
        string carrier = f.S("ten_don_vi_van_chuyen");
        if (carrier.Length > 0 || ship > 0)
            c.Execute("INSERT INTO VAN_CHUYEN (ma_don_hang, ma_nhan_vien, ten_don_vi_van_chuyen, ma_van_don, phi_van_chuyen, trang_thai) VALUES (@order, @emp, @carrier, @track, @ship, 'cho_lay_hang')",
                new { order, emp = me.EmployeeId, carrier = carrier.Length > 0 ? carrier : "Tại quầy", track = f.N("ma_van_don"), ship }, tx);
        if (ctkm != null)
        {
            c.Execute("INSERT INTO LUOT_SU_DUNG_KHUYEN_MAI (ma_ctkm, ma_khach_hang, ma_don_hang, so_tien_giam, ngay_su_dung) VALUES (@ctkm, @customer, @order, @discount, NOW())", new { ctkm, customer, order, discount }, tx);
            c.Execute("UPDATE CTKM SET so_luot_da_dung = so_luot_da_dung + 1 WHERE ma_ctkm=@ctkm", new { ctkm }, tx);
        }
        string extra = "";
        if (paid == "da_thanh_toan") { extra = " Đã lập hóa đơn " + CreateInvoice(c, tx, order) + "."; c.Execute("UPDATE DON_HANG SET trang_thai_thanh_toan='da_thanh_toan' WHERE ma_don_hang=@order", new { order }, tx); }
        return $"Đã tạo đơn hàng DH{order:D5}, tổng thanh toán {total:N0}đ." + extra;
    }

    // ---------- Đơn hàng: đổi trạng thái, hủy (hoàn tồn kho), thanh toán (lập hóa đơn), vận chuyển ----------
    string UpdateOrder(MySqlConnection c, MySqlTransaction tx, int id, Form f)
    {
        var o = One(c, "SELECT trang_thai_don_hang, trang_thai_thanh_toan FROM DON_HANG WHERE ma_don_hang=@id FOR UPDATE", new { id }, tx)
            ?? throw new UserError("Không tìm thấy đơn hàng.");
        string oldStatus = S(o, "trang_thai_don_hang"), oldPay = S(o, "trang_thai_thanh_toan");
        string status = f.S("trang_thai_don_hang"), pay = f.S("trang_thai_thanh_toan"), reason = f.S("ly_do_huy");
        if (status is not ("cho_xac_nhan" or "dang_chuan_bi" or "dang_giao" or "da_giao" or "da_huy")) status = oldStatus;
        if (pay is not ("chua_thanh_toan" or "da_thanh_toan")) pay = oldPay;
        // Đơn đã hủy hoặc đã giao là trạng thái cuối: không đổi được nữa
        if (oldStatus == "da_huy") throw new UserError("Đơn hàng đã hủy, không thể thay đổi trạng thái.");
        if (oldStatus == "da_giao") throw new UserError("Đơn hàng đã giao, không thể thay đổi trạng thái.");
        if (oldPay == "da_thanh_toan" && pay == "chua_thanh_toan") throw new UserError("Đơn đã thanh toán và lập hóa đơn, không chuyển lại \"Chưa thanh toán\".");
        // Đơn chưa thanh toán thì chưa được xử lý tiếp: phải chuyển sang "Đã thanh toán" trước, rồi mới chọn đang chuẩn bị / đang giao / đã giao / đã hủy
        if (pay == "chua_thanh_toan" && status != "cho_xac_nhan") throw new UserError("Đơn chưa thanh toán nên chỉ ở trạng thái \"Chờ xác nhận\". Vui lòng chuyển sang \"Đã thanh toán\" trước khi đổi trạng thái đơn.");

        string msg = "Đã cập nhật đơn hàng.";
        if (status == "da_huy")
        {
            if (reason.Length == 0) throw new UserError("Vui lòng nhập lý do hủy.");
            // Hủy đơn: hoàn lại số lượng vào tồn kho
            c.Execute("UPDATE BIEN_THE_SP bt JOIN CHI_TIET_DON_HANG ct ON ct.ma_bien_the = bt.ma_bien_the SET bt.so_luong_ton = bt.so_luong_ton + ct.so_luong WHERE ct.ma_don_hang = @id", new { id }, tx);
            msg = "Đã hủy đơn hàng và hoàn lại tồn kho.";
            // Đơn đã thanh toán / lập hóa đơn: hoàn tiền cho khách, không tính vào doanh thu và tổng chi tiêu
            decimal refunded = RefundInvoice(c, tx, id);
            // Đã thanh toán nhưng chưa giao (chưa có hóa đơn): vẫn hoàn lại số tiền khách đã trả
            if (refunded == 0 && oldPay == "da_thanh_toan") refunded = c.ExecuteScalar<decimal>("SELECT tong_thanh_toan FROM DON_HANG WHERE ma_don_hang=@id", new { id }, tx);
            if (refunded > 0) { pay = "chua_thanh_toan"; msg = $"Đã hủy đơn hàng, hoàn lại tồn kho và hoàn {refunded:N0}đ cho khách (không tính vào doanh thu và tổng chi tiêu)."; }
        }
        c.Execute("UPDATE DON_HANG SET trang_thai_don_hang=@status, ly_do_huy=@reason, trang_thai_thanh_toan=@pay WHERE ma_don_hang=@id",
            new { status, reason = status == "da_huy" ? reason : null, pay, id }, tx);

        // Vận chuyển: lưu đơn vị + mã vận đơn, trạng thái giao theo trạng thái đơn
        string carrier = f.S("ten_don_vi_van_chuyen");
        string shipStatus = status switch { "dang_giao" => "dang_giao", "da_giao" => "da_giao", _ => "cho_lay_hang" };
        if (carrier.Length > 0)
            c.Execute("""
                INSERT INTO VAN_CHUYEN (ma_don_hang, ten_don_vi_van_chuyen, ma_van_don, phi_van_chuyen, trang_thai, ngay_giao_thuc_te)
                VALUES (@id, @carrier, @track, 0, @shipStatus, IF(@shipStatus = 'da_giao', CURDATE(), NULL))
                ON DUPLICATE KEY UPDATE ten_don_vi_van_chuyen = VALUES(ten_don_vi_van_chuyen), ma_van_don = VALUES(ma_van_don),
                                        trang_thai = VALUES(trang_thai), ngay_giao_thuc_te = VALUES(ngay_giao_thuc_te)
                """, new { id, carrier, track = f.N("ma_van_don"), shipStatus }, tx);
        else
            c.Execute("UPDATE VAN_CHUYEN SET trang_thai=@shipStatus, ngay_giao_thuc_te = IF(@shipStatus = 'da_giao', CURDATE(), NULL) WHERE ma_don_hang=@id", new { shipStatus, id }, tx);

        // Đơn đã thanh toán và giao thành công: lập hóa đơn, cộng doanh thu, điểm tích lũy / tổng chi tiêu, xét lại hạng thành viên
        if (status == "da_giao" && pay == "da_thanh_toan" && c.ExecuteScalar<int>("SELECT COUNT(*) FROM HOA_DON WHERE ma_don_hang=@id", new { id }, tx) == 0)
            msg = "Đơn đã giao: lập hóa đơn " + CreateInvoice(c, tx, id) + ", cộng doanh thu và điểm tích lũy cho khách.";
        return msg;
    }

    /// <summary>
    /// Hoàn tiền một đơn đã lập hóa đơn mà bị hủy: xóa hóa đơn (không còn tính vào doanh thu), trừ số tiền đó khỏi tổng chi tiêu
    /// và điểm tích lũy của khách, hạ hạng thành viên nếu tổng chi tiêu không còn đạt mốc. Trả về số tiền đã hoàn (0 nếu đơn chưa có hóa đơn).
    /// </summary>
    decimal RefundInvoice(MySqlConnection c, MySqlTransaction tx, int order)
    {
        var inv = One(c, "SELECT h.ma_hoa_don, h.tong_thanh_toan, d.ma_khach_hang FROM HOA_DON h JOIN DON_HANG d ON d.ma_don_hang = h.ma_don_hang WHERE h.ma_don_hang=@order", new { order }, tx);
        if (inv == null) return 0;
        int invoice = int.Parse(S(inv, "ma_hoa_don")), customer = int.Parse(S(inv, "ma_khach_hang"));
        decimal total = decimal.Parse(Dec(inv, "tong_thanh_toan"), CultureInfo.InvariantCulture);
        c.Execute("DELETE FROM CHI_TIET_HOA_DON WHERE ma_hoa_don=@invoice", new { invoice }, tx);
        c.Execute("DELETE FROM HOA_DON WHERE ma_hoa_don=@invoice", new { invoice }, tx);

        var kh = One(c, "SELECT tong_chi_tieu, diem_tich_luy, hang_thanh_vien FROM KHACH_HANG WHERE ma_khach_hang=@customer", new { customer }, tx)!;
        decimal spent = Math.Max(0, decimal.Parse(Dec(kh, "tong_chi_tieu"), CultureInfo.InvariantCulture) - total);
        int points = Math.Max(0, int.Parse(S(kh, "diem_tich_luy")) - (int)Math.Floor(total / 10000));
        string[] tiers = ["thuong", "bac", "vang", "VIP"];
        // Hạng hiện tại tính theo tổng chi tiêu trước khi hoàn (khách chưa chi tiêu thì là hạng thường)
        string currentTier = MemberTier.Effective(S(kh, "hang_thanh_vien"), spent + total);
        int oldRank = Math.Max(0, Array.IndexOf(tiers, currentTier));
        int rankBySpent = spent >= 10_000_000 ? 3 : spent >= 5_000_000 ? 2 : spent >= 2_000_000 ? 1 : 0;
        c.Execute("UPDATE KHACH_HANG SET tong_chi_tieu=@spent, diem_tich_luy=@points, hang_thanh_vien=@tier WHERE ma_khach_hang=@customer",
            new { spent, points, tier = tiers[Math.Min(oldRank, rankBySpent)], customer }, tx);
        return total;
    }

    /// <summary>
    /// Dọn dữ liệu cũ khi web admin khởi động: đơn đã hủy mà vẫn còn hóa đơn (hủy trước khi có chức năng hoàn tiền)
    /// thì hoàn tiền lại cho khách. Trả về số đơn đã xử lý; lỗi kết nối được bỏ qua.
    /// </summary>
    public int ReconcileCancelled()
    {
        try
        {
            using var conn = new MySqlConnection(_cs);
            conn.Open();
            using var tx = conn.BeginTransaction();
            var orders = conn.Query<int>("SELECT d.ma_don_hang FROM DON_HANG d JOIN HOA_DON h ON h.ma_don_hang = d.ma_don_hang WHERE d.trang_thai_don_hang = 'da_huy'", transaction: tx).ToList();
            foreach (var o in orders)
            {
                RefundInvoice(conn, tx, o);
                conn.Execute("UPDATE DON_HANG SET trang_thai_thanh_toan='chua_thanh_toan' WHERE ma_don_hang=@o", new { o }, tx);
            }
            tx.Commit();
            return orders.Count;
        }
        catch { return 0; }
    }

    /// <summary>
    /// Lập hóa đơn cho đơn đã thanh toán: lưu giá vốn tại thời điểm bán, cộng 1 điểm / 10.000đ và tổng chi tiêu cho khách,
    /// nâng hạng thành viên theo mốc chi tiêu (Bạc từ 2 triệu, Vàng từ 5 triệu, VIP từ 10 triệu; chỉ nâng, không hạ). Trả về số hóa đơn.
    /// </summary>
    string CreateInvoice(MySqlConnection c, MySqlTransaction tx, int order)
    {
        var d = One(c, "SELECT ma_khach_hang, ma_phuong_thuc_thanh_toan, tam_tinh, tong_tien_giam_gia, tong_thanh_toan FROM DON_HANG WHERE ma_don_hang=@order", new { order }, tx)!;
        decimal ship = c.ExecuteScalar<decimal?>("SELECT phi_van_chuyen FROM VAN_CHUYEN WHERE ma_don_hang=@order", new { order }, tx) ?? 0;
        decimal total = decimal.Parse(Dec(d, "tong_thanh_toan"), CultureInfo.InvariantCulture);
        string number = "HD" + (c.ExecuteScalar<int>("SELECT IFNULL(MAX(ma_hoa_don), 0) + 1 FROM HOA_DON", transaction: tx)).ToString("D5");
        int invoice = c.ExecuteScalar<int>("""
            INSERT INTO HOA_DON (so_hoa_don, ma_don_hang, ma_phuong_thuc_thanh_toan, ngay_thanh_toan, ngay_xuat, tong_tien, tong_tien_giam_gia, phi_van_chuyen, tong_thanh_toan)
            VALUES (@number, @order, @pay, NOW(), NOW(), @sub, @disc, @ship, @total); SELECT LAST_INSERT_ID();
            """, new { number, order, pay = int.Parse(S(d, "ma_phuong_thuc_thanh_toan")), sub = decimal.Parse(Dec(d, "tam_tinh"), CultureInfo.InvariantCulture),
                       disc = decimal.Parse(Dec(d, "tong_tien_giam_gia"), CultureInfo.InvariantCulture), ship, total }, tx);
        c.Execute("""
            INSERT INTO CHI_TIET_HOA_DON (ma_hoa_don, ma_bien_the, so_luong, don_gia, gia_von, giam_gia, thanh_tien)
            SELECT @invoice, ct.ma_bien_the, ct.so_luong, ct.don_gia, bt.gia_von, ct.giam_gia, ct.thanh_tien
            FROM CHI_TIET_DON_HANG ct JOIN BIEN_THE_SP bt ON bt.ma_bien_the = ct.ma_bien_the WHERE ct.ma_don_hang = @order
            """, new { invoice, order }, tx);

        int customer = int.Parse(S(d, "ma_khach_hang"));
        var kh = One(c, "SELECT tong_chi_tieu, hang_thanh_vien FROM KHACH_HANG WHERE ma_khach_hang=@customer", new { customer }, tx)!;
        decimal spent = decimal.Parse(Dec(kh, "tong_chi_tieu"), CultureInfo.InvariantCulture) + total;
        string[] tiers = ["thuong", "bac", "vang", "VIP"];
        // Hạng hiện tại tính theo tổng chi tiêu trước khi cộng đơn này (khách chưa chi tiêu thì là hạng thường)
        int oldRank = Math.Max(0, Array.IndexOf(tiers, MemberTier.Effective(S(kh, "hang_thanh_vien"), spent - total)));
        int newRank = Math.Max(oldRank, spent >= 10_000_000 ? 3 : spent >= 5_000_000 ? 2 : spent >= 2_000_000 ? 1 : 0);
        c.Execute("UPDATE KHACH_HANG SET tong_chi_tieu=@spent, diem_tich_luy = diem_tich_luy + @pts, hang_thanh_vien=@tier WHERE ma_khach_hang=@customer",
            new { spent, pts = (int)Math.Floor(total / 10000), tier = tiers[newRank], customer }, tx);
        return number;
    }

    // ---------- Khuyến mãi ----------
    string SavePromotion(MySqlConnection c, MySqlTransaction tx, int id, Form f)
    {
        string name = f.S("ten_ctkm"), type = f.S("loai_khuyen_mai"), status = f.S("trang_thai");
        var start = f.Date("ngay_bat_dau"); var end = f.Date("ngay_ket_thuc");
        if (name.Length == 0) throw new UserError("Vui lòng nhập tên chương trình.");
        if (type is not ("theo_san_pham" or "theo_don_hang" or "theo_voucher")) throw new UserError("Vui lòng chọn loại khuyến mãi.");
        if (start == null || end == null) throw new UserError("Vui lòng nhập thời gian áp dụng.");
        if (end <= start) throw new UserError("Ngày kết thúc phải sau ngày bắt đầu.");
        if (status is not ("hoat_dong" or "ngung" or "het_han")) status = "hoat_dong";

        // Mức giảm chi tiết (một dòng) theo loại
        decimal pct = f.D("d_phan_tram");
        if (pct <= 0 || pct > 100) throw new UserError("Phần trăm giảm phải lớn hơn 0 và không quá 100.");
        int product = f.I("d_san_pham"); decimal threshold = f.D("d_muc_tien"); string voucher = f.S("d_voucher");
        if (type == "theo_san_pham" && product == 0) throw new UserError("Vui lòng chọn sản phẩm được giảm.");
        if (type == "theo_don_hang" && threshold <= 0) throw new UserError("Vui lòng nhập mốc tiền đơn hàng.");
        if (type == "theo_voucher" && voucher.Length == 0) throw new UserError("Vui lòng nhập mã voucher.");

        var p = new { name, type, status, start, end, max = f.ND("gia_tri_giam_toi_da"), tier = f.N("hang_ap_dung"), limit = f.NI("so_luong_gioi_han"), per = f.NI("gioi_han_khach_hang"), id };
        if (id == 0)
            id = c.ExecuteScalar<int>("""
                INSERT INTO CTKM (ten_ctkm, loai_khuyen_mai, ngay_bat_dau, ngay_ket_thuc, gia_tri_giam_toi_da, hang_ap_dung, so_luong_gioi_han, gioi_han_khach_hang, trang_thai)
                VALUES (@name, @type, @start, @end, @max, @tier, @limit, @per, @status); SELECT LAST_INSERT_ID();
                """, p, tx);
        else
        {
            c.Execute("""
                UPDATE CTKM SET ten_ctkm=@name, loai_khuyen_mai=@type, ngay_bat_dau=@start, ngay_ket_thuc=@end, gia_tri_giam_toi_da=@max,
                       hang_ap_dung=@tier, so_luong_gioi_han=@limit, gioi_han_khach_hang=@per, trang_thai=@status WHERE ma_ctkm=@id
                """, p, tx);
            c.Execute("DELETE FROM KM_THEO_SP WHERE ma_ctkm=@id", new { id }, tx);
            c.Execute("DELETE FROM KM_THEO_DH WHERE ma_ctkm=@id", new { id }, tx);
            c.Execute("DELETE FROM KM_THEO_VOUCHER WHERE ma_ctkm=@id", new { id }, tx);
        }
        if (type == "theo_san_pham") c.Execute("INSERT INTO KM_THEO_SP (ma_ctkm, ma_san_pham, phan_tram_giam) VALUES (@id, @product, @pct)", new { id, product, pct }, tx);
        else if (type == "theo_don_hang") c.Execute("INSERT INTO KM_THEO_DH (ma_ctkm, muc_tien_don_hang, phan_tram_giam) VALUES (@id, @threshold, @pct)", new { id, threshold, pct }, tx);
        else c.Execute("INSERT INTO KM_THEO_VOUCHER (ma_ctkm, ma_voucher, phan_tram_giam) VALUES (@id, @voucher, @pct)", new { id, voucher, pct }, tx);
        return $"Đã lưu chương trình khuyến mãi \"{name}\".";
    }

    // ---------- Chiến dịch thông báo ----------
    string SaveCampaign(MySqlConnection c, MySqlTransaction tx, int id, Form f, Ctx me)
    {
        string title = f.S("tieu_de"), content = f.S("noi_dung"), channel = f.S("kenh_gui"), status = f.S("trang_thai") == "da_gui" ? "da_gui" : "nhap";
        if (title.Length == 0 || content.Length == 0) throw new UserError("Vui lòng nhập tiêu đề và nội dung.");
        if (channel is not ("email" or "sms" or "app")) throw new UserError("Vui lòng chọn kênh gửi.");
        var p = new { title, content, channel, ctkm = f.NI("ma_ctkm"), group = f.NI("ma_nhom_kh"), status, emp = me.EmployeeId, id };
        if (id == 0)
            c.Execute("""
                INSERT INTO CHIEN_DICH_THONG_BAO (tieu_de, noi_dung, kenh_gui, ma_ctkm, ma_nhom_kh, thoi_gian_gui, trang_thai, ma_nhan_vien)
                VALUES (@title, @content, @channel, @ctkm, @group, IF(@status = 'da_gui', NOW(), NULL), @status, @emp)
                """, p, tx);
        else
            c.Execute("""
                UPDATE CHIEN_DICH_THONG_BAO SET tieu_de=@title, noi_dung=@content, kenh_gui=@channel, ma_ctkm=@ctkm, ma_nhom_kh=@group,
                       thoi_gian_gui = IF(@status = 'da_gui', IFNULL(thoi_gian_gui, NOW()), NULL), trang_thai=@status WHERE ma_chien_dich=@id
                """, p, tx);
        return $"Đã lưu chiến dịch \"{title}\".";
    }

    // ---------- Khảo sát ----------
    string SaveSurvey(MySqlConnection c, MySqlTransaction tx, int id, Form f, Ctx me)
    {
        string title = f.S("tieu_de"); var start = f.Date("ngay_bat_dau"); var end = f.Date("ngay_ket_thuc");
        string status = f.S("trang_thai"); if (status is not ("nhap" or "da_gui" or "da_dong")) status = "nhap";
        if (title.Length == 0) throw new UserError("Vui lòng nhập tiêu đề khảo sát.");
        if (start == null || end == null) throw new UserError("Vui lòng nhập thời gian khảo sát.");
        if (end <= start) throw new UserError("Ngày kết thúc phải sau ngày bắt đầu.");

        // Đọc các câu hỏi gửi lên (lặp theo thứ tự)
        var texts = f.All("q_noi_dung"); var types = f.All("q_loai"); var opts = f.All("q_lua_chon"); var req = f.Many("q_bat_buoc");
        var questions = new List<(string Text, string Type, bool Required, string[] Options)>();
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].Length == 0) continue;
            string type = i < types.Length ? types[i] : "van_ban";
            if (type is not ("mot_lua_chon" or "nhieu_lua_chon" or "thang_diem" or "van_ban")) type = "van_ban";
            var o = (i < opts.Length ? opts[i] : "").Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            if (type == "thang_diem" && o.Length == 0) o = ["1", "2", "3", "4", "5"];
            if (type is "mot_lua_chon" or "nhieu_lua_chon" && o.Length < 2) throw new UserError($"Câu hỏi \"{texts[i]}\" cần ít nhất 2 lựa chọn (mỗi dòng một lựa chọn).");
            questions.Add((texts[i], type, req.Contains(i.ToString()), type == "van_ban" ? [] : o));
        }
        if (id == 0 && questions.Count == 0) throw new UserError("Khảo sát cần có ít nhất một câu hỏi.");

        var p = new { title, desc = f.N("mo_ta"), start, end, status, emp = me.EmployeeId, id };
        bool replaceQuestions = true;
        if (id == 0)
            id = c.ExecuteScalar<int>("""
                INSERT INTO KHAO_SAT (tieu_de, mo_ta, ngay_bat_dau, ngay_ket_thuc, trang_thai, ma_nhan_vien, ngay_tao) VALUES (@title, @desc, @start, @end, @status, @emp, NOW());
                SELECT LAST_INSERT_ID();
                """, p, tx);
        else
        {
            c.Execute("UPDATE KHAO_SAT SET tieu_de=@title, mo_ta=@desc, ngay_bat_dau=@start, ngay_ket_thuc=@end, trang_thai=@status WHERE ma_khao_sat=@id", p, tx);
            // Đã có khách làm khảo sát thì giữ nguyên câu hỏi để không mất kết quả
            replaceQuestions = c.ExecuteScalar<int>("SELECT COUNT(*) FROM TRA_LOI_KHAO_SAT WHERE ma_khao_sat=@id", new { id }, tx) == 0 && questions.Count > 0;
            if (replaceQuestions)
            {
                c.Execute("DELETE FROM LUA_CHON_CAU_HOI WHERE ma_cau_hoi IN (SELECT ma_cau_hoi FROM CAU_HOI_KHAO_SAT WHERE ma_khao_sat=@id)", new { id }, tx);
                c.Execute("DELETE FROM CAU_HOI_KHAO_SAT WHERE ma_khao_sat=@id", new { id }, tx);
            }
        }
        if (replaceQuestions)
            for (int i = 0; i < questions.Count; i++)
            {
                var q = questions[i];
                int qid = c.ExecuteScalar<int>("""
                    INSERT INTO CAU_HOI_KHAO_SAT (ma_khao_sat, noi_dung, loai_cau_hoi, bat_buoc, thu_tu) VALUES (@id, @text, @type, @required, @order);
                    SELECT LAST_INSERT_ID();
                    """, new { id, text = q.Text, type = q.Type, required = q.Required, order = i + 1 }, tx);
                foreach (var o in q.Options)
                    c.Execute("INSERT INTO LUA_CHON_CAU_HOI (ma_cau_hoi, noi_dung) VALUES (@qid, @o)", new { qid, o }, tx);
            }
        return $"Đã lưu khảo sát \"{title}\"" + (replaceQuestions ? $" ({questions.Count} câu hỏi)." : ". Khảo sát đã có người trả lời nên giữ nguyên câu hỏi.");
    }

    /// <summary>Gửi khảo sát đến tài khoản khách hàng (tất cả hoặc theo nhóm); mỗi khách chỉ nhận một lần.</summary>
    string SendSurvey(MySqlConnection c, MySqlTransaction tx, int id, Form f)
    {
        var s = One(c, "SELECT trang_thai FROM KHAO_SAT WHERE ma_khao_sat=@id", new { id }, tx) ?? throw new UserError("Không tìm thấy khảo sát.");
        if (S(s, "trang_thai") == "da_dong") throw new UserError("Khảo sát đã đóng, không gửi thêm được.");
        if (c.ExecuteScalar<int>("SELECT COUNT(*) FROM CAU_HOI_KHAO_SAT WHERE ma_khao_sat=@id", new { id }, tx) == 0) throw new UserError("Khảo sát chưa có câu hỏi.");
        int? group = f.NI("ma_nhom_kh");
        int n = c.Execute("""
            INSERT IGNORE INTO KHAO_SAT_KHACH_HANG (ma_khao_sat, ma_khach_hang, ngay_gui, trang_thai)
            SELECT @id, kh.ma_khach_hang, NOW(), 'chua_lam' FROM KHACH_HANG kh
            JOIN TAI_KHOAN tk ON tk.ma_tai_khoan = kh.ma_tai_khoan AND tk.trang_thai = 'hoat_dong'
            WHERE @group IS NULL OR kh.ma_khach_hang IN (SELECT x.ma_khach_hang FROM KHACH_HANG_NHOM_KH x WHERE x.ma_nhom_kh = @group)
            """, new { id, group }, tx);
        c.Execute("UPDATE KHAO_SAT SET trang_thai='da_gui' WHERE ma_khao_sat=@id AND trang_thai='nhap'", new { id }, tx);
        return n == 0 ? "Không có khách mới để gửi (các khách đủ điều kiện đều đã nhận khảo sát này)." : $"Đã gửi khảo sát đến {n} khách hàng.";
    }

    // ============================================================
    //  Xóa
    // ============================================================

    public Result Delete(string entity, int id, Ctx me) => Run((c, tx) =>
    {
        switch (entity)
        {
            case "account":
                {
                    var u = c.ExecuteScalar<string>("SELECT ten_dang_nhap FROM TAI_KHOAN WHERE ma_tai_khoan=@id", new { id }, tx) ?? throw new UserError("Không tìm thấy tài khoản.");
                    if (u == me.Username) throw new UserError("Không thể xóa tài khoản đang đăng nhập.");
                    // Xóa mềm: chuyển sang "đã xóa", dữ liệu liên quan (hóa đơn, phản hồi...) được giữ lại
                    c.Execute("UPDATE TAI_KHOAN SET trang_thai='da_xoa' WHERE ma_tai_khoan=@id", new { id }, tx);
                    return $"Đã xóa tài khoản {u} (chuyển sang trạng thái \"đã xóa\", dữ liệu liên quan vẫn được giữ).";
                }
            case "employee":
                {
                    if (id == me.EmployeeId) throw new UserError("Không thể cho chính mình nghỉ việc.");
                    c.Execute("UPDATE NHAN_VIEN SET trang_thai='nghi_viec' WHERE ma_nhan_vien=@id", new { id }, tx);
                    return "Đã chuyển nhân viên sang \"nghỉ việc\" (lịch sử hóa đơn, phiếu nhập, chăm sóc vẫn được giữ).";
                }
            case "customer":
                {
                    var acc = c.ExecuteScalar<int?>("SELECT ma_tai_khoan FROM KHACH_HANG WHERE ma_khach_hang=@id", new { id }, tx);
                    try
                    {
                        foreach (var t in new[] { "SO_DIA_CHI", "KHACH_HANG_SO_THICH", "KHACH_HANG_NHOM_KH", "LICH_SU_CHAM_SOC" })
                            c.Execute($"DELETE FROM {t} WHERE ma_khach_hang=@id", new { id }, tx);
                        c.Execute("DELETE FROM KHACH_HANG WHERE ma_khach_hang=@id", new { id }, tx);
                    }
                    catch (MySqlException e) when (e.Number == 1451)
                    {
                        throw new UserError("Khách hàng đã có đơn hàng, phản hồi hoặc khảo sát nên không thể xóa. Hãy dùng \"Khóa tài khoản\" để ngừng hoạt động.");
                    }
                    if (acc != null) c.Execute("UPDATE TAI_KHOAN SET trang_thai='da_xoa' WHERE ma_tai_khoan=@acc", new { acc }, tx);
                    return "Đã xóa khách hàng.";
                }
            case "group":
                {
                    c.Execute("DELETE FROM KHACH_HANG_NHOM_KH WHERE ma_nhom_kh=@id", new { id }, tx);
                    try { c.Execute("DELETE FROM NHOM_KHACH_HANG WHERE ma_nhom_kh=@id", new { id }, tx); }
                    catch (MySqlException e) when (e.Number == 1451) { throw new UserError("Nhóm đang được dùng trong chiến dịch thông báo nên không xóa được."); }
                    return "Đã xóa nhóm khách hàng.";
                }
            case "category":
                {
                    var name = c.ExecuteScalar<string>("SELECT ten_loai FROM LOAI_SP WHERE ma_loai=@id", new { id }, tx) ?? throw new UserError("Không tìm thấy loại sản phẩm.");
                    int removed = 0, kept = 0;
                    int? archiveId = null;
                    foreach (var product in c.Query<int>("SELECT ma_san_pham FROM SAN_PHAM WHERE ma_loai=@id", new { id }, tx).ToList())
                    {
                        if (ProductHasHistory(c, tx, product))
                        {
                            // Đã có đơn hàng / phiếu nhập / khuyến mãi / phản hồi: giữ lại để lịch sử không mất,
                            // nhưng ẩn khỏi cửa hàng và chuyển sang loại ẩn "[Đã xóa]" (không hiện ở admin và web khách)
                            archiveId ??= ArchivedCategoryId(c, tx);
                            c.Execute("UPDATE SAN_PHAM SET trang_thai='an', ma_loai=@archiveId WHERE ma_san_pham=@product", new { archiveId, product }, tx);
                            kept++;
                        }
                        else
                        {
                            c.Execute("DELETE FROM BIEN_THE_SP WHERE ma_san_pham=@product", new { product }, tx);
                            c.Execute("DELETE FROM SAN_PHAM WHERE ma_san_pham=@product", new { product }, tx);
                            removed++;
                        }
                    }
                    c.Execute("DELETE FROM LOAI_SP WHERE ma_loai=@id", new { id }, tx);
                    string detail = removed + kept == 0 ? "" : $" {removed} sản phẩm đã xóa" + (kept > 0 ? $", {kept} sản phẩm đã có lịch sử mua nên được ẩn khỏi cửa hàng (lịch sử mua hàng vẫn giữ)." : ".");
                    return $"Đã xóa loại \"{name}\".{detail}";
                }
            case "product":
                {
                    try
                    {
                        c.Execute("DELETE FROM BIEN_THE_SP WHERE ma_san_pham=@id", new { id }, tx);
                        c.Execute("DELETE FROM SAN_PHAM WHERE ma_san_pham=@id", new { id }, tx);
                    }
                    catch (MySqlException e) when (e.Number == 1451)
                    {
                        throw new UserError("Sản phẩm đã phát sinh đơn hàng, phiếu nhập, khuyến mãi hoặc phản hồi nên không thể xóa. Hãy chuyển sang trạng thái \"Đang ẩn\".");
                    }
                    return "Đã xóa sản phẩm.";
                }
            case "variant":
                try { c.Execute("DELETE FROM BIEN_THE_SP WHERE ma_bien_the=@id", new { id }, tx); }
                catch (MySqlException e) when (e.Number == 1451) { throw new UserError("Biến thể đã phát sinh đơn hàng hoặc phiếu nhập nên không thể xóa. Hãy chuyển sang \"Đang ẩn\"."); }
                return "Đã xóa biến thể.";
            case "supplier":
                try { c.Execute("DELETE FROM NHA_CUNG_CAP WHERE ma_ncc=@id", new { id }, tx); return "Đã xóa nhà cung cấp."; }
                catch (MySqlException e) when (e.Number == 1451)
                {
                    c.Execute("UPDATE NHA_CUNG_CAP SET trang_thai='ngung_hop_tac' WHERE ma_ncc=@id", new { id }, tx);
                    return "Nhà cung cấp đã có phiếu nhập nên được chuyển sang \"Ngừng hợp tác\" thay vì xóa.";
                }
            case "promotion":
                {
                    bool used = c.ExecuteScalar<int>("SELECT (SELECT COUNT(*) FROM LUOT_SU_DUNG_KHUYEN_MAI WHERE ma_ctkm=@id) + (SELECT COUNT(*) FROM DON_HANG WHERE ma_ctkm=@id) + (SELECT COUNT(*) FROM CHIEN_DICH_THONG_BAO WHERE ma_ctkm=@id)", new { id }, tx) > 0;
                    if (used)
                    {
                        c.Execute("UPDATE CTKM SET trang_thai='ngung' WHERE ma_ctkm=@id", new { id }, tx);
                        return "Chương trình đã được sử dụng nên được chuyển sang \"Ngừng\" thay vì xóa.";
                    }
                    foreach (var t in new[] { "KM_THEO_SP", "KM_THEO_DH", "KM_THEO_VOUCHER" }) c.Execute($"DELETE FROM {t} WHERE ma_ctkm=@id", new { id }, tx);
                    c.Execute("DELETE FROM CTKM WHERE ma_ctkm=@id", new { id }, tx);
                    return "Đã xóa chương trình khuyến mãi.";
                }
            case "campaign":
                c.Execute("DELETE FROM CHIEN_DICH_THONG_BAO WHERE ma_chien_dich=@id", new { id }, tx);
                return "Đã xóa chiến dịch.";
            case "survey":
                {
                    if (c.ExecuteScalar<int>("SELECT COUNT(*) FROM KHAO_SAT_KHACH_HANG WHERE ma_khao_sat=@id", new { id }, tx) > 0)
                        throw new UserError("Khảo sát đã gửi cho khách nên không xóa được. Hãy chuyển sang trạng thái \"Đã đóng\".");
                    c.Execute("DELETE FROM LUA_CHON_CAU_HOI WHERE ma_cau_hoi IN (SELECT ma_cau_hoi FROM CAU_HOI_KHAO_SAT WHERE ma_khao_sat=@id)", new { id }, tx);
                    c.Execute("DELETE FROM CAU_HOI_KHAO_SAT WHERE ma_khao_sat=@id", new { id }, tx);
                    c.Execute("DELETE FROM KHAO_SAT WHERE ma_khao_sat=@id", new { id }, tx);
                    return "Đã xóa khảo sát.";
                }
            case "order":
                throw new UserError("Đơn hàng không xóa được. Hãy dùng trạng thái \"Đã hủy\" (kèm lý do) để hủy đơn.");
            case "import":
                throw new UserError("Phiếu nhập đã cập nhật tồn kho và giá vốn nên không xóa được.");
            default:
                throw new UserError("Không hỗ trợ thao tác này.");
        }
    });

    // ============================================================
    //  Các thao tác nhanh (nút bấm không cần biểu mẫu)
    // ============================================================

    public Result Act(string action, int id, Form f, Ctx me) => Run((c, tx) =>
    {
        switch (action)
        {
            // Khóa / mở khóa tài khoản của khách hàng (id = mã khách hàng)
            case "lock_customer":
                {
                    var acc = One(c, "SELECT tk.ma_tai_khoan, tk.trang_thai FROM KHACH_HANG kh JOIN TAI_KHOAN tk ON tk.ma_tai_khoan = kh.ma_tai_khoan WHERE kh.ma_khach_hang=@id", new { id }, tx)
                        ?? throw new UserError("Khách hàng này chưa có tài khoản đăng nhập nên không thể khóa.");
                    string now = S(acc, "trang_thai") == "hoat_dong" ? "bi_khoa" : "hoat_dong";
                    c.Execute("UPDATE TAI_KHOAN SET trang_thai=@now WHERE ma_tai_khoan=@a", new { now, a = int.Parse(S(acc, "ma_tai_khoan")) }, tx);
                    return now == "bi_khoa" ? "Đã khóa tài khoản khách hàng (không đăng nhập được, dữ liệu vẫn được giữ)." : "Đã mở khóa tài khoản khách hàng.";
                }
            // Trả lời phản hồi của khách (id = mã phản hồi)
            case "feedback_reply":
                {
                    string text = f.S("noi_dung");
                    if (text.Length == 0) throw new UserError("Vui lòng nhập nội dung trả lời.");
                    if (text.Length > 1000) throw new UserError("Nội dung trả lời tối đa 1000 ký tự.");
                    if (c.ExecuteScalar<int>("SELECT COUNT(*) FROM PHAN_HOI WHERE ma_phan_hoi=@id", new { id }, tx) == 0) throw new UserError("Không tìm thấy phản hồi.");
                    c.Execute("INSERT INTO TRA_LOI_PHAN_HOI (ma_phan_hoi, ma_nhan_vien, noi_dung, ngay_tra_loi) VALUES (@id, @emp, @text, NOW())", new { id, emp = me.EmployeeId, text }, tx);
                    c.Execute("UPDATE PHAN_HOI SET trang_thai='da_tra_loi' WHERE ma_phan_hoi=@id", new { id }, tx);
                    return "Đã gửi câu trả lời cho khách hàng.";
                }
            // Ẩn / hiện phản hồi (nội dung không phù hợp)
            case "feedback_toggle":
                {
                    c.Execute("UPDATE PHAN_HOI SET an_hien = IF(an_hien = 1, 0, 1) WHERE ma_phan_hoi=@id", new { id }, tx);
                    return "Đã đổi trạng thái hiển thị của phản hồi.";
                }
            // Đóng khảo sát (id = mã khảo sát)
            case "survey_close":
                {
                    c.Execute("UPDATE KHAO_SAT SET trang_thai='da_dong' WHERE ma_khao_sat=@id", new { id }, tx);
                    return "Đã đóng khảo sát.";
                }
            default:
                throw new UserError("Không hỗ trợ thao tác này.");
        }
    });

    // ============================================================
    //  Hàm tiện ích
    // ============================================================

    /// <summary>Chạy một thao tác trong transaction; lỗi nghiệp vụ / lỗi MySQL được đổi thành thông báo tiếng Việt.</summary>
    Result Run(Func<MySqlConnection, MySqlTransaction, string> work)
    {
        try
        {
            using var conn = new MySqlConnection(_cs);
            conn.Open();
            using var tx = conn.BeginTransaction();
            try { var msg = work(conn, tx); tx.Commit(); return new Result(true, msg); }
            catch { tx.Rollback(); throw; }
        }
        catch (UserError e) { return new Result(false, e.Message); }
        catch (MySqlException e) { return new Result(false, Friendly(e)); }
    }


    /// <summary>Sản phẩm đã gắn với dữ liệu khác (đơn hàng, hóa đơn, phiếu nhập, khuyến mãi theo sản phẩm, phản hồi) nên không được xóa hẳn.</summary>
    static bool ProductHasHistory(MySqlConnection c, MySqlTransaction tx, int product) => c.ExecuteScalar<int>("""
        SELECT (SELECT COUNT(*) FROM CHI_TIET_DON_HANG x JOIN BIEN_THE_SP b ON b.ma_bien_the = x.ma_bien_the WHERE b.ma_san_pham = @product)
             + (SELECT COUNT(*) FROM CHI_TIET_HOA_DON x JOIN BIEN_THE_SP b ON b.ma_bien_the = x.ma_bien_the WHERE b.ma_san_pham = @product)
             + (SELECT COUNT(*) FROM CHI_TIET_PHIEU_NHAP_HANG x JOIN BIEN_THE_SP b ON b.ma_bien_the = x.ma_bien_the WHERE b.ma_san_pham = @product)
             + (SELECT COUNT(*) FROM KM_THEO_SP WHERE ma_san_pham = @product)
             + (SELECT COUNT(*) FROM PHAN_HOI WHERE ma_san_pham = @product)
        """, new { product }, tx) > 0;

    /// <summary>Mã của loại ẩn "[Đã xóa]" (tạo nếu chưa có), nơi giữ các sản phẩm đã có lịch sử khi xóa loại sản phẩm.</summary>
    static int ArchivedCategoryId(MySqlConnection c, MySqlTransaction tx)
    {
        var id = c.ExecuteScalar<int?>("SELECT ma_loai FROM LOAI_SP WHERE ten_loai=@name", new { name = Catalog.ArchivedCategory }, tx);
        return id ?? c.ExecuteScalar<int>("INSERT INTO LOAI_SP (ten_loai) VALUES (@name); SELECT LAST_INSERT_ID();", new { name = Catalog.ArchivedCategory }, tx);
    }
    static string Friendly(MySqlException e) => e.Number switch
    {
        1062 => "Dữ liệu bị trùng (tên đăng nhập, email, mã voucher hoặc biến thể đã tồn tại).",
        1451 => "Dữ liệu này đang được dùng ở nơi khác nên không thể xóa.",
        1452 => "Dữ liệu liên quan không tồn tại, vui lòng kiểm tra lại các ô đã chọn.",
        1406 => "Có ô nhập quá dài.",
        3819 when e.Message.Contains("CK_BT_TON") => "Số lượng tồn kho không đủ.",
        3819 when e.Message.Contains("CK_KH_GIOITINH") => "Giới tính không hợp lệ.",
        3819 => "Có giá trị nhập không hợp lệ (vi phạm ràng buộc dữ liệu).",
        _ => "Lỗi cơ sở dữ liệu: " + e.Message,
    };

    static void CheckEmail(string email)
    {
        if (!Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$")) throw new UserError("Email chưa đúng định dạng.");
    }

    static void CheckPhone(string phone, bool required)
    {
        if (phone.Length == 0) { if (required) throw new UserError("Vui lòng nhập số điện thoại."); return; }
        if (!Regex.IsMatch(phone, @"^0\d{9}$")) throw new UserError("Số điện thoại cần gồm 10 chữ số, bắt đầu bằng 0.");
    }

    static IDictionary<string, object?>? One(MySqlConnection c, string sql, object? args = null, MySqlTransaction? tx = null) =>
        c.Query(sql, args, tx).Cast<IDictionary<string, object?>>().FirstOrDefault();

    static string S(IDictionary<string, object?> r, string k) => r[k] is null or DBNull ? "" : Convert.ToString(r[k], CultureInfo.InvariantCulture)!;
    static string Dec(IDictionary<string, object?> r, string k) => r[k] is null or DBNull ? "0" : Convert.ToDecimal(r[k]).ToString("0.##", CultureInfo.InvariantCulture);
}

static class StringExt
{
    public static string? NullIfEmpty(this string s) => s.Length == 0 ? null : s;
}
