using Dapper;
using MySqlConnector;

namespace SportShop.Admin.Models;

public class ShopData
{
    readonly string _cs;
    readonly DataCache _cache;

    // Đọc dữ liệu cho các trang quản trị từ MySQL (ConnectionStrings:Default): mỗi danh sách nạp bằng Load...() khi dùng lần đầu trong request (Lazy); sản phẩm, biến thể, nhân viên, khách, đơn, chăm sóc, phản hồi được đặt luôn vào DataCache cho trang Báo cáo dùng lại.
    public ShopData(IConfiguration config, DataCache cache)
    {
        _cache = cache;
        _cs = config.GetConnectionString("Default") ?? "";
        if (string.IsNullOrWhiteSpace(_cs))
            throw new InvalidOperationException("Chưa điền chuỗi kết nối MySQL: appsettings.json → ConnectionStrings:Default");

        _categories = new(() => cache.Get<List<Category>>("categories") ?? cache.Put("categories", LoadCategories(), DataCache.LongAge));
        _products = new(() => cache.Get<List<Product>>("products") ?? cache.Put("products", LoadProducts(), DataCache.LongAge));
        _variants = new(() => cache.Put("variants", LoadVariants()));
        _employees = new(() => cache.Put("employees", LoadEmployees()));
        _customers = new(() => cache.Put("customers", LoadCustomers()));
        _groups = new(LoadGroups);
        _orders = new(() => cache.Put("orders", LoadOrders()));
        _careNotes = new(() => cache.Put("care", LoadCareNotes()));
        _feedback = new(() => cache.Put("feedback", LoadFeedback()));
        _accounts = new(LoadAccounts);
        _suppliers = new(LoadSuppliers);
        _imports = new(LoadImports);
        _promotions = new(LoadPromotions);
        _campaigns = new(LoadCampaigns);
        _surveys = new(LoadSurveys);
    }

    readonly Lazy<List<Category>> _categories;
    readonly Lazy<List<Product>> _products;
    readonly Lazy<List<Variant>> _variants;
    readonly Lazy<List<Employee>> _employees;
    readonly Lazy<List<Customer>> _customers;
    readonly Lazy<List<CustomerGroup>> _groups;
    readonly Lazy<List<Order>> _orders;
    readonly Lazy<List<CareNote>> _careNotes;
    readonly Lazy<List<FeedbackItem>> _feedback;
    readonly Lazy<List<Account>> _accounts;
    readonly Lazy<List<Supplier>> _suppliers;
    readonly Lazy<List<ImportSlip>> _imports;
    readonly Lazy<List<Promotion>> _promotions;
    readonly Lazy<List<Campaign>> _campaigns;
    readonly Lazy<List<Survey>> _surveys;

    public List<Category> CategoryList => _categories.Value;
    public List<string> Categories => _categories.Value.Select(c => c.Name).ToList();
    public List<Product> Products => _products.Value;
    public List<Variant> Variants => _variants.Value;
    public List<Product> ActiveProducts => Products.Where(p => p.Category != Catalog.ArchivedCategory).ToList();
    public List<Variant> ActiveVariants { get { var ids = ActiveProducts.Select(p => p.Id).ToHashSet(); return Variants.Where(v => ids.Contains(v.ProductId)).ToList(); } }
    public List<Employee> Employees => _employees.Value;
    public List<Customer> Customers => _customers.Value;
    public List<Customer> ActiveCustomers => Customers.Where(c => !c.Deleted).ToList();
    public List<CustomerGroup> CustomerGroups => _groups.Value;
    public List<Order> Orders => _orders.Value;
    public List<CareNote> CareNotes => _careNotes.Value;
    public List<FeedbackItem> FeedbackItems => _feedback.Value;
    public List<Account> Accounts => _accounts.Value;
    public List<Supplier> Suppliers => _suppliers.Value;
    public List<ImportSlip> ImportSlips => _imports.Value;
    public List<Promotion> Promotions => _promotions.Value;
    public List<Campaign> Campaigns => _campaigns.Value;
    public List<Survey> Surveys => _surveys.Value;

    public Variant Variant(int id) => Variants.First(v => v.Id == id);
    public Product Product(int id) => Products.First(p => p.Id == id);
    public Customer Customer(int id) => Customers.First(c => c.Id == id);
    public Employee Employee(int id) => Employees.First(e => e.Id == id);
    public string VariantName(int id) => VariantName(Product(Variant(id).ProductId), Variant(id));
    public static string VariantName(Product p, Variant v)
    {
        var extra = string.Join(" / ", new[] { v.Size, v.Color }.Where(s => !string.IsNullOrEmpty(s)));
        return extra.Length == 0 ? p.Name : $"{p.Name} ({extra})";
    }

