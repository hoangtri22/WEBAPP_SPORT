using System.Globalization;
using Dapper;
using MySqlConnector;
using SportShop.CustomerWeb.Models;

namespace SportShop.CustomerWeb.Services;

public class ShopStore(IConfiguration config, CartStore carts)
{
    readonly string _cs = config.GetConnectionString("Default") ?? "";
    MySqlConnection Open() => StoreDatabase.Connection(_cs);

    public const decimal FreeShipFrom = 500_000;
    public const decimal ShipFee = 30_000;


    // Danh mục sản phẩm (LOAI_SP) kèm số sản phẩm đang bán còn hàng; bỏ loại lưu trữ Catalog.ArchivedCategory.
    public List<ShopCategory> Categories()
    {
        using var c = Open();
        return c.Query("""
            SELECT l.ma_loai, l.ten_loai,
                   (SELECT COUNT(*) FROM SAN_PHAM s WHERE s.ma_loai = l.ma_loai AND s.trang_thai = 'hien'
                      AND EXISTS (SELECT 1 FROM BIEN_THE_SP b WHERE b.ma_san_pham = s.ma_san_pham AND b.trang_thai = 'hien' AND b.so_luong_ton > 0)) AS so_sp
            FROM LOAI_SP l WHERE l.ten_loai <> @archived ORDER BY l.ma_loai
            """, new { archived = Catalog.ArchivedCategory }).Cast<IDictionary<string, object?>>()
            .Select(r => new ShopCategory(I(r, "ma_loai"), S(r, "ten_loai"), I(r, "so_sp"))).ToList();
    }

    // Tìm kiếm / lọc sản phẩm: theo loại, từ khóa (LIKE tên sản phẩm hoặc tên loại) và kiểu sắp xếp; giá thấp nhất/cao nhất và tồn kho tổng hợp từ BIEN_THE_SP.
    public List<ShopProduct> Products(int? category = null, string? keyword = null, string sort = "new", int? take = null)
    {
        string order = sort switch
        {
            "price_asc" => "MIN(bt.gia_ban) IS NULL, MIN(bt.gia_ban) ASC",
            "price_desc" => "MAX(bt.gia_ban) DESC",
            "name" => "sp.ten_san_pham ASC",
            _ => "sp.ma_san_pham DESC",
        };
        using var c = Open();
        return c.Query($"""
            SELECT sp.ma_san_pham, sp.ten_san_pham, l.ten_loai, sp.don_vi_tinh, sp.mo_ta,
                   (SELECT b2.hinh_anh FROM BIEN_THE_SP b2 WHERE b2.ma_san_pham = sp.ma_san_pham AND b2.hinh_anh IS NOT NULL
                    ORDER BY b2.trang_thai = 'an', b2.ma_bien_the LIMIT 1) AS hinh_anh,
                   MIN(bt.gia_ban) AS gia_min, MAX(bt.gia_ban) AS gia_max, IFNULL(SUM(bt.so_luong_ton), 0) AS ton
            FROM SAN_PHAM sp
            JOIN LOAI_SP l ON l.ma_loai = sp.ma_loai
            LEFT JOIN BIEN_THE_SP bt ON bt.ma_san_pham = sp.ma_san_pham AND bt.trang_thai = 'hien'
            WHERE sp.trang_thai = 'hien'
              AND (@category IS NULL OR sp.ma_loai = @category)
              AND (@kw IS NULL OR sp.ten_san_pham LIKE @kw OR l.ten_loai LIKE @kw)
            GROUP BY sp.ma_san_pham, sp.ten_san_pham, l.ten_loai, sp.don_vi_tinh, sp.mo_ta
            ORDER BY {order}
            LIMIT @take
            """, new { category, kw = string.IsNullOrWhiteSpace(keyword) ? null : "%" + keyword.Trim() + "%", take = take ?? 1000 })
            .Cast<IDictionary<string, object?>>().Select(ToProduct).ToList();
    }

