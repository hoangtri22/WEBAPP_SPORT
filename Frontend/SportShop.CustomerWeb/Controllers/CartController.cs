using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

// Giỏ hàng và thanh toán của web khách, xử lý qua ShopStore.
public class CartController(DbStatus db, ShopStore shop, GuestStore guests) : Controller
{
    int CustomerId => HttpContext.Session.GetInt32("customer_id") ?? 0;
    bool LoggedIn => !string.IsNullOrEmpty(HttpContext.Session.GetString("customer")) && CustomerId > 0;
    // Khách chưa đăng nhập mua hàng bằng số điện thoại: hồ sơ giỏ hàng tạm (GuestStore) lưu ở session; ShopperId là khách đăng nhập hoặc hồ sơ tạm này, 0 nếu chưa có giỏ.
    int GuestId => HttpContext.Session.GetInt32("guest_id") ?? 0;
    int ShopperId => LoggedIn ? CustomerId : GuestId;

    // Khách chưa đăng nhập thêm sản phẩm đầu tiên thì tạo hồ sơ giỏ tạm (GuestStore.CreatePlaceholder()) và nhớ trong session.
    void EnsureShopper()
    {
        if (ShopperId == 0) HttpContext.Session.SetInt32("guest_id", guests.CreatePlaceholder());
    }