    // Bản đã nạp sẵn để trang Báo cáo dùng lại: request này đã nạp thì lấy luôn, trang khác vừa nạp (trong DataCache) thì lấy từ bộ nhớ đệm, chưa có mới truy vấn MySQL.
    List<T> Reuse<T>(string key, Lazy<List<T>> lazy) => lazy.IsValueCreated ? lazy.Value : _cache.Get<List<T>>(key) ?? lazy.Value;
    public List<Product> ReusedProducts => Reuse("products", _products);
    public List<Variant> ReusedVariants => Reuse("variants", _variants);
    public List<Employee> ReusedEmployees => Reuse("employees", _employees);
    public List<Customer> ReusedCustomers => Reuse("customers", _customers);
    public List<Order> ReusedOrders => Reuse("orders", _orders);
    public List<CareNote> ReusedCareNotes => Reuse("care", _careNotes);
    public List<FeedbackItem> ReusedFeedback => Reuse("feedback", _feedback);

    // Báo cáo (tải mới): mỗi hóa đơn trong [from, to] một dòng gồm ngày, doanh thu sau giảm giá đơn và giá vốn (ưu tiên giá vốn lưu trong CHI_TIET_HOA_DON); MySQL tự cộng dồn nên không phải nạp cả bảng đơn hàng.
    public List<ReportOrder> ReportOrders(DateTime from, DateTime to) =>
        Query("""
            SELECT dh.ngay_dat,
                   IFNULL(SUM(ct.so_luong * ct.don_gia), 0) - dh.tong_tien_giam_gia AS doanh_thu,
                   IFNULL(SUM(ct.so_luong * COALESCE(cthd.gia_von, bt.gia_von)), 0) AS gia_von
            FROM DON_HANG dh
            JOIN HOA_DON hd ON hd.ma_don_hang = dh.ma_don_hang
            LEFT JOIN CHI_TIET_DON_HANG ct ON ct.ma_don_hang = dh.ma_don_hang
            LEFT JOIN BIEN_THE_SP bt ON bt.ma_bien_the = ct.ma_bien_the
            LEFT JOIN CHI_TIET_HOA_DON cthd ON cthd.ma_hoa_don = hd.ma_hoa_don AND cthd.ma_bien_the = ct.ma_bien_the
            WHERE dh.ngay_dat >= @from AND dh.ngay_dat < @to
            GROUP BY dh.ma_don_hang, dh.ngay_dat, dh.tong_tien_giam_gia
            """, new { from, to = to.AddDays(1) })
            .Select(r => new ReportOrder(T(r, "ngay_dat"), M(r, "doanh_thu"), M(r, "gia_von"))).ToList();

    // Báo cáo (tải mới): số lượng bán, doanh thu dòng và giá vốn theo sản phẩm của các hóa đơn trong [from, to], sắp theo lần bán đầu tiên trong kỳ (ngày đặt, mã đơn, mã biến thể).
    public List<ReportProduct> ReportProducts(DateTime from, DateTime to) =>
        Query("""
            SELECT sp.ten_san_pham, SUM(ct.so_luong) AS so_luong,
                   SUM(ct.so_luong * ct.don_gia - ct.giam_gia) AS doanh_thu,
                   SUM(ct.so_luong * COALESCE(cthd.gia_von, bt.gia_von)) AS gia_von
            FROM DON_HANG dh
            JOIN HOA_DON hd ON hd.ma_don_hang = dh.ma_don_hang
            JOIN CHI_TIET_DON_HANG ct ON ct.ma_don_hang = dh.ma_don_hang
            JOIN BIEN_THE_SP bt ON bt.ma_bien_the = ct.ma_bien_the
            JOIN SAN_PHAM sp ON sp.ma_san_pham = bt.ma_san_pham
            LEFT JOIN CHI_TIET_HOA_DON cthd ON cthd.ma_hoa_don = hd.ma_hoa_don AND cthd.ma_bien_the = ct.ma_bien_the
            WHERE dh.ngay_dat >= @from AND dh.ngay_dat < @to
            GROUP BY sp.ma_san_pham, sp.ten_san_pham
            ORDER BY MIN(CONCAT(DATE_FORMAT(dh.ngay_dat, '%Y%m%d%H%i%s'), LPAD(dh.ma_don_hang, 10, '0'), LPAD(ct.ma_bien_the, 10, '0')))
            """, new { from, to = to.AddDays(1) })
            .Select(r => new ReportProduct(S(r, "ten_san_pham"), I(r, "so_luong"), M(r, "doanh_thu"), M(r, "gia_von"))).ToList();

    // Thống kê tổng tiền hóa đơn: mỗi hóa đơn một dòng (số hóa đơn, ngày xuất, tổng thanh toán, nhân viên lập đơn, khách hàng); nạp một lần rồi cất vào DataCache 10 phút, các bảng thống kê cộng dồn trong bộ nhớ (đổi khoảng ngày / nhóm không truy vấn lại).
    public List<InvoiceRow> InvoiceRows() =>
        _cache.Get<List<InvoiceRow>>("invoiceRows") ?? _cache.Put("invoiceRows", Query("""
            SELECT hd.so_hoa_don, hd.ngay_xuat, hd.tong_thanh_toan,
                   CONCAT(nv.ho_lot, ' ', nv.ten) AS nhan_vien, CONCAT(kh.ho_lot, ' ', kh.ten) AS khach_hang
            FROM HOA_DON hd
            JOIN DON_HANG dh ON dh.ma_don_hang = hd.ma_don_hang
            JOIN KHACH_HANG kh ON kh.ma_khach_hang = dh.ma_khach_hang
            LEFT JOIN NHAN_VIEN nv ON nv.ma_nhan_vien = dh.ma_nhan_vien
            ORDER BY hd.ngay_xuat, hd.ma_hoa_don
            """).Select(r => new InvoiceRow(S(r, "so_hoa_don"), T(r, "ngay_xuat"), M(r, "tong_thanh_toan"), S(r, "nhan_vien"), S(r, "khach_hang"))).ToList(), DataCache.LongAge);