    // Chi tiết một sản phẩm đang hiện và các biến thể (size, màu, giá, tồn kho, ảnh) trong BIEN_THE_SP.
    public (ShopProduct Product, List<ShopVariant> Variants)? Product(int id)
    {
        using var c = Open();
        var r = c.Query("""
            SELECT sp.ma_san_pham, sp.ten_san_pham, l.ten_loai, sp.don_vi_tinh, sp.mo_ta,
                   (SELECT b2.hinh_anh FROM BIEN_THE_SP b2 WHERE b2.ma_san_pham = sp.ma_san_pham AND b2.hinh_anh IS NOT NULL
                    ORDER BY b2.trang_thai = 'an', b2.ma_bien_the LIMIT 1) AS hinh_anh,
                   MIN(bt.gia_ban) AS gia_min, MAX(bt.gia_ban) AS gia_max, IFNULL(SUM(bt.so_luong_ton), 0) AS ton
            FROM SAN_PHAM sp
            JOIN LOAI_SP l ON l.ma_loai = sp.ma_loai
            LEFT JOIN BIEN_THE_SP bt ON bt.ma_san_pham = sp.ma_san_pham AND bt.trang_thai = 'hien'
            WHERE sp.ma_san_pham = @id AND sp.trang_thai = 'hien'
            GROUP BY sp.ma_san_pham, sp.ten_san_pham, l.ten_loai, sp.don_vi_tinh, sp.mo_ta
            """, new { id }).Cast<IDictionary<string, object?>>().FirstOrDefault();
        if (r == null) return null;
        var variants = c.Query("SELECT ma_bien_the, kich_co, mau_sac, gia_ban, so_luong_ton, hinh_anh FROM BIEN_THE_SP WHERE ma_san_pham=@id AND trang_thai='hien' ORDER BY gia_ban, ma_bien_the", new { id })
            .Cast<IDictionary<string, object?>>()
            .Select(v => new ShopVariant(I(v, "ma_bien_the"), NS(v, "kich_co"), NS(v, "mau_sac"), D(v, "gia_ban"), I(v, "so_luong_ton"), NS(v, "hinh_anh"))).ToList();
        return (ToProduct(r), variants);
    }

    // Voucher đang hiệu lực và còn lượt dùng (KM_THEO_VOUCHER JOIN CTKM).
    public List<PromoInfo> Promos()
    {
        using var c = Open();
        return c.Query("""
            SELECT v.ma_voucher, ct.ten_ctkm, v.phan_tram_giam, ct.gia_tri_giam_toi_da, ct.hang_ap_dung, ct.ngay_ket_thuc
            FROM KM_THEO_VOUCHER v JOIN CTKM ct ON ct.ma_ctkm = v.ma_ctkm
            WHERE ct.trang_thai = 'hoat_dong' AND NOW() BETWEEN ct.ngay_bat_dau AND ct.ngay_ket_thuc
              AND (ct.so_luong_gioi_han IS NULL OR ct.so_luot_da_dung < ct.so_luong_gioi_han)
            ORDER BY v.phan_tram_giam DESC, v.ma_voucher
            """, new { archived = Catalog.ArchivedCategory }).Cast<IDictionary<string, object?>>()
            .Select(r => new PromoInfo(S(r, "ma_voucher"), S(r, "ten_ctkm"), D(r, "phan_tram_giam"), ND(r, "gia_tri_giam_toi_da"), NS(r, "hang_ap_dung"), T(r, "ngay_ket_thuc"))).ToList();
    }

    // Mã khách áp dụng được cho đơn hiện tại: lấy voucher đang hiệu lực từ Promos() rồi kiểm tra từng mã bằng ApplyVoucher() (hạng thành viên, lượt mua của mã khách mới, lượt dùng); trả về kèm số tiền giảm, sắp theo số tiền giảm nhiều nhất.
    public List<UsablePromo> UsablePromos(int customerId, decimal subtotal)
    {
        var list = new List<UsablePromo>();
        using var c = Open();
        foreach (var p in Promos())
        {
            try { list.Add(new UsablePromo(p, ApplyVoucher(c, null, p.Code, customerId, subtotal).Discount)); }
            catch (OrderError) { }
        }
        return list.OrderByDescending(x => x.Discount).ThenBy(x => x.Promo.Code).ToList();
    }


    static int CustomerOf(ISession s) => s.GetInt32("customer_id") ?? 0;

    public Dictionary<int, int> GetCart(ISession s) => CustomerOf(s) is > 0 and var id ? GetCart(id) : [];
    public Dictionary<int, int> GetCart(int customerId) => carts.Get(customerId);
    public void ClearCart(ISession s) { if (CustomerOf(s) > 0) ClearCart(CustomerOf(s)); }
    public void ClearCart(int customerId) => carts.Save(customerId, []);
    public int CartCount(ISession s) => GetCart(s).Values.Sum();

    public string? AddToCart(ISession session, int variantId, int qty) => AddToCart(CustomerOf(session), variantId, qty);