    // Giỏ hàng: lấy các dòng giỏ qua ShopStore.CartLines() (số lượng lưu ở CUSTOMER_CART_ITEM qua CartStore, giá và tồn kho đọc từ BIEN_THE_SP), mức giảm đang đạt qua ShopStore.BestOrderPromo() và gợi ý mua thêm qua ShopStore.OrderUpsell(); khách chưa đăng nhập xem giỏ của hồ sơ tạm (không có thì giỏ trống).
    public IActionResult Index()
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        ViewData["Guest"] = !LoggedIn;
        var lines = shop.CartLines(HttpContext.Session);
        decimal subtotal = lines.Sum(l => l.Amount);
        return lines.Count == 0 ? View(new CartPageVm(lines, null, null))
            : View(new CartPageVm(lines, shop.BestOrderPromo(ShopperId, subtotal), shop.OrderUpsell(ShopperId, lines, subtotal)));
    }

    // Thêm vào giỏ: gọi ShopStore.AddToCart() kiểm tra tồn kho trong BIEN_THE_SP và lưu giỏ qua CartStore; buyNow = true thì chuyển thẳng sang Checkout.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Add(int variantId, int qty = 1, bool buyNow = false, string? back = null)
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        EnsureShopper();
        var error = shop.AddToCart(HttpContext.Session, variantId, qty);
        if (error != null) TempData["ShopError"] = error;
        else TempData["ShopNotice"] = "Đã thêm vào giỏ hàng.";
        if (error == null && buyNow) return RedirectToAction(nameof(Checkout));
        return Url.IsLocalUrl(back) ? Redirect(back) : RedirectToAction(nameof(Index));
    }

    // Thêm nhanh từ thẻ sản phẩm: chọn biến thể còn hàng có giá thấp nhất của sản phẩm rồi gọi ShopStore.AddToCart() với số lượng 1; quay lại đúng trang và vị trí thẻ vừa bấm (back).
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult QuickAdd(int productId, string? back = null)
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        EnsureShopper();
        var variant = shop.Product(productId)?.Variants.Where(v => v.Stock > 0).OrderBy(v => v.Price).ThenBy(v => v.Id).FirstOrDefault();
        var error = variant == null ? "Sản phẩm đã hết hàng." : shop.AddToCart(HttpContext.Session, variant.Id, 1);
        if (error != null) TempData["ShopError"] = error;
        else TempData["ShopNotice"] = "Đã thêm vào giỏ hàng.";
        return Url.IsLocalUrl(back) ? Redirect(back) : RedirectToAction(nameof(Index));
    }

    // Sửa số lượng một dòng giỏ: giới hạn theo tồn kho rồi gọi ShopStore.SetQty().
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(int variantId, int qty)
    {
        if (ShopperId == 0) return RedirectToAction(nameof(Index));
        var line = shop.CartLines(HttpContext.Session).FirstOrDefault(l => l.VariantId == variantId);
        if (line != null && qty > line.Stock)
        {
            TempData["ShopError"] = $"\"{line.Name}\" chỉ còn {line.Stock} sản phẩm trong kho.";
            qty = line.Stock;
        }
        shop.SetQty(HttpContext.Session, variantId, qty);
        return RedirectToAction(nameof(Index));
    }

    // Bỏ một sản phẩm khỏi giỏ: gọi ShopStore.SetQty() với số lượng 0.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Remove(int variantId)
    {
        if (ShopperId == 0) return RedirectToAction(nameof(Index));
        shop.SetQty(HttpContext.Session, variantId, 0);
        return RedirectToAction(nameof(Index));
    }


    // Trang thanh toán: lấy giỏ hàng và người nhận mặc định qua ShopStore.Receiver() (SO_DIA_CHI/KHACH_HANG), dựng dữ liệu bằng BuildCheckout().
    public IActionResult Checkout()
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        if (ShopperId == 0) return RedirectToAction(nameof(Index));
        var lines = shop.CartLines(HttpContext.Session);
        if (lines.Count == 0) return RedirectToAction(nameof(Index));
        var receiver = shop.Receiver(ShopperId);
        // Khách chưa để lại số điện thoại thì để trống thông tin nhận hàng (không điền tên hồ sơ tạm).
        if (!LoggedIn && string.IsNullOrEmpty(receiver.Phone)) receiver = new ReceiverInfo("", "", "");
        return View(BuildCheckout(lines, receiver, 0, null));
    }

    // Đặt hàng: nút "Áp dụng" (apply) chỉ tính lại mã giảm giá; ngược lại gọi ShopStore.PlaceOrder() ghi đơn vào MySQL, xóa giỏ rồi chuyển sang trang Done.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Checkout(string name, string phone, string address, int payMethod, string? voucher, string? apply)
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        if (ShopperId == 0) return RedirectToAction(nameof(Index));
        var lines = shop.CartLines(HttpContext.Session);
        if (lines.Count == 0) return RedirectToAction(nameof(Index));
        var to = new ReceiverInfo(name ?? "", phone ?? "", address ?? "");

        if (apply != null)
        {
            var vm = BuildCheckout(lines, to, payMethod, voucher);
            return View(vm);
        }

        if (lines.Any(l => !l.Enough))
        {
            TempData["ShopError"] = "Có sản phẩm vượt quá tồn kho, vui lòng chỉnh lại số lượng trong giỏ hàng.";
            return RedirectToAction(nameof(Index));
        }
        int? orderId; string? error;
        if (LoggedIn) (orderId, error, _) = shop.PlaceCartOrder(CustomerId, to, payMethod, voucher, Guid.NewGuid().ToString("N"), orderPromo: true);
        else (orderId, error) = PlaceAsGuest(to, payMethod);
        if (orderId == null)
        {
            var vm = BuildCheckout(lines, to, payMethod, voucher);
            vm.Error = error;
            return View(vm);
        }
        return RedirectToAction(nameof(Done), new { id = orderId });
    }

    // Trang đặt hàng thành công: đọc lại đơn vừa đặt qua ShopStore.Orders(customerId, id).
    public IActionResult Done(int id)
    {
        if (ShopperId == 0) return RedirectToAction(nameof(Index));
        var order = shop.Orders(ShopperId, id).FirstOrDefault();
        if (order == null) return NotFound();
        ViewData["Guest"] = !LoggedIn;
        return View(order);
    }

    // Đặt hàng của khách không tài khoản: kiểm tra họ tên, số điện thoại, địa chỉ; số đã thuộc tài khoản thì yêu cầu đăng nhập; chọn hồ sơ theo số điện thoại (GuestStore.Resolve(), dùng lại hồ sơ cũ của số đó nếu có) rồi ghi đơn qua ShopStore.PlaceGuestOrder(); xong thì hồ sơ theo số điện thoại thành hồ sơ đang dùng của session và hồ sơ tạm thừa được xóa.
    (int? OrderId, string? Error) PlaceAsGuest(ReceiverInfo to, int payMethod)
    {
        string phone = to.Phone.Trim();
        if (string.IsNullOrWhiteSpace(to.Name) || to.Name.Length > 100 || string.IsNullOrWhiteSpace(to.Address) || to.Address.Length > 255)
            return (null, "Vui lòng nhập họ tên (tối đa 100 ký tự) và địa chỉ (tối đa 255 ký tự).");
        if (!Regex.IsMatch(phone, @"^0\d{9}$")) return (null, "Số điện thoại cần gồm 10 chữ số, bắt đầu bằng 0.");
        if (guests.PhoneHasAccount(phone)) return (null, "Số điện thoại này đã đăng ký tài khoản. Vui lòng đăng nhập để đặt hàng.");
        int owner = guests.Resolve(GuestId, to.Name, phone);
        var (orderId, error) = shop.PlaceGuestOrder(GuestId, owner, to, payMethod);
        if (orderId == null) return (null, error);
        int previous = GuestId;
        HttpContext.Session.SetInt32("guest_id", owner);
        if (previous != owner) guests.DiscardPlaceholder(previous);
        return (orderId, null);
    }

    // Lưu thông báo vào TempData["LoginNotice"] rồi chuyển sang Account/Login.
    IActionResult LoginRequired(string action)
    {
        TempData["LoginNotice"] = $"Vui lòng đăng nhập để {action}. Nếu chưa có tài khoản, hãy đăng ký.";
        return RedirectToAction("Login", "Account");
    }

    // Tính tạm tính, giảm giá (ShopStore.PreviewVoucher(); không chọn mã thì dùng ShopStore.BestOrderPromo()), danh sách mã dùng được (ShopStore.UsablePromos()), phí ship (ShopStore.ShippingFor()) và lấy phương thức thanh toán (ShopStore.PaymentMethods()) cho view Checkout.cshtml.
    CheckoutVm BuildCheckout(List<CartLine> lines, ReceiverInfo to, int payMethod, string? voucher)
    {
        decimal subtotal = lines.Sum(l => l.Amount), discount = 0; string? voucherError = null;
        AppliedOrderPromo? orderPromo = null;
        if (LoggedIn && !string.IsNullOrWhiteSpace(voucher)) (discount, voucherError) = shop.PreviewVoucher(CustomerId, voucher, subtotal);
        else if ((orderPromo = shop.BestOrderPromo(ShopperId, subtotal)) != null) discount = orderPromo.Discount;
        return new CheckoutVm
        {
            Lines = lines, To = to, Methods = shop.PaymentMethods(), PayMethod = payMethod, Voucher = voucher,
            Subtotal = subtotal, Discount = discount, Ship = ShopStore.ShippingFor(subtotal), VoucherError = voucherError, OrderPromo = orderPromo,
            Promos = LoggedIn ? shop.UsablePromos(CustomerId, subtotal) : [], IsGuest = !LoggedIn,
        };
    }
}

// Dữ liệu trang thanh toán (Views/Cart/Checkout.cshtml).
public class CheckoutVm
{
    public List<CartLine> Lines { get; init; } = [];
    public ReceiverInfo To { get; init; } = new("", "", "");
    public List<PaymentMethod> Methods { get; init; } = [];
    public int PayMethod { get; init; }
    public string? Voucher { get; init; }
    public bool IsGuest { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Discount { get; init; }
    public decimal Ship { get; init; }
    public string? VoucherError { get; init; }
    public AppliedOrderPromo? OrderPromo { get; init; }
    public List<UsablePromo> Promos { get; init; } = [];
    public string? Error { get; set; }
    public decimal Total => Math.Max(0, Subtotal - Discount) + Ship;
}