    // Thống kê theo sản phẩm: mỗi dòng chi tiết đơn hàng của đơn đã có hóa đơn (ngày xuất hóa đơn, tên sản phẩm, thành tiền dòng).
    public List<InvoiceLine> InvoiceLines() =>
        _cache.Get<List<InvoiceLine>>("invoiceLines") ?? _cache.Put("invoiceLines", Query("""
            SELECT hd.ngay_xuat, sp.ten_san_pham, ct.thanh_tien
            FROM HOA_DON hd
            JOIN CHI_TIET_DON_HANG ct ON ct.ma_don_hang = hd.ma_don_hang
            JOIN BIEN_THE_SP bt ON bt.ma_bien_the = ct.ma_bien_the
            JOIN SAN_PHAM sp ON sp.ma_san_pham = bt.ma_san_pham
            ORDER BY hd.ngay_xuat
            """).Select(r => new InvoiceLine(T(r, "ngay_xuat"), S(r, "ten_san_pham"), M(r, "thanh_tien"))).ToList(), DataCache.LongAge);

    // Ngày hóa đơn gần nhất và số hóa đơn trong [recentFrom, today] (MySQL) để chọn ngày kết thúc mặc định của báo cáo.
    public (DateTime? Last, int Recent) InvoiceDates(DateTime recentFrom, DateTime today)
    {
        var r = Query("""
            SELECT MAX(dh.ngay_dat) AS cuoi, IFNULL(SUM(dh.ngay_dat >= @recentFrom AND dh.ngay_dat < @to), 0) AS gan
            FROM DON_HANG dh JOIN HOA_DON hd ON hd.ma_don_hang = dh.ma_don_hang
            """, new { recentFrom, to = today.AddDays(1) })[0];
        return (NT(r, "cuoi")?.Date, I(r, "gan"));
    }

    // Thống kê hiệu quả khuyến mãi trong kỳ: số lượt dùng và tiền giảm (LUOT_SU_DUNG_KHUYEN_MAI), doanh thu các hóa đơn liên quan (HOA_DON).
    public List<(string Name, int Uses, decimal Discount, decimal Revenue)> PromoStats(DateTime from, DateTime to) =>
        Query("""
            SELECT c.ten_ctkm, COUNT(*) AS luot, SUM(l.so_tien_giam) AS giam,
                   (SELECT IFNULL(SUM(hd.tong_thanh_toan), 0) FROM HOA_DON hd
                     WHERE hd.ma_don_hang IN (SELECT x.ma_don_hang FROM LUOT_SU_DUNG_KHUYEN_MAI x
                                              WHERE x.ma_ctkm = c.ma_ctkm AND x.ngay_su_dung >= @from AND x.ngay_su_dung < @to)) AS doanh_thu
            FROM LUOT_SU_DUNG_KHUYEN_MAI l JOIN CTKM c ON c.ma_ctkm = l.ma_ctkm
            WHERE l.ngay_su_dung >= @from AND l.ngay_su_dung < @to
            GROUP BY c.ma_ctkm, c.ten_ctkm
            ORDER BY luot DESC
            """, new { from, to = to.AddDays(1) })
            .Select(r => (S(r, "ten_ctkm"), I(r, "luot"), M(r, "giam"), M(r, "doanh_thu"))).ToList();

    // Kiểm tra đăng nhập quản trị: tìm TAI_KHOAN theo tên đăng nhập/email (JOIN VAI_TRO, NHAN_VIEN), so mật khẩu; chỉ nhận tài khoản nhân viên đang hoạt động và đang làm.
    public LoginResult CheckLogin(string username, string password)
    {
        var r = Query("""
            SELECT tk.ma_tai_khoan, tk.ten_dang_nhap, tk.mat_khau, tk.trang_thai, vt.ten_vai_tro,
                   nv.ma_nhan_vien, nv.ho_lot, nv.ten, nv.chuc_vu, nv.trang_thai AS nv_trang_thai
            FROM TAI_KHOAN tk
            JOIN VAI_TRO vt ON vt.ma_vai_tro = tk.ma_vai_tro
            LEFT JOIN NHAN_VIEN nv ON nv.ma_tai_khoan = tk.ma_tai_khoan
            WHERE tk.ten_dang_nhap = @u OR tk.email = @u
            LIMIT 1
            """, new { u = username.Trim() }).FirstOrDefault();

        if (r == null || !SportShop.CustomerWeb.Services.PasswordStorage.Verify(S(r, "mat_khau"), password))
            return LoginResult.Fail("Sai tên đăng nhập hoặc mật khẩu.");
        if (S(r, "trang_thai") != "hoat_dong")
            return LoginResult.Fail("Tài khoản đã bị khóa hoặc đã xóa.");
        if (NI(r, "ma_nhan_vien") == null)
            return LoginResult.Fail("Tài khoản khách hàng không được đăng nhập trang quản trị.");
        if (S(r, "nv_trang_thai") != "dang_lam")
            return LoginResult.Fail("Nhân viên đã nghỉ việc, không thể đăng nhập.");

        return new LoginResult(true, null, I(r, "ma_nhan_vien"), S(r, "ten_dang_nhap"),
            $"{S(r, "ho_lot")} {S(r, "ten")}", S(r, "ten"), S(r, "ten_vai_tro"), S(r, "chuc_vu"));
    }