    // Both hosts use this operation, with a customer lock to prevent lost cart updates.
    public string? AddToCart(int customerId, int variantId, int qty)
    {
        if (customerId <= 0) return "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng.";
        if (qty is <= 0 or > 999 || variantId <= 0) return "Số lượng không hợp lệ.";
        return carts.Mutate<string?>(customerId, cart =>
        {
            using var c = Open();
            var stock = c.ExecuteScalar<int?>("""
                SELECT bt.so_luong_ton FROM BIEN_THE_SP bt JOIN SAN_PHAM sp ON sp.ma_san_pham=bt.ma_san_pham
                WHERE bt.ma_bien_the=@variantId AND bt.trang_thai='hien' AND sp.trang_thai='hien'
                """, new { variantId });
            if (stock is null) return "Sản phẩm này hiện không bán.";
            long want = (long)cart.GetValueOrDefault(variantId) + qty;
            if (want > 999) return "Mỗi biến thể được mua tối đa 999 sản phẩm.";
            if (want > stock) return stock == 0 ? "Sản phẩm đã hết hàng." : $"Chỉ còn {stock} sản phẩm trong kho.";
            cart[variantId] = (int)want;
            return null;
        });
    }

    public void SetQty(ISession session, int variantId, int qty)
    {
        if (CustomerOf(session) > 0) SetQty(CustomerOf(session), variantId, qty);
    }

    public string? SetQty(int customerId, int variantId, int qty) => carts.Mutate<string?>(customerId, cart =>
    {
        if (!cart.ContainsKey(variantId)) return "Không tìm thấy sản phẩm trong giỏ hàng.";
        if (qty <= 0) { cart.Remove(variantId); return null; }
        if (qty > 999) return "Số lượng không hợp lệ.";
        using var c = Open();
        var stock = c.ExecuteScalar<int?>("""
            SELECT bt.so_luong_ton FROM BIEN_THE_SP bt JOIN SAN_PHAM sp ON sp.ma_san_pham=bt.ma_san_pham
            WHERE bt.ma_bien_the=@variantId AND bt.trang_thai='hien' AND sp.trang_thai='hien'
            """, new { variantId });
        if (stock is null || stock < qty) return "Sản phẩm không còn bán hoặc không đủ tồn kho.";
        cart[variantId] = qty;
        return null;
    });

    public List<CartLine> CartLines(ISession session) => CustomerOf(session) > 0 ? CartLines(CustomerOf(session)) : [];

    public List<CartLine> CartLines(int customerId)
    {
        // Retain unavailable lines in storage until checkout rejects them or the user removes them.
        var cart = GetCart(customerId);
        if (cart.Count == 0) return [];
        var ids = cart.Keys.ToArray();
        using var c = Open();
        return c.Query("""
            SELECT bt.ma_bien_the, sp.ma_san_pham, sp.ten_san_pham, bt.kich_co, bt.mau_sac, bt.hinh_anh, bt.gia_ban,
                   CASE WHEN bt.trang_thai='hien' AND sp.trang_thai='hien' THEN bt.so_luong_ton ELSE 0 END AS stock
            FROM BIEN_THE_SP bt JOIN SAN_PHAM sp ON sp.ma_san_pham=bt.ma_san_pham WHERE bt.ma_bien_the IN @ids
            """, new { ids }).Cast<IDictionary<string, object?>>()
            .Select(r => new CartLine(I(r,"ma_bien_the"),I(r,"ma_san_pham"),S(r,"ten_san_pham"),NS(r,"kich_co"),NS(r,"mau_sac"),NS(r,"hinh_anh"),
                D(r,"gia_ban"),cart[I(r,"ma_bien_the")],I(r,"stock"))).OrderBy(l => l.Name).ToList();
    }

    // The additive migration provisions the existing four simulated web payment methods.
    public List<PaymentMethod> PaymentMethods()
    {
        using var c = Open();
        return c.Query<PaymentMethod>("""
            SELECT ma_phuong_thuc_thanh_toan AS Id,ten_phuong_thuc_thanh_toan AS Name
            FROM PHUONG_THUC_THANH_TOAN WHERE trang_thai='hoat_dong' ORDER BY ma_phuong_thuc_thanh_toan
            """).ToList();
    }

