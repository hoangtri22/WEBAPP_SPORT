using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

/// <summary>
/// Giỏ hàng và đặt hàng. Giỏ lưu trong session; thanh toán cần đăng nhập.
/// Thanh toán được giả lập: đơn tạo ra ở trạng thái "Chờ xác nhận / Chưa thanh toán",
/// admin duyệt và đổi trạng thái; đơn "Đã giao" thì hóa đơn và doanh thu được ghi nhận.
/// </summary>
public class CartController(DbStatus db, ShopStore shop) : Controller
{
    int CustomerId => HttpContext.Session.GetInt32("customer_id") ?? 0;
    bool LoggedIn => !string.IsNullOrEmpty(HttpContext.Session.GetString("customer")) && CustomerId > 0;

    public IActionResult Index()
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        return LoggedIn ? View(shop.CartLines(HttpContext.Session)) : LoginRequired("xem giỏ hàng");
    }

    // Thêm vào giỏ (từ trang chi tiết sản phẩm). buyNow = true: thêm xong chuyển thẳng sang thanh toán.
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

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Remove(int variantId)
    {
        if (!LoggedIn) return LoginRequired("sửa giỏ hàng");
        shop.SetQty(HttpContext.Session, variantId, 0);
        return RedirectToAction(nameof(Index));
    }

    // ----- Thanh toán -----

    public IActionResult Checkout()
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        if (!LoggedIn) return LoginRequired("đặt hàng");
        var lines = shop.CartLines(HttpContext.Session);
        if (lines.Count == 0) return RedirectToAction(nameof(Index));
        return View(BuildCheckout(lines, shop.Receiver(CustomerId), 0, null));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Checkout(string name, string phone, string address, int payMethod, string? voucher, string? apply)
    {
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        if (!LoggedIn) return RedirectToAction("Login", "Account");
        var lines = shop.CartLines(HttpContext.Session);
        if (lines.Count == 0) return RedirectToAction(nameof(Index));
        var to = new ReceiverInfo(name ?? "", phone ?? "", address ?? "");

        // Nút "Áp dụng" chỉ tính lại mức giảm, chưa đặt hàng
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

    /// <summary>Màn hình đặt hàng thành công.</summary>
    public IActionResult Done(int id)
    {
        if (!LoggedIn) return RedirectToAction("Login", "Account");
        var order = shop.Orders(CustomerId, id).FirstOrDefault();
        if (order == null) return NotFound();
        return View(order);
    }

    /// <summary>Chưa đăng nhập: chuyển sang trang đăng nhập kèm lời nhắc (chưa có tài khoản thì đăng ký).</summary>
    IActionResult LoginRequired(string action)
    {
        TempData["LoginNotice"] = $"Vui lòng đăng nhập để {action}. Nếu chưa có tài khoản, hãy đăng ký.";
        return RedirectToAction("Login", "Account");
    }

    CheckoutVm BuildCheckout(List<CartLine> lines, ReceiverInfo to, int payMethod, string? voucher)
    {
        decimal subtotal = lines.Sum(l => l.Amount), discount = 0; string? voucherError = null;
        if (!string.IsNullOrWhiteSpace(voucher)) (discount, voucherError) = shop.PreviewVoucher(CustomerId, voucher, subtotal);
        return new CheckoutVm
        {
            Lines = lines, To = to, Methods = shop.PaymentMethods(), PayMethod = payMethod, Voucher = voucher,
            Subtotal = subtotal, Discount = discount, Ship = ShopStore.ShippingFor(subtotal), VoucherError = voucherError,
        };
    }
}

/// <summary>Dữ liệu trang thanh toán.</summary>
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
    public string? Error { get; set; }
    public decimal Total => Math.Max(0, Subtotal - Discount) + Ship;
}