    List<Category> LoadCategories() =>
        Query("SELECT ma_loai, ten_loai FROM LOAI_SP WHERE ten_loai <> @archived ORDER BY ma_loai", new { archived = Catalog.ArchivedCategory })
            .Select(r => new Category(I(r, "ma_loai"), S(r, "ten_loai"))).ToList();

    List<Product> LoadProducts() =>
        Query("""
            SELECT sp.ma_san_pham, l.ten_loai, sp.ten_san_pham, sp.don_vi_tinh, sp.trang_thai, sp.mo_ta,
                   (SELECT b2.hinh_anh FROM BIEN_THE_SP b2 WHERE b2.ma_san_pham = sp.ma_san_pham AND b2.hinh_anh IS NOT NULL
                    ORDER BY b2.trang_thai = 'an', b2.ma_bien_the LIMIT 1) AS hinh_anh
            FROM SAN_PHAM sp JOIN LOAI_SP l ON l.ma_loai = sp.ma_loai
            ORDER BY sp.ma_san_pham
            """).Select(r => new Product(I(r, "ma_san_pham"), S(r, "ten_loai"), S(r, "ten_san_pham"), S(r, "don_vi_tinh"),
                S(r, "trang_thai") == "hien", S(r, "mo_ta"), NS(r, "hinh_anh"))).ToList();

    List<Variant> LoadVariants() =>
        Query("SELECT ma_bien_the, ma_san_pham, kich_co, mau_sac, gia_ban, gia_von, so_luong_ton FROM BIEN_THE_SP ORDER BY ma_bien_the")
            .Select(r => new Variant(I(r, "ma_bien_the"), I(r, "ma_san_pham"), NS(r, "kich_co"), NS(r, "mau_sac"),
                M(r, "gia_ban"), M(r, "gia_von"), I(r, "so_luong_ton"))).ToList();

    List<Employee> LoadEmployees() =>
        Query("""
            SELECT nv.ma_nhan_vien, nv.ho_lot, nv.ten, nv.gioi_tinh, nv.ngay_sinh, nv.chuc_vu, nv.luong_co_ban, nv.trang_thai,
                   tk.ten_dang_nhap, tk.so_dien_thoai, tk.email, vt.ten_vai_tro
            FROM NHAN_VIEN nv
            JOIN TAI_KHOAN tk ON tk.ma_tai_khoan = nv.ma_tai_khoan
            JOIN VAI_TRO vt ON vt.ma_vai_tro = tk.ma_vai_tro
            ORDER BY nv.ma_nhan_vien
            """).Select(r => new Employee(I(r, "ma_nhan_vien"), S(r, "ho_lot"), S(r, "ten"), S(r, "gioi_tinh"), NT(r, "ngay_sinh"),
                S(r, "chuc_vu"), M(r, "luong_co_ban"), S(r, "trang_thai") == "dang_lam", S(r, "ten_dang_nhap"),
                S(r, "so_dien_thoai"), S(r, "email"), S(r, "ten_vai_tro"))).ToList();