    // Người nhận mặc định khi thanh toán: lấy từ SO_DIA_CHI (ưu tiên địa chỉ mặc định), không có thì lấy họ tên và SĐT trong KHACH_HANG.
    public ReceiverInfo Receiver(int customerId)
    {
        using var c = Open();
        var a = c.Query("SELECT ten_nguoi_nhan, sdt_nguoi_nhan, dia_chi_chi_tiet FROM SO_DIA_CHI WHERE ma_khach_hang=@customerId ORDER BY la_mac_dinh DESC, ma_dia_chi LIMIT 1", new { customerId })
            .Cast<IDictionary<string, object?>>().FirstOrDefault();
        if (a != null) return new ReceiverInfo(S(a, "ten_nguoi_nhan"), S(a, "sdt_nguoi_nhan"), S(a, "dia_chi_chi_tiet"));
        var k = c.Query("SELECT CONCAT(ho_lot, ' ', ten) AS ten, so_dien_thoai FROM KHACH_HANG WHERE ma_khach_hang=@customerId", new { customerId })
            .Cast<IDictionary<string, object?>>().FirstOrDefault();
        return new ReceiverInfo(k == null ? "" : S(k, "ten"), k == null ? "" : S(k, "so_dien_thoai"), "");
    }

    // Kiểm tra voucher (KM_THEO_VOUCHER, CTKM): còn hạn, đúng hạng thành viên, mã khách mới chỉ dùng ở lần mua đầu (Catalog.IsNewCustomerPromo()), còn lượt và chưa vượt giới hạn mỗi khách (mã khách mới / theo hạng mặc định 1 lần); trả về mã CTKM và số tiền giảm.
    (int CtkmId, decimal Discount) ApplyVoucher(MySqlConnection c, MySqlTransaction? tx, string code, int customerId, decimal subtotal)
    {
        var km = c.Query("""
            SELECT k.ma_ctkm, ct.ten_ctkm, k.phan_tram_giam, ct.gia_tri_giam_toi_da, ct.hang_ap_dung, ct.so_luong_gioi_han, ct.so_luot_da_dung, ct.gioi_han_khach_hang
            FROM KM_THEO_VOUCHER k JOIN CTKM ct ON ct.ma_ctkm = k.ma_ctkm
            WHERE k.ma_voucher = @code AND ct.trang_thai = 'hoat_dong' AND NOW() BETWEEN ct.ngay_bat_dau AND ct.ngay_ket_thuc
            """ + (tx is null ? "" : " FOR UPDATE"), new { code }, tx).Cast<IDictionary<string, object?>>().FirstOrDefault()
            ?? throw new OrderError("Mã giảm giá không hợp lệ hoặc đã hết hạn.");
        var member = c.Query("SELECT hang_thanh_vien, tong_chi_tieu FROM KHACH_HANG WHERE ma_khach_hang=@customerId", new { customerId }, tx).Cast<IDictionary<string, object?>>().First();
        string tier = MemberTier.Effective(S(member, "hang_thanh_vien"), D(member, "tong_chi_tieu"));
        string? needTier = NS(km, "hang_ap_dung");
        if (needTier is { Length: > 0 } && needTier != tier) throw new OrderError($"Mã này chỉ áp dụng cho thành viên hạng {needTier}.");
        bool newCustomerOnly = Catalog.IsNewCustomerPromo(S(km, "ten_ctkm"));
        if (newCustomerOnly && c.ExecuteScalar<int>("SELECT COUNT(*) FROM DON_HANG WHERE ma_khach_hang=@customerId AND trang_thai_don_hang <> 'da_huy'", new { customerId }, tx) > 0)
            throw new OrderError("Mã dành cho khách mới chỉ dùng được ở lần mua hàng đầu tiên.");
        if (NI(km, "so_luong_gioi_han") is int lim && I(km, "so_luot_da_dung") >= lim) throw new OrderError("Mã giảm giá đã hết lượt sử dụng.");
        int ctId = I(km, "ma_ctkm");
        int? perLimit = NI(km, "gioi_han_khach_hang") ?? (newCustomerOnly || needTier is { Length: > 0 } ? 1 : null);
        if (perLimit is int per
            && c.ExecuteScalar<int>("SELECT COUNT(*) FROM LUOT_SU_DUNG_KHUYEN_MAI WHERE ma_ctkm=@ctId AND ma_khach_hang=@customerId", new { ctId, customerId }, tx) >= per)
            throw new OrderError("Bạn đã dùng hết số lần cho phép của mã này.");
        decimal discount = Math.Round(subtotal * D(km, "phan_tram_giam") / 100, 0);
        if (ND(km, "gia_tri_giam_toi_da") is decimal max) discount = Math.Min(discount, max);
        return (ctId, discount);
    }

