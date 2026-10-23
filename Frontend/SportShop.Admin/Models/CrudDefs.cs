namespace SportShop.Admin.Models;

// ============================================================
//  Định nghĩa các biểu mẫu thêm / sửa / xem trong web admin.
//  Mỗi thực thể (khách hàng, nhân viên, sản phẩm...) là một EntityDef gồm danh sách ô nhập (Field).
//  Biểu mẫu được dựng tự động từ định nghĩa này (Views/Crud/_Form.cshtml),
//  việc lưu vào MySQL nằm ở ShopCommands.
// ============================================================

/// <summary>
/// Một ô trong biểu mẫu.
/// Type: text, email, password, number, date, datetime, textarea, select, interests (nhiều ô tích sở thích),
///       orderlines / importlines / questions / promodetail (khối đặc biệt, xem _Form.cshtml).
/// Options: "a|Chữ A;b|Chữ B" (danh sách cố định) hoặc "lookup:tên" (lấy từ CSDL, xem ShopCommands.Options).
/// Show: "both" (cả thêm và sửa), "create" (chỉ khi thêm), "edit" (chỉ khi sửa).
/// ShowWhen: "tên_ô=giá_trị1|giá_trị2" - chỉ hiện ô khi ô kia đang có giá trị này (xử lý bằng JavaScript).
/// </summary>
public record Field(
    string Name, string Label, string Type = "text", bool Required = false,
    string? Options = null, string? Hint = null, string Show = "both",
    string? ShowWhen = null, string? Placeholder = null, bool Wide = false);

/// <summary>Một thực thể có biểu mẫu. AdminOnly = chỉ tài khoản vai trò "Quản lý" được thao tác.</summary>
public record EntityDef(string Key, string Title, Field[] Fields, bool AdminOnly = false);

public static class CrudDefs
{
    const string Status3 = "hoat_dong|Đang hoạt động;bi_khoa|Đã khóa;da_xoa|Đã xóa";
    const string Gender = "Nam|Nam;Nữ|Nữ;Khác|Khác";