    // Đọc khách hàng từ KHACH_HANG + TAI_KHOAN kèm sở thích, sổ địa chỉ và nhóm khách.
    List<Customer> LoadCustomers()
    {
        var interests = Query("""
            SELECT ks.ma_khach_hang, st.ten_so_thich FROM KHACH_HANG_SO_THICH ks
            JOIN SO_THICH st ON st.ma_so_thich = ks.ma_so_thich ORDER BY st.ten_so_thich
            """).ToLookup(r => I(r, "ma_khach_hang"), r => S(r, "ten_so_thich"));
        var addresses = Query("""
            SELECT ma_khach_hang, ten_nguoi_nhan, sdt_nguoi_nhan, dia_chi_chi_tiet, la_mac_dinh FROM SO_DIA_CHI
            ORDER BY la_mac_dinh DESC, ma_dia_chi
            """).ToLookup(r => I(r, "ma_khach_hang"),
                r => $"{S(r, "dia_chi_chi_tiet")} ({S(r, "ten_nguoi_nhan")} - {S(r, "sdt_nguoi_nhan")})" + (B(r, "la_mac_dinh") ? " (mặc định)" : ""));
        var groups = Query("""
            SELECT x.ma_khach_hang, n.ten_nhom FROM KHACH_HANG_NHOM_KH x
            JOIN NHOM_KHACH_HANG n ON n.ma_nhom_kh = x.ma_nhom_kh ORDER BY n.ma_nhom_kh
            """).ToLookup(r => I(r, "ma_khach_hang"), r => S(r, "ten_nhom"));

        return Query("""
            SELECT kh.*, tk.ten_dang_nhap, tk.mat_khau, tk.email, tk.so_dien_thoai AS tk_sdt, tk.trang_thai AS tk_trang_thai
            FROM KHACH_HANG kh LEFT JOIN TAI_KHOAN tk ON tk.ma_tai_khoan = kh.ma_tai_khoan
            ORDER BY kh.ma_khach_hang
            """).Select(r =>
        {
            int id = I(r, "ma_khach_hang");
            return new Customer
            {
                Id = id,
                LastName = S(r, "ho_lot"),
                FirstName = S(r, "ten"),
                Gender = S(r, "gioi_tinh"),
                Birth = NT(r, "ngay_sinh"),
                Phone = NS(r, "so_dien_thoai") ?? S(r, "tk_sdt"),
                Email = S(r, "email"),
                Username = S(r, "ten_dang_nhap"),
                Password = "Được bảo vệ",
                Locked = S(r, "tk_trang_thai") is "bi_khoa" or "da_xoa",
                Deleted = S(r, "da_xoa") is "1" or "True",
                Interests = interests[id].ToArray(),
                Addresses = addresses[id].ToArray(),
                Groups = groups[id].ToArray(),
                TotalSpent = M(r, "tong_chi_tieu"),
                Points = I(r, "diem_tich_luy"),
                Tier = MemberTier.Label(MemberTier.Effective(S(r, "hang_thanh_vien"), M(r, "tong_chi_tieu"))),
            };
        }).ToList();
    }

    List<CustomerGroup> LoadGroups() =>
        Query("""
            SELECT n.*, st.ten_so_thich,
                   (SELECT COUNT(*) FROM KHACH_HANG_NHOM_KH x JOIN KHACH_HANG k ON k.ma_khach_hang = x.ma_khach_hang AND k.da_xoa = 0 WHERE x.ma_nhom_kh = n.ma_nhom_kh) AS so_khach
            FROM NHOM_KHACH_HANG n LEFT JOIN SO_THICH st ON st.ma_so_thich = n.ma_so_thich
            ORDER BY n.ma_nhom_kh
            """).Select(r =>
        {
            decimal? from = NM(r, "gia_tri_tu"), to = NM(r, "gia_tri_den");
            string Range(Func<decimal, string> f, string suffix) =>
                from != null && to != null ? $"Từ {f(from.Value)} đến {f(to.Value)}{suffix}"
                : from != null ? $"Từ {f(from.Value)}{suffix} trở lên"
                : to != null ? $"Đến {f(to.Value)}{suffix}" : "-";
            var (criterion, value) = S(r, "loai_tieu_chi") switch
            {
                "tuoi" => ("Độ tuổi", Range(v => v.ToString("0"), " tuổi")),
                "gioi_tinh" => ("Giới tính", S(r, "gioi_tinh")),
                "so_thich" => ("Sở thích", S(r, "ten_so_thich")),
                "chi_tieu" => ("Mức chi tiêu", Range(Fmt.Money, "")),
                var t => (t, "-"),
            };
            return new CustomerGroup(I(r, "ma_nhom_kh"), S(r, "ten_nhom"), criterion, value, I(r, "so_khach"));
        }).ToList();

    // Đọc đơn hàng từ DON_HANG kèm hóa đơn, phương thức thanh toán, vận chuyển, khuyến mãi và các dòng chi tiết (giá vốn ưu tiên lấy từ CHI_TIET_HOA_DON).
    List<Order> LoadOrders()
    {
        var lines = Query("""
            SELECT ct.ma_don_hang, ct.ma_bien_the, ct.so_luong, ct.don_gia, ct.giam_gia,
                   COALESCE(cthd.gia_von, bt.gia_von) AS gia_von
            FROM CHI_TIET_DON_HANG ct
            JOIN BIEN_THE_SP bt ON bt.ma_bien_the = ct.ma_bien_the
            LEFT JOIN HOA_DON hd ON hd.ma_don_hang = ct.ma_don_hang
            LEFT JOIN CHI_TIET_HOA_DON cthd ON cthd.ma_hoa_don = hd.ma_hoa_don AND cthd.ma_bien_the = ct.ma_bien_the
            """).ToLookup(r => I(r, "ma_don_hang"),
                r => new OrderLine(I(r, "ma_bien_the"), I(r, "so_luong"), M(r, "don_gia"), M(r, "giam_gia"), M(r, "gia_von")));

        var promos = Query("""
            SELECT l.ma_don_hang, GROUP_CONCAT(DISTINCT c.ten_ctkm ORDER BY c.ma_ctkm SEPARATOR ', ') AS ten
            FROM LUOT_SU_DUNG_KHUYEN_MAI l JOIN CTKM c ON c.ma_ctkm = l.ma_ctkm
            GROUP BY l.ma_don_hang
            """).ToDictionary(r => I(r, "ma_don_hang"), r => S(r, "ten"));

        return Query("""
            SELECT dh.*, hd.so_hoa_don, pt.ten_phuong_thuc_thanh_toan AS pt_ten,
                   vc.ten_don_vi_van_chuyen, vc.ma_van_don,
                   COALESCE(hd.phi_van_chuyen, vc.phi_van_chuyen, 0) AS phi_ship,
                   km.ten_ctkm
            FROM DON_HANG dh
            LEFT JOIN HOA_DON hd ON hd.ma_don_hang = dh.ma_don_hang
            JOIN PHUONG_THUC_THANH_TOAN pt ON pt.ma_phuong_thuc_thanh_toan = dh.ma_phuong_thuc_thanh_toan
            LEFT JOIN VAN_CHUYEN vc ON vc.ma_don_hang = dh.ma_don_hang
            LEFT JOIN CTKM km ON km.ma_ctkm = dh.ma_ctkm
            ORDER BY dh.ngay_dat
            """).Select(r =>
        {
            int id = I(r, "ma_don_hang");
            int? emp = NI(r, "ma_nhan_vien");
            string? invoice = NS(r, "so_hoa_don");
            string carrier = NS(r, "ten_don_vi_van_chuyen") ?? (emp != null ? "Tại quầy" : "-");
            return new Order(
                id, invoice ?? $"DH{id:D5}", I(r, "ma_khach_hang"), emp, T(r, "ngay_dat"),
                S(r, "trang_thai_don_hang"), S(r, "trang_thai_thanh_toan"), S(r, "pt_ten"),
                carrier, S(r, "ma_van_don"), M(r, "phi_ship"), M(r, "tong_tien_giam_gia"),
                promos.GetValueOrDefault(id) ?? NS(r, "ten_ctkm"),
                M(r, "tong_thanh_toan"), invoice != null,
                S(r, "ten_nguoi_nhan"), S(r, "sdt_nguoi_nhan"), S(r, "dia_chi_giao_hang"), NS(r, "ly_do_huy"),
                lines[id].ToList());
        }).ToList();
    }