    // Xem trước số tiền giảm ở trang thanh toán (gọi ApplyVoucher(), không ghi CSDL).
    public (decimal Discount, string? Error) PreviewVoucher(int customerId, string code, decimal subtotal)
    {
        try { using var c = Open(); return (ApplyVoucher(c, null, code.Trim(), customerId, subtotal).Discount, null); }
        catch (OrderError e) { return (0, e.Message); }
    }

    // Phí vận chuyển: miễn phí từ FreeShipFrom (500.000đ), còn lại ShipFee (30.000đ).
    public static decimal ShippingFor(decimal subtotal) => subtotal >= FreeShipFrom || subtotal == 0 ? 0 : ShipFee;

    // Atomic mobile checkout. Replay returns the original order without reading or clearing a newer cart.
    public (int? OrderId, string? Error, bool Replayed) PlaceCartOrder(int customerId, ReceiverInfo to, int payMethod, string? voucher, string requestKey)
    {
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 128 || requestKey.Any(ch => ch < 33 || ch > 126))
            return (null, "Idempotency-Key phải gồm 1–128 ký tự ASCII hiển thị.", false);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(new { Name = to.Name.Trim(), Phone = to.Phone.Trim(), Address = to.Address.Trim(), payMethod, Voucher = voucher?.Trim() ?? "" }))));
        using var c = Open();
        c.Open();
        using var tx = c.BeginTransaction();
        try
        {
            CartStore.LockCustomer(c, tx, customerId);
            var prior = c.QueryFirstOrDefault<OrderRequestRow>(
                "SELECT request_hash AS Hash,order_id AS Id FROM MOBILE_ORDER_REQUEST WHERE customer_id=@customerId AND request_key=@requestKey",
                new { customerId, requestKey }, tx);
            if (prior is { } old)
                return old.Hash == hash ? (old.Id, null, true) : (null, "Idempotency-Key đã được sử dụng với nội dung khác.", false);
            var cart = carts.ReadLocked(c, tx, customerId);
            var orderId = WriteOrder(c, tx, customerId, to, payMethod, voucher, cart);
            c.Execute("INSERT INTO MOBILE_ORDER_REQUEST(customer_id,request_key,request_hash,order_id) VALUES (@customerId,@requestKey,@hash,@orderId)",
                new { customerId, requestKey, hash, orderId }, tx);
            CartStore.WriteLocked(c, tx, customerId, []);
            tx.Commit();
            return (orderId, null, false);
        }
        catch (OrderError error) { return (null, error.Message, false); }
    }

    // Compatibility entry point for web callers supplying an explicit cart; shares the same transaction writer.
    public (int? OrderId, string? Error) PlaceOrder(int customerId, ReceiverInfo to, int payMethod, string? voucher, Dictionary<int, int> cart)
    {
        using var c = Open();
        c.Open();
        using var tx = c.BeginTransaction();
        try
        {
            CartStore.LockCustomer(c, tx, customerId);
            var order = WriteOrder(c, tx, customerId, to, payMethod, voucher, cart);
            tx.Commit();
            return (order, null);
        }
        catch (OrderError error) { return (null, error.Message); }
    }

    int WriteOrder(MySqlConnection c, MySqlTransaction tx, int customerId, ReceiverInfo to, int payMethod, string? voucher, Dictionary<int, int> cart)
    {
        if (cart.Count == 0) throw new OrderError("Giỏ hàng đang trống.");
        if (cart.Any(x => x.Key <= 0 || x.Value is <= 0 or > 999)) throw new OrderError("Số lượng không hợp lệ.");
        if (string.IsNullOrWhiteSpace(to.Name) || to.Name.Length > 100 || string.IsNullOrWhiteSpace(to.Address) || to.Address.Length > 255)
            throw new OrderError("Vui lòng nhập họ tên (tối đa 100 ký tự) và địa chỉ (tối đa 255 ký tự).");
        if (!System.Text.RegularExpressions.Regex.IsMatch(to.Phone.Trim(), @"^0\d{9}$"))
            throw new OrderError("Số điện thoại cần gồm 10 chữ số, bắt đầu bằng 0.");
        if (c.ExecuteScalar<int>("SELECT COUNT(*) FROM PHUONG_THUC_THANH_TOAN WHERE ma_phuong_thuc_thanh_toan=@payMethod AND trang_thai='hoat_dong'", new { payMethod }, tx) != 1)
            throw new OrderError("Phương thức thanh toán không hợp lệ hoặc đã ngừng hoạt động.");
        decimal subtotal = 0;
        var priced = new List<(int Variant,int Qty,decimal Price)>();
        foreach (var (variant, qty) in cart.OrderBy(k => k.Key))
        {
            var row = c.Query("""
                SELECT bt.gia_ban,bt.so_luong_ton,sp.ten_san_pham
                FROM BIEN_THE_SP bt JOIN SAN_PHAM sp ON sp.ma_san_pham=bt.ma_san_pham
                WHERE bt.ma_bien_the=@variant AND bt.trang_thai='hien' AND sp.trang_thai='hien' FOR UPDATE
                """, new { variant },tx).Cast<IDictionary<string, object?>>().FirstOrDefault()
                ?? throw new OrderError("Có sản phẩm trong giỏ không còn bán, vui lòng kiểm tra lại giỏ hàng.");
            if (qty > I(row,"so_luong_ton")) throw new OrderError($"{S(row,"ten_san_pham")} không đủ tồn kho.");
            decimal price = D(row,"gia_ban");
            priced.Add((variant,qty,price));
            subtotal += qty * price;
        }
        int? ctkm = null; decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(voucher))
        {
            var promotion = ApplyVoucher(c,tx,voucher.Trim(),customerId,subtotal);
            ctkm = promotion.CtkmId; discount = Math.Clamp(promotion.Discount,0,subtotal);
        }
        decimal ship = ShippingFor(subtotal), total = Math.Max(0,subtotal-discount)+ship;
        int order = c.ExecuteScalar<int>("""
            INSERT INTO DON_HANG(ma_khach_hang,ma_nhan_vien,ngay_dat,ten_nguoi_nhan,sdt_nguoi_nhan,dia_chi_giao_hang,ma_ctkm,
                tam_tinh,tong_tien_giam_gia,tong_thanh_toan,ma_phuong_thuc_thanh_toan,trang_thai_thanh_toan,trang_thai_don_hang)
            VALUES (@customerId,NULL,NOW(),@name,@phone,@address,@ctkm,@subtotal,@discount,@total,@payMethod,'chua_thanh_toan','cho_xac_nhan');
            SELECT LAST_INSERT_ID();
            """,new { customerId,name=to.Name.Trim(),phone=to.Phone.Trim(),address=to.Address.Trim(),ctkm,subtotal,discount,total,payMethod },tx);
        foreach(var (variant,qty,price) in priced)
        {
            c.Execute("UPDATE BIEN_THE_SP SET so_luong_ton=so_luong_ton-@qty WHERE ma_bien_the=@variant",new {qty,variant},tx);
            c.Execute("INSERT INTO CHI_TIET_DON_HANG(ma_don_hang,ma_bien_the,so_luong,don_gia,giam_gia,thanh_tien) VALUES (@order,@variant,@qty,@price,0,@amount)",
                new {order,variant,qty,price,amount=qty*price},tx);
        }
        c.Execute("INSERT INTO VAN_CHUYEN(ma_don_hang,ma_nhan_vien,ten_don_vi_van_chuyen,ma_van_don,phi_van_chuyen,trang_thai) VALUES (@order,NULL,'Giao hàng tiêu chuẩn',NULL,@ship,'cho_lay_hang')",new {order,ship},tx);
        if (ctkm is not null)
        {
            c.Execute("INSERT INTO LUOT_SU_DUNG_KHUYEN_MAI(ma_ctkm,ma_khach_hang,ma_don_hang,so_tien_giam,ngay_su_dung) VALUES (@ctkm,@customerId,@order,@discount,NOW())",new {ctkm,customerId,order,discount},tx);
            c.Execute("UPDATE CTKM SET so_luot_da_dung=so_luot_da_dung+1 WHERE ma_ctkm=@ctkm",new {ctkm},tx);
        }
        if(c.ExecuteScalar<int>("SELECT COUNT(*) FROM SO_DIA_CHI WHERE ma_khach_hang=@customerId",new {customerId},tx)==0)
            c.Execute("INSERT INTO SO_DIA_CHI(ma_khach_hang,ten_nguoi_nhan,sdt_nguoi_nhan,dia_chi_chi_tiet,la_mac_dinh) VALUES (@customerId,@name,@phone,@address,1)",
                new {customerId,name=to.Name.Trim(),phone=to.Phone.Trim(),address=to.Address.Trim()},tx);
        return order;
    }

    // Đơn hàng của khách (DON_HANG, PHUONG_THUC_THANH_TOAN, VAN_CHUYEN, HOA_DON) kèm các dòng CHI_TIET_DON_HANG; truyền orderId để lấy một đơn.
    public List<MyOrder> Orders(int customerId, int? orderId = null, int? page = null, int pageSize = 20, string? status = null)
    {
        using var c = Open();
        var orders = c.Query("""
            SELECT dh.ma_don_hang, dh.ngay_dat, dh.trang_thai_don_hang, dh.trang_thai_thanh_toan, p.ten_phuong_thuc_thanh_toan,
                   dh.ten_nguoi_nhan, dh.sdt_nguoi_nhan, dh.dia_chi_giao_hang, dh.ly_do_huy, vc.ten_don_vi_van_chuyen, vc.ma_van_don,
                   dh.tam_tinh, dh.tong_tien_giam_gia, IFNULL(vc.phi_van_chuyen, 0) AS phi_ship, dh.tong_thanh_toan, hd.so_hoa_don
            FROM DON_HANG dh
            JOIN PHUONG_THUC_THANH_TOAN p ON p.ma_phuong_thuc_thanh_toan = dh.ma_phuong_thuc_thanh_toan
            LEFT JOIN VAN_CHUYEN vc ON vc.ma_don_hang = dh.ma_don_hang
            LEFT JOIN HOA_DON hd ON hd.ma_don_hang = dh.ma_don_hang
            WHERE dh.ma_khach_hang = @customerId AND (@orderId IS NULL OR dh.ma_don_hang = @orderId)
              AND (@status IS NULL OR dh.trang_thai_don_hang=@status)
            ORDER BY dh.ma_don_hang DESC
            """ + (page.HasValue ? " LIMIT @pageSize OFFSET @offset" : ""),
            new { customerId, orderId, status, pageSize, offset = ((long)(page ?? 1)-1)*pageSize }).Cast<IDictionary<string, object?>>().ToList();
        if (orders.Count == 0) return [];

        var ids = orders.Select(o => I(o, "ma_don_hang")).ToArray();
        var lines = c.Query("""
            SELECT ct.ma_don_hang, sp.ma_san_pham, sp.ten_san_pham, bt.kich_co, bt.mau_sac, ct.so_luong, ct.don_gia, ct.thanh_tien, bt.hinh_anh
            FROM CHI_TIET_DON_HANG ct JOIN BIEN_THE_SP bt ON bt.ma_bien_the = ct.ma_bien_the JOIN SAN_PHAM sp ON sp.ma_san_pham = bt.ma_san_pham
            WHERE ct.ma_don_hang IN @ids ORDER BY sp.ten_san_pham
            """, new { ids }).Cast<IDictionary<string, object?>>()
            .ToLookup(r => I(r, "ma_don_hang"), r => new OrderLineView(S(r, "ten_san_pham"),
                string.Join(" / ", new[] { NS(r, "kich_co"), NS(r, "mau_sac") }.Where(s => !string.IsNullOrWhiteSpace(s))),
                I(r, "so_luong"), D(r, "don_gia"), D(r, "thanh_tien"), NS(r, "hinh_anh"), I(r, "ma_san_pham")));

        return orders.Select(o => new MyOrder
        {
            Id = I(o, "ma_don_hang"), Date = T(o, "ngay_dat"), Status = S(o, "trang_thai_don_hang"), PayStatus = S(o, "trang_thai_thanh_toan"),
            PayMethod = S(o, "ten_phuong_thuc_thanh_toan"), Receiver = S(o, "ten_nguoi_nhan"), Phone = S(o, "sdt_nguoi_nhan"), Address = S(o, "dia_chi_giao_hang"),
            CancelReason = NS(o, "ly_do_huy"), Carrier = NS(o, "ten_don_vi_van_chuyen"), Tracking = NS(o, "ma_van_don"),
            Subtotal = D(o, "tam_tinh"), Discount = D(o, "tong_tien_giam_gia"), Ship = D(o, "phi_ship"), Total = D(o, "tong_thanh_toan"), Invoice = NS(o, "so_hoa_don"),
            Lines = lines[I(o, "ma_don_hang")].ToList(),
        }).ToList();
    }

    public long OrderCount(int customerId, string? status = null)
    {
        using var c = Open();
        return c.ExecuteScalar<long>("SELECT COUNT(*) FROM DON_HANG WHERE ma_khach_hang=@customerId AND (@status IS NULL OR trang_thai_don_hang=@status)",new {customerId,status});
    }

    // Khách hủy đơn: chỉ khi đơn còn cho_xac_nhan; trong transaction cộng lại tồn kho BIEN_THE_SP, chuyển DON_HANG sang da_huy và VAN_CHUYEN sang that_bai.
    public string? CancelOrder(int customerId, int orderId)
    {
        using var c = Open();
        c.Open();
        using var tx = c.BeginTransaction();
        CartStore.LockCustomer(c, tx, customerId);
        var status = c.ExecuteScalar<string>("SELECT trang_thai_don_hang FROM DON_HANG WHERE ma_don_hang=@orderId AND ma_khach_hang=@customerId FOR UPDATE", new { orderId, customerId }, tx);
        if (status == null) return "Không tìm thấy đơn hàng.";
        if (status != "cho_xac_nhan") return "Đơn hàng đã được cửa hàng xử lý nên không thể tự hủy. Vui lòng liên hệ cửa hàng.";
        if (c.ExecuteScalar<string>("SELECT trang_thai_thanh_toan FROM DON_HANG WHERE ma_don_hang=@orderId", new { orderId }, tx) == "da_thanh_toan")
            return "Đơn hàng đã thanh toán. Vui lòng liên hệ cửa hàng để hủy và hoàn tiền.";
        c.Execute("UPDATE BIEN_THE_SP bt JOIN CHI_TIET_DON_HANG ct ON ct.ma_bien_the = bt.ma_bien_the SET bt.so_luong_ton = bt.so_luong_ton + ct.so_luong WHERE ct.ma_don_hang = @orderId", new { orderId }, tx);
        c.Execute("UPDATE DON_HANG SET trang_thai_don_hang='da_huy', ly_do_huy='Khách hàng tự hủy đơn' WHERE ma_don_hang=@orderId", new { orderId }, tx);
        c.Execute("UPDATE VAN_CHUYEN SET trang_thai='that_bai' WHERE ma_don_hang=@orderId", new { orderId }, tx);
        RefundVoucher(c, tx, orderId);
        tx.Commit();
        return null;
    }

    // Đơn bị hủy: hoàn lại voucher đã áp dụng (xóa lượt dùng của đơn, trả lại lượt dùng chung của chương trình) để khách dùng lại ở lần mua sau.
    static void RefundVoucher(MySqlConnection c, MySqlTransaction tx, int orderId)
    {
        c.Execute("""
            UPDATE CTKM ct JOIN (SELECT ma_ctkm, COUNT(*) AS n FROM LUOT_SU_DUNG_KHUYEN_MAI WHERE ma_don_hang = @orderId GROUP BY ma_ctkm) l ON l.ma_ctkm = ct.ma_ctkm
            SET ct.so_luot_da_dung = GREATEST(ct.so_luot_da_dung - l.n, 0)
            """, new { orderId }, tx);
        c.Execute("DELETE FROM LUOT_SU_DUNG_KHUYEN_MAI WHERE ma_don_hang = @orderId", new { orderId }, tx);
    }


    sealed class OrderRequestRow
    {
        public string Hash { get; set; } = "";
        public int Id { get; set; }
    }

    static ShopProduct ToProduct(IDictionary<string, object?> r) =>
        new(I(r, "ma_san_pham"), S(r, "ten_san_pham"), S(r, "ten_loai"), S(r, "don_vi_tinh"), NS(r, "mo_ta"), NS(r, "hinh_anh"), ND(r, "gia_min"), ND(r, "gia_max"), I(r, "ton"));

    static bool IsNull(object? v) => v is null or DBNull;
    static int I(IDictionary<string, object?> r, string k) => Convert.ToInt32(r[k], CultureInfo.InvariantCulture);
    static int? NI(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? null : Convert.ToInt32(r[k], CultureInfo.InvariantCulture);
    static decimal D(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? 0 : Convert.ToDecimal(r[k], CultureInfo.InvariantCulture);
    static decimal? ND(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? null : Convert.ToDecimal(r[k], CultureInfo.InvariantCulture);
    static string S(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? "" : Convert.ToString(r[k], CultureInfo.InvariantCulture)!;
    static string? NS(IDictionary<string, object?> r, string k) => IsNull(r[k]) ? null : Convert.ToString(r[k], CultureInfo.InvariantCulture);
    static DateTime T(IDictionary<string, object?> r, string k) => Convert.ToDateTime(r[k], CultureInfo.InvariantCulture);
}