    public static readonly Dictionary<string, EntityDef> All = new[]
    {
        // ---------- Tài khoản & phân quyền (TAI_KHOAN) ----------
        new EntityDef("account", "tài khoản",
        [
            new("ten_dang_nhap", "Tên đăng nhập", Required: true, Show: "create", Placeholder: "ví dụ: nguyenvana"),
            new("email", "Email", "email", true),
            new("so_dien_thoai", "Số điện thoại", Placeholder: "10 chữ số"),
            new("mat_khau", "Mật khẩu", "password", Hint: "Tối thiểu 6 ký tự. Khi sửa, để trống nếu không đổi mật khẩu."),
            new("ma_vai_tro", "Vai trò (phân quyền)", "select", true, "lookup:vaitro"),
            new("trang_thai", "Trạng thái", "select", false, Status3, "Khóa tài khoản: không đăng nhập được nhưng dữ liệu vẫn được giữ lại.", Show: "edit"),
        ], AdminOnly: true),

        // ---------- Khách hàng (KHACH_HANG) ----------
        new EntityDef("customer", "khách hàng",
        [
            new("ho_lot", "Họ lót", Required: true),
            new("ten", "Tên", Required: true),
            new("gioi_tinh", "Giới tính", "select", false, "|(Chưa chọn);" + Gender),
            new("ngay_sinh", "Ngày sinh", "date"),
            new("so_dien_thoai", "Số điện thoại", Placeholder: "10 chữ số"),
            new("email", "Email", "email", Show: "edit", Hint: "Chỉ lưu khi khách có tài khoản đăng nhập. Mật khẩu do khách tự đổi, admin không sửa."),
            new("dia_chi", "Địa chỉ giao hàng mặc định", Show: "edit", Wide: true),
            new("so_thich", "Sở thích thể thao", "interests", Wide: true),
            new("ten_dang_nhap", "Tên đăng nhập (không bắt buộc)", Show: "create", Hint: "Điền nếu muốn tạo luôn tài khoản đăng nhập cho khách."),
            new("email", "Email (nếu tạo tài khoản)", "email", Show: "create"),
            new("mat_khau", "Mật khẩu (nếu tạo tài khoản)", "password", Show: "create", Hint: "Tối thiểu 6 ký tự."),
        ]),

        // ---------- Nhóm khách hàng (NHOM_KHACH_HANG) ----------
        new EntityDef("group", "nhóm khách hàng",
        [
            new("ten_nhom", "Tên nhóm", Required: true),
            new("mo_ta", "Mô tả", Wide: true),
            new("loai_tieu_chi", "Tiêu chí phân nhóm", "select", true, "tuoi|Độ tuổi;gioi_tinh|Giới tính;so_thich|Sở thích;chi_tieu|Mức chi tiêu"),
            new("gia_tri_tu", "Từ", "number", ShowWhen: "loai_tieu_chi=tuoi|chi_tieu", Hint: "Tuổi hoặc số tiền (đ). Để trống nếu không giới hạn."),
            new("gia_tri_den", "Đến", "number", ShowWhen: "loai_tieu_chi=tuoi|chi_tieu"),
            new("gioi_tinh", "Giới tính", "select", false, Gender, ShowWhen: "loai_tieu_chi=gioi_tinh"),
            new("ma_so_thich", "Sở thích", "select", false, "lookup:sothich", ShowWhen: "loai_tieu_chi=so_thich"),
        ]),

        // ---------- Ghi chú chăm sóc khách hàng (LICH_SU_CHAM_SOC) ----------
        new EntityDef("care", "ghi chú chăm sóc",
        [
            new("hinh_thuc", "Hình thức", "select", true, "goi_dien|Gọi điện;email|Email;tin_nhan|Tin nhắn;gap_truc_tiep|Gặp trực tiếp"),
            new("noi_dung", "Nội dung", "textarea", true, Wide: true),
            new("ngay_hen_lien_he_lai", "Hẹn liên hệ lại", "date", Hint: "Không bắt buộc."),
        ]),

        // ---------- Nhân viên (NHAN_VIEN) ----------
        new EntityDef("employee", "nhân viên",
        [
            new("ho_lot", "Họ lót", Required: true),
            new("ten", "Tên", Required: true),
            new("gioi_tinh", "Giới tính", "select", false, "|(Chưa chọn);" + Gender),
            new("ngay_sinh", "Ngày sinh", "date"),
            new("chuc_vu", "Chức vụ", Placeholder: "ví dụ: Nhân viên bán hàng"),
            new("luong_co_ban", "Lương cơ bản (đ)", "number"),
            new("trang_thai", "Trạng thái", "select", false, "dang_lam|Đang làm việc;nghi_viec|Nghỉ việc", Show: "edit"),
            new("ten_dang_nhap", "Tên đăng nhập", Required: true, Show: "create"),
            new("email", "Email", "email", true, Show: "create"),
            new("so_dien_thoai", "Số điện thoại", Show: "create"),
            new("mat_khau", "Mật khẩu", "password", true, Show: "create", Hint: "Tối thiểu 6 ký tự."),
            new("ma_vai_tro", "Vai trò", "select", true, "lookup:vaitro_nv", Show: "create"),
        ], AdminOnly: true),

        // ---------- Loại sản phẩm (LOAI_SP) ----------
        new EntityDef("category", "loại sản phẩm", [new("ten_loai", "Tên loại sản phẩm", Required: true, Wide: true)]),

        // ---------- Sản phẩm (SAN_PHAM) ----------
        new EntityDef("product", "sản phẩm",
        [
            new("ma_loai", "Loại sản phẩm", "select", true, "lookup:loai"),
            new("ten_san_pham", "Tên sản phẩm", Required: true),
            new("don_vi_tinh", "Đơn vị tính", Required: true, Placeholder: "cái, đôi, bộ..."),
            new("trang_thai", "Hiển thị", "select", false, "hien|Đang bán;an|Đang ẩn"),
            new("mo_ta", "Mô tả", "textarea", Wide: true),
        ]),

        // ---------- Biến thể sản phẩm (BIEN_THE_SP) ----------
        new EntityDef("variant", "biến thể sản phẩm",
        [
            new("ma_san_pham", "Sản phẩm", "select", true, "lookup:sanpham", Wide: true),
            new("kich_co", "Kích cỡ", Placeholder: "S, M, 41..."),
            new("mau_sac", "Màu sắc"),
            new("gia_ban", "Giá bán (đ)", "number", true),
            new("gia_von", "Giá vốn (đ)", "number", Hint: "Giá vốn tự cập nhật theo đơn giá nhập gần nhất khi lập phiếu nhập."),
            new("so_luong_ton", "Số lượng tồn", "number", Hint: "Tồn kho tự tăng khi nhập hàng và giảm khi bán."),
            new("trang_thai", "Hiển thị", "select", false, "hien|Đang bán;an|Đang ẩn"),
            new("hinh_anh", "Hình ảnh", "select", false, "lookup:images", "Ảnh của biến thể (theo màu): chọn ảnh có sẵn hoặc tải ảnh mới từ thiết bị lên bên dưới (nếu chọn file thì dùng file). Ảnh biến thể đầu tiên làm ảnh đại diện của sản phẩm.", Wide: true),
        ]),

        // ---------- Nhà cung cấp (NHA_CUNG_CAP) ----------
        new EntityDef("supplier", "nhà cung cấp",
        [
            new("ten_ncc", "Tên nhà cung cấp", Required: true, Wide: true),
            new("so_dien_thoai", "Số điện thoại"),
            new("email", "Email", "email"),
            new("dia_chi", "Địa chỉ", Wide: true),
            new("trang_thai", "Trạng thái", "select", false, "hop_tac|Đang hợp tác;ngung_hop_tac|Ngừng hợp tác"),
        ]),

        // ---------- Phiếu nhập hàng (PHIEU_NHAP_HANG): chỉ thêm và xem ----------
        new EntityDef("import", "phiếu nhập hàng",
        [
            new("ma_ncc", "Nhà cung cấp", "select", true, "lookup:ncc"),
            new("ngay_nhap", "Ngày nhập", "datetime", true),
            new("dong", "Các dòng hàng nhập", "importlines", Wide: true),
        ]),

        // ---------- Đơn hàng / hóa đơn (DON_HANG): thêm mới tại quầy, đổi trạng thái ----------
        new EntityDef("order", "đơn hàng",
        [
            new("ma_khach_hang", "Khách hàng", "select", true, "lookup:khach", Show: "create", Wide: true),
            new("ten_nguoi_nhan", "Người nhận", Required: true, Show: "create"),
            new("sdt_nguoi_nhan", "SĐT người nhận", Required: true, Show: "create"),
            new("dia_chi_giao_hang", "Địa chỉ giao hàng", Required: true, Show: "create", Wide: true, Hint: "Mua tại quầy thì ghi \"Nhận tại cửa hàng\"."),
            new("dong", "Sản phẩm trong đơn", "orderlines", Wide: true, Show: "create"),
            new("ma_voucher", "Mã voucher", Show: "create", Hint: "Không bắt buộc. Hệ thống kiểm tra hạn, hạng thành viên, số lượt dùng."),
            new("phi_van_chuyen", "Phí vận chuyển (đ)", "number", Show: "create"),
            new("ma_phuong_thuc_thanh_toan", "Phương thức thanh toán", "select", true, "lookup:pttt", Show: "create"),
            new("trang_thai_don_hang", "Trạng thái đơn hàng", "select", false, "cho_xac_nhan|Chờ xác nhận;dang_chuan_bi|Đang chuẩn bị;dang_giao|Đang giao;da_giao|Đã giao;da_huy|Đã hủy"),
            new("ly_do_huy", "Lý do hủy", ShowWhen: "trang_thai_don_hang=da_huy", Wide: true, Hint: "Bắt buộc khi hủy. Hủy đơn sẽ hoàn lại tồn kho."),
            new("trang_thai_thanh_toan", "Thanh toán", "select", false, "chua_thanh_toan|Chưa thanh toán;da_thanh_toan|Đã thanh toán", "Chuyển sang \"Đã thanh toán\" sẽ lập hóa đơn và cộng điểm tích lũy cho khách."),
            new("ten_don_vi_van_chuyen", "Đơn vị vận chuyển", Hint: "Bỏ trống nếu nhận tại cửa hàng."),
            new("ma_van_don", "Mã vận đơn"),
        ]),

        // ---------- Khuyến mãi (CTKM) ----------
        new EntityDef("promotion", "chương trình khuyến mãi",
        [
            new("ten_ctkm", "Tên chương trình", Required: true, Wide: true),
            new("loai_khuyen_mai", "Loại khuyến mãi", "select", true, "theo_san_pham|Theo sản phẩm;theo_don_hang|Theo đơn hàng;theo_voucher|Theo voucher"),
            new("trang_thai", "Trạng thái", "select", false, "hoat_dong|Hoạt động;ngung|Ngừng;het_han|Hết hạn"),
            new("ngay_bat_dau", "Bắt đầu", "datetime", true),
            new("ngay_ket_thuc", "Kết thúc", "datetime", true),
            new("chi_tiet", "Mức giảm", "promodetail", Wide: true),
            new("gia_tri_giam_toi_da", "Giảm tối đa (đ)", "number", Hint: "Không bắt buộc."),
            new("hang_ap_dung", "Hạng áp dụng", "select", false, "|Mọi hạng;thuong|Thường;bac|Bạc;vang|Vàng;VIP|VIP"),
            new("so_luong_gioi_han", "Số lượt tối đa", "number", Hint: "Để trống = không giới hạn."),
            new("gioi_han_khach_hang", "Tối đa mỗi khách (lần)", "number"),
        ]),

        // ---------- Chiến dịch thông báo (CHIEN_DICH_THONG_BAO) ----------
        new EntityDef("campaign", "chiến dịch thông báo",
        [
            new("tieu_de", "Tiêu đề", Required: true, Wide: true),
            new("noi_dung", "Nội dung", "textarea", true, Wide: true),
            new("kenh_gui", "Kênh gửi", "select", true, "email|Email;sms|SMS;app|App"),
            new("ma_ctkm", "Khuyến mãi gắn kèm", "select", false, "lookup:ctkm"),
            new("ma_nhom_kh", "Gửi cho nhóm", "select", false, "lookup:nhom"),
            new("trang_thai", "Trạng thái", "select", false, "nhap|Nháp;da_gui|Đã gửi", "Chọn \"Đã gửi\" để ghi nhận chiến dịch đã gửi cho nhóm khách."),
        ]),

        // ---------- Khảo sát (KHAO_SAT + câu hỏi + lựa chọn) ----------
        new EntityDef("survey", "khảo sát",
        [
            new("tieu_de", "Tiêu đề khảo sát", Required: true, Wide: true),
            new("mo_ta", "Mô tả", "textarea", Wide: true),
            new("ngay_bat_dau", "Bắt đầu", "datetime", true),
            new("ngay_ket_thuc", "Kết thúc", "datetime", true),
            new("trang_thai", "Trạng thái", "select", false, "nhap|Nháp;da_gui|Đang thu thập;da_dong|Đã đóng"),
            new("cau_hoi", "Các câu hỏi", "questions", Wide: true),
        ]),

        // ---------- Gửi khảo sát đến khách hàng (KHAO_SAT_KHACH_HANG) ----------
        new EntityDef("survey_send", "gửi khảo sát",
        [
            new("ma_nhom_kh", "Gửi cho", "select", false, "lookup:nhom", "Chọn một nhóm khách hàng, hoặc để \"Tất cả khách\". Chỉ gửi cho khách có tài khoản đang hoạt động; khách đã nhận rồi sẽ không bị gửi lại.", Wide: true),
        ]),
    }.ToDictionary(e => e.Key);
}