    List<CareNote> LoadCareNotes() =>
        Query("SELECT * FROM LICH_SU_CHAM_SOC ORDER BY ngay_cham_soc")
            .Select(r => new CareNote(I(r, "ma_khach_hang"), I(r, "ma_nhan_vien"), S(r, "hinh_thuc"), S(r, "noi_dung"),
                T(r, "ngay_cham_soc"), NT(r, "ngay_hen_lien_he_lai"))).ToList();

    List<FeedbackItem> LoadFeedback() =>
        Query("""
            SELECT ph.*, sp.ten_san_pham, hd.so_hoa_don,
                   tl.noi_dung AS tl_noi_dung, tl.ngay_tra_loi, tl.ma_nhan_vien AS tl_nhan_vien
            FROM PHAN_HOI ph
            LEFT JOIN SAN_PHAM sp ON sp.ma_san_pham = ph.ma_san_pham
            LEFT JOIN HOA_DON hd ON hd.ma_don_hang = ph.ma_don_hang
            LEFT JOIN TRA_LOI_PHAN_HOI tl ON tl.ma_tra_loi =
                 (SELECT MAX(t2.ma_tra_loi) FROM TRA_LOI_PHAN_HOI t2 WHERE t2.ma_phan_hoi = ph.ma_phan_hoi)
            ORDER BY ph.ngay_gui
            """).Select(r =>
        {
            int? order = NI(r, "ma_don_hang");
            string? target = NS(r, "ten_san_pham") ?? NS(r, "so_hoa_don") ?? (order != null ? $"DH{order:D5}" : null);
            return new FeedbackItem(I(r, "ma_phan_hoi"), I(r, "ma_khach_hang"), S(r, "loai"), NI(r, "so_sao"),
                S(r, "tieu_de"), S(r, "noi_dung"), T(r, "ngay_gui"), target, S(r, "trang_thai"), B(r, "an_hien"),
                NS(r, "tl_noi_dung"), NT(r, "ngay_tra_loi"), NI(r, "tl_nhan_vien"));
        }).ToList();

    List<Account> LoadAccounts() =>
        Query("""
            SELECT tk.ma_tai_khoan, tk.ten_dang_nhap, vt.ten_vai_tro, tk.email, tk.so_dien_thoai, tk.trang_thai
            FROM TAI_KHOAN tk JOIN VAI_TRO vt ON vt.ma_vai_tro = tk.ma_vai_tro
            ORDER BY tk.ma_vai_tro, tk.ma_tai_khoan
            """).Select(r => new Account(S(r, "ten_dang_nhap"), S(r, "ten_vai_tro"), S(r, "email"), S(r, "so_dien_thoai"), S(r, "trang_thai"), I(r, "ma_tai_khoan"))).ToList();

    List<Supplier> LoadSuppliers() =>
        Query("SELECT * FROM NHA_CUNG_CAP ORDER BY ma_ncc")
            .Select(r => new Supplier(I(r, "ma_ncc"), S(r, "ten_ncc"), S(r, "so_dien_thoai"), S(r, "email"), S(r, "dia_chi"),
                S(r, "trang_thai") == "hop_tac")).ToList();

