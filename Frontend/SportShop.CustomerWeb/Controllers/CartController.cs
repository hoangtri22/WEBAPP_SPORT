using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

public class CartController(DbStatus db, ShopStore shop) : Controller
{
    int CustomerId => HttpContext.Session.GetInt32("customer_id") ?? 0;
    bool LoggedIn => !string.IsNullOrEmpty(HttpContext.Session.GetString("customer")) && CustomerId > 0;

    // Giỏ hàng: lấy các dòng giỏ qua ShopStore.CartLines() (số lượng lưu ở App_Data/carts.json, giá và tồn kho đọc từ BIEN_THE_SP); chưa đăng nhập thì chuyển sang trang đăng nhập.
    public IActionResult Index()
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        return LoggedIn ? View(shop.CartLines(HttpContext.Session)) : LoginRequired("xem giỏ hàng");
    }

    // Thêm vào giỏ: gọi ShopStore.AddToCart() kiểm tra tồn kho trong BIEN_THE_SP và lưu giỏ qua CartStore; buyNow = true thì chuyển thẳng sang Checkout.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Add(int variantId, int qty = 1, bool buyNow = false, string? back = null)
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        if (!LoggedIn) return LoginRequired("thêm sản phẩm vào giỏ hàng");
        var error = shop.AddToCart(HttpContext.Session, variantId, qty);
        if (error != null) TempData["ShopError"] = error;
        else TempData["ShopNotice"] = "Đã thêm vào giỏ hàng.";
        if (error == null && buyNow) return RedirectToAction(nameof(Checkout));
        return Url.IsLocalUrl(back) ? Redirect(back) : RedirectToAction(nameof(Index));
    }

    // Sửa số lượng một dòng giỏ: giới hạn theo tồn kho rồi gọi ShopStore.SetQty().
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(int variantId, int qty)
    {
        if (!LoggedIn) return LoginRequired("sửa giỏ hàng");
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
        if (!LoggedIn) return LoginRequired("sửa giỏ hàng");
        shop.SetQty(HttpContext.Session, variantId, 0);
        return RedirectToAction(nameof(Index));
    }


    // Trang thanh toán: lấy giỏ hàng và người nhận mặc định qua ShopStore.Receiver() (SO_DIA_CHI/KHACH_HANG), dựng dữ liệu bằng BuildCheckout().
    public IActionResult Checkout()
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        if (!LoggedIn) return LoginRequired("đặt hàng");
        var lines = shop.CartLines(HttpContext.Session);
        if (lines.Count == 0) return RedirectToAction(nameof(Index));
        return View(BuildCheckout(lines, shop.Receiver(CustomerId), 0, null));
    }

    // Đặt hàng: nút "Áp dụng" (apply) chỉ tính lại mã giảm giá; ngược lại gọi ShopStore.PlaceOrder() ghi đơn vào MySQL, xóa giỏ rồi chuyển sang trang Done.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Checkout(string name, string phone, string address, int payMethod, string? voucher, string? apply)
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        if (!LoggedIn) return RedirectToAction("Login", "Account");
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
        var (orderId, error) = shop.PlaceOrder(CustomerId, to, payMethod, voucher, shop.GetCart(HttpContext.Session));
        if (orderId == null)
        {
            var vm = BuildCheckout(lines, to, payMethod, voucher);
            vm.Error = error;
            return View(vm);
        }
        shop.ClearCart(HttpContext.Session);
        return RedirectToAction(nameof(Done), new { id = orderId });
    }

    // Trang đặt hàng thành công: đọc lại đơn vừa đặt qua ShopStore.Orders(customerId, id).
    public IActionResult Done(int id)
    {
        if (!LoggedIn) return RedirectToAction("Login", "Account");
        var order = shop.Orders(CustomerId, id).FirstOrDefault();
        if (order == null) return NotFound();
        return View(order);
    }

    IActionResult LoginRequired(string action)
    {
        TempData["LoginNotice"] = $"Vui lòng đăng nhập để {action}. Nếu chưa có tài khoản, hãy đăng ký.";
        return RedirectToAction("Login", "Account");
    }

    // Tính tạm tính, giảm giá (ShopStore.PreviewVoucher()), phí ship (ShopStore.ShippingFor()) và lấy phương thức thanh toán (ShopStore.PaymentMethods()) cho view Checkout.cshtml.
    CheckoutVm BuildCheckout(List<CartLine> lines, ReceiverInfo to, int payMethod, string? voucher)
    {
        decimal subtotal = lines.Sum(l => l.Amount), discount = 0; string? voucherError = null;
        if (!string.IsNullOrWhiteSpace(voucher)) (discount, voucherError) = shop.PreviewVoucher(CustomerId, voucher, subtotal);
        return new CheckoutVm
        {
            Lines = lines, To = to, Methods = shop.PaymentMethods(), PayMethod = payMethod, Voucher = voucher,
            Subtotal = subtotal, Discount = discount, Ship = ShopStore.ShippingFor(subtotal), VoucherError = voucherError,
            Vouchers = shop.EligibleVouchers(CustomerId, subtotal),
        };
    }
}

public class CheckoutVm
{
    public List<CartLine> Lines { get; init; } = [];
    public ReceiverInfo To { get; init; } = new("", "", "");
    public List<PaymentMethod> Methods { get; init; } = [];
    public int PayMethod { get; init; }
    public string? Voucher { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Discount { get; init; }
    public decimal Ship { get; init; }
    public string? VoucherError { get; init; }
    public List<VoucherOption> Vouchers { get; init; } = [];
    public string? Error { get; set; }
    public decimal Total => Math.Max(0, Subtotal - Discount) + Ship;
}