    List<ImportSlip> LoadImports()
    {
        var items = Query("""
            SELECT c.ma_phieu_nhap, sp.ten_san_pham, bt.kich_co, bt.mau_sac, c.so_luong
            FROM CHI_TIET_PHIEU_NHAP_HANG c
            JOIN BIEN_THE_SP bt ON bt.ma_bien_the = c.ma_bien_the
            JOIN SAN_PHAM sp ON sp.ma_san_pham = bt.ma_san_pham
            ORDER BY c.ma_phieu_nhap, c.ma_bien_the
            """).ToLookup(r => I(r, "ma_phieu_nhap"), r =>
            {
                var extra = string.Join(" / ", new[] { NS(r, "kich_co"), NS(r, "mau_sac") }.Where(s => !string.IsNullOrEmpty(s)));
                return $"{S(r, "ten_san_pham")}{(extra.Length == 0 ? "" : $" ({extra})")} × {I(r, "so_luong")}";
            });

        return Query("""
            SELECT pn.ma_phieu_nhap, ncc.ten_ncc, pn.ngay_nhap, CONCAT(nv.ho_lot, ' ', nv.ten) AS nv_ten, pn.tong_tien,
                   (SELECT IFNULL(SUM(c.so_luong), 0) FROM CHI_TIET_PHIEU_NHAP_HANG c WHERE c.ma_phieu_nhap = pn.ma_phieu_nhap) AS so_luong
            FROM PHIEU_NHAP_HANG pn
            JOIN NHA_CUNG_CAP ncc ON ncc.ma_ncc = pn.ma_ncc
            JOIN NHAN_VIEN nv ON nv.ma_nhan_vien = pn.ma_nhan_vien
            ORDER BY pn.ngay_nhap DESC
            """).Select(r => new ImportSlip(I(r, "ma_phieu_nhap"), S(r, "ten_ncc"), T(r, "ngay_nhap"), I(r, "so_luong"),
                S(r, "nv_ten"), M(r, "tong_tien"), string.Join((char)10, items[I(r, "ma_phieu_nhap")]))).ToList();
    }

    List<Promotion> LoadPromotions()
    {
        var bySp = Query("""
            SELECT k.ma_ctkm, sp.ten_san_pham, k.phan_tram_giam FROM KM_THEO_SP k
            JOIN SAN_PHAM sp ON sp.ma_san_pham = k.ma_san_pham
            """).ToLookup(r => I(r, "ma_ctkm"), r => $"{S(r, "ten_san_pham")} giảm {Fmt.Pct(M(r, "phan_tram_giam"))}");
        var byDh = Query("SELECT ma_ctkm, muc_tien_don_hang, phan_tram_giam FROM KM_THEO_DH ORDER BY muc_tien_don_hang")
            .ToLookup(r => I(r, "ma_ctkm"), r => $"Từ {Fmt.Compact(M(r, "muc_tien_don_hang"))} giảm {Fmt.Pct(M(r, "phan_tram_giam"))}");
        var byVc = Query("SELECT ma_ctkm, ma_voucher, phan_tram_giam FROM KM_THEO_VOUCHER ORDER BY ma_voucher")
            .ToLookup(r => I(r, "ma_ctkm"), r => $"Mã {S(r, "ma_voucher")} giảm {Fmt.Pct(M(r, "phan_tram_giam"))}");

        return Query("SELECT * FROM CTKM ORDER BY ngay_bat_dau DESC").Select(r =>
        {
            int id = I(r, "ma_ctkm");
            var (type, parts) = S(r, "loai_khuyen_mai") switch
            {
                "theo_san_pham" => ("Theo sản phẩm", bySp[id]),
                "theo_don_hang" => ("Theo đơn hàng", byDh[id]),
                "theo_voucher" => ("Theo voucher", byVc[id]),
                var t => (t, Enumerable.Empty<string>()),
            };
            var desc = string.Join(", ", parts);
            if (NM(r, "gia_tri_giam_toi_da") is decimal max) desc += $" (tối đa {Fmt.Money(max)})";
            if (NS(r, "hang_ap_dung") is string tier) desc += $", hạng {tier}";
            return new Promotion(id, S(r, "ten_ctkm"), type, T(r, "ngay_bat_dau"), T(r, "ngay_ket_thuc"),
                desc.Length == 0 ? "-" : desc, S(r, "trang_thai"), I(r, "so_luot_da_dung"));
        }).ToList();
    }

    List<Campaign> LoadCampaigns() =>
        Query("""
            SELECT cd.ma_chien_dich, cd.tieu_de, cd.kenh_gui, cd.trang_thai, n.ten_nhom, km.ten_ctkm, CONCAT(nv.ho_lot, ' ', nv.ten) AS nv_ten
            FROM CHIEN_DICH_THONG_BAO cd
            LEFT JOIN NHOM_KHACH_HANG n ON n.ma_nhom_kh = cd.ma_nhom_kh
            LEFT JOIN CTKM km ON km.ma_ctkm = cd.ma_ctkm
            JOIN NHAN_VIEN nv ON nv.ma_nhan_vien = cd.ma_nhan_vien
            ORDER BY cd.ma_chien_dich DESC
            """).Select(r => new Campaign(S(r, "tieu_de"),
                S(r, "kenh_gui") switch { "email" => "Email", "sms" => "SMS", "app" => "App", var k => k },
                NS(r, "ten_nhom") ?? "Tất cả khách", NS(r, "ten_ctkm") ?? "-", S(r, "nv_ten"), S(r, "trang_thai") == "da_gui", I(r, "ma_chien_dich"))).ToList();

    List<Survey> LoadSurveys() =>
        Query("""
            SELECT ks.ma_khao_sat, ks.tieu_de, ks.ngay_ket_thuc, ks.trang_thai,
                   (SELECT COUNT(*) FROM CAU_HOI_KHAO_SAT c WHERE c.ma_khao_sat = ks.ma_khao_sat) AS so_cau,
                   (SELECT COUNT(*) FROM KHAO_SAT_KHACH_HANG k WHERE k.ma_khao_sat = ks.ma_khao_sat) AS da_gui,
                   (SELECT COUNT(*) FROM KHAO_SAT_KHACH_HANG k WHERE k.ma_khao_sat = ks.ma_khao_sat AND k.trang_thai = 'hoan_thanh') AS hoan_thanh
            FROM KHAO_SAT ks ORDER BY ks.ngay_tao DESC
            """).Select(r => new Survey(I(r, "ma_khao_sat"), S(r, "tieu_de"), I(r, "so_cau"), I(r, "da_gui"), I(r, "hoan_thanh"),
                T(r, "ngay_ket_thuc"), S(r, "trang_thai"))).ToList();

    // Thống kê một khảo sát: số khách nhận / hoàn thành (KHAO_SAT_KHACH_HANG), số lượt chọn từng lựa chọn và câu trả lời văn bản (TRA_LOI_KHAO_SAT).
    public SurveyStats? SurveyStat(int id)
    {
        var info = Query("SELECT ma_khao_sat, tieu_de, mo_ta, ngay_bat_dau, ngay_ket_thuc, trang_thai FROM KHAO_SAT WHERE ma_khao_sat=@id", new { id }).FirstOrDefault();
        if (info == null) return null;
        var counts = Query("SELECT COUNT(*) AS gui, SUM(trang_thai = 'hoan_thanh') AS xong FROM KHAO_SAT_KHACH_HANG WHERE ma_khao_sat=@id", new { id })[0];
        var questions = new List<QuestionStat>();
        foreach (var q in Query("SELECT ma_cau_hoi, noi_dung, loai_cau_hoi FROM CAU_HOI_KHAO_SAT WHERE ma_khao_sat=@id ORDER BY thu_tu, ma_cau_hoi", new { id }))
        {
            int qid = I(q, "ma_cau_hoi");
            var options = Query("""
                SELECT lc.noi_dung, (SELECT COUNT(*) FROM TRA_LOI_KHAO_SAT t WHERE t.ma_lua_chon = lc.ma_lua_chon) AS so_luot
                FROM LUA_CHON_CAU_HOI lc WHERE lc.ma_cau_hoi=@qid ORDER BY lc.ma_lua_chon
                """, new { qid }).Select(r => (S(r, "noi_dung"), I(r, "so_luot"))).ToList();
            var texts = Query("SELECT noi_dung_tra_loi FROM TRA_LOI_KHAO_SAT WHERE ma_cau_hoi=@qid AND noi_dung_tra_loi IS NOT NULL ORDER BY ma_tra_loi", new { qid })
                .Select(r => S(r, "noi_dung_tra_loi")).ToList();
            questions.Add(new QuestionStat(S(q, "noi_dung"), S(q, "loai_cau_hoi"), options, texts));
        }
        return new SurveyStats(I(info, "ma_khao_sat"), S(info, "tieu_de"), NS(info, "mo_ta"), T(info, "ngay_bat_dau"), T(info, "ngay_ket_thuc"), S(info, "trang_thai"),
            I(counts, "gui"), counts["xong"] is null or DBNull ? 0 : Convert.ToInt32(counts["xong"]), questions);
    }


    List<IDictionary<string, object?>> Query(string sql, object? args = null)
    {
        using var conn = new MySqlConnection(_cs);
        return conn.Query(sql, args).Cast<IDictionary<string, object?>>().ToList();
    }

    static bool IsNull(object? v) => v is null || v is DBNull;
    static int I(IDictionary<string, object?> r, string k) => Convert.ToInt32(r[k]);
    static int? NI(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? null : Convert.ToInt32(r[k]);
    static decimal M(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? 0 : Convert.ToDecimal(r[k]);
    static decimal? NM(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? null : Convert.ToDecimal(r[k]);
    static string S(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? "" : Convert.ToString(r[k])!;
    static string? NS(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? null : Convert.ToString(r[k]);
    static DateTime T(IDictionary<string, object?> r, string k) => Convert.ToDateTime(r[k]);
    static DateTime? NT(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? null : Convert.ToDateTime(r[k]);
    static bool B(IDictionary<string, object?> r, string k) => !IsNull(r[k]) && Convert.ToBoolean(r[k]);
}

public record LoginResult(bool Ok, string? Error, int EmployeeId, string Username, string FullName, string FirstName, string Role, string Position)
{
    public static LoginResult Fail(string error) => new(false, error, 0, "", "", "", "", "");
}
