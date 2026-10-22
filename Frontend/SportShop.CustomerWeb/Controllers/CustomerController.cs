using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

/// <summary>
/// Khu vực của khách hàng đã đăng nhập: chỉnh sửa thông tin, gửi phản hồi, làm khảo sát.
/// Đã kết nối MySQL: đọc / ghi dữ liệu thật của khách đang đăng nhập.
/// Chưa kết nối: hiện dữ liệu mẫu (StoreData), các nút lưu / gửi chỉ hiện thông báo.
/// </summary>
public class CustomerController(DbStatus db, CustomerStore store, ShopStore shop, LoginThrottle throttle) : Controller
{
    int CustomerId => HttpContext.Session.GetInt32("customer_id") ?? 0;

    /// <summary>Chưa đăng nhập thì chuyển về trang đăng nhập.</summary>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        bool loggedIn = !string.IsNullOrEmpty(HttpContext.Session.GetString("customer"));
        // Đã có CSDL nhưng phiên đăng nhập cũ không có mã khách hàng (đăng nhập lúc chưa có CSDL) → đăng nhập lại
        if (loggedIn && db.IsConnected && CustomerId == 0)
        {
            HttpContext.Session.Remove("customer");
            loggedIn = false;
        }
        if (!loggedIn) context.Result = RedirectToAction("Login", "Account");
        base.OnActionExecuting(context);
    }

    // ===================== Chỉnh sửa thông tin =====================

    public IActionResult Profile() { ViewData["Active"] = "profile"; return View(); }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Profile(string fullName, string email, string phone, DateTime? birth, string gender, string[] interests)
    {
        if (!db.IsConnected) return RedirectToAction(nameof(Profile));
        fullName = (fullName ?? "").Trim(); email = (email ?? "").Trim(); phone = (phone ?? "").Trim();

        string? error =
            fullName.Length == 0 ? "Vui lòng nhập họ và tên." :
            !Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$") ? "Email chưa đúng định dạng." :
            !Regex.IsMatch(phone, @"^0\d{9}$") ? "Số điện thoại cần gồm 10 chữ số." :
            gender is not ("Nam" or "Nữ" or "Khác") ? "Giới tính không hợp lệ." :
            birth > DateTime.Today ? "Ngày sinh không hợp lệ." : null;
        error ??= store.UpdateProfile(CustomerId, fullName, email, phone, birth, gender, interests ?? []);

        TempData["Notice"] = error ?? "Đã lưu thông tin cá nhân.";
        return RedirectToAction(nameof(Profile));
    }

    // ===================== Đổi mật khẩu =====================

    /// <summary>
    /// Khách tự đổi mật khẩu: nhập mật khẩu hiện tại, mật khẩu mới và nhập lại. Nhập sai mật khẩu hiện tại nhiều lần thì bị khóa tạm
    /// (giống đăng nhập). Mật khẩu mới được lưu ngay vào tài khoản nên web quản trị thấy giá trị mới khi mở lại hồ sơ khách.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (!db.IsConnected) return RedirectToAction(nameof(Profile));
        currentPassword ??= ""; newPassword ??= "";
        string throttleKey = "pw:" + CustomerId;
        string? ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        int minutesLeft = throttle.MinutesLeft(throttleKey, ip);

        string? error =
            minutesLeft > 0 ? $"Bạn nhập sai mật khẩu hiện tại quá nhiều lần. Vui lòng thử lại sau {minutesLeft} phút." :
            currentPassword.Length == 0 ? "Vui lòng nhập mật khẩu hiện tại." :
            newPassword.Length < 6 ? "Mật khẩu mới cần có ít nhất 6 ký tự." :
            newPassword != confirmPassword ? "Mật khẩu nhập lại không khớp." :
            newPassword == currentPassword ? "Mật khẩu mới phải khác mật khẩu hiện tại." : null;

        if (error == null)
        {
            error = store.ChangePassword(CustomerId, currentPassword, newPassword);
            if (error != null) throttle.Fail(throttleKey, ip); else throttle.Reset(throttleKey, ip);
        }

        TempData["Notice"] = error ?? "Đã đổi mật khẩu thành công.";
        return RedirectToAction(nameof(Profile));
    }

    // ===================== Phản hồi về sản phẩm =====================

    public IActionResult Feedback() { ViewData["Active"] = "feedback"; return View(); }

    /// <summary>Gửi phản hồi sản phẩm. Có order (gửi từ trang "Đơn hàng của tôi") thì chỉ nhận sản phẩm nằm trong đơn đã giao đó.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Feedback(int product, int stars, string content, int? order = null)
    {
        if (!db.IsConnected) return RedirectToAction(nameof(Feedback));
        content = (content ?? "").Trim();
        string? error =
            !store.Products().Any(p => p.Id == product) ? "Vui lòng chọn sản phẩm." :
            !HasReceivedProduct(product, order) ? "Bạn chỉ gửi được phản hồi cho sản phẩm đã mua và đã nhận hàng." :
            stars is < 1 or > 5 ? "Vui lòng chọn số sao đánh giá." :
            content.Length == 0 ? "Vui lòng nhập nội dung phản hồi." :
            content.Length > 1000 ? "Nội dung phản hồi tối đa 1000 ký tự." : null;
        if (error == null) store.SendFeedback(CustomerId, product, stars, content);

        TempData["Notice"] = error ?? "Cảm ơn bạn! Phản hồi đã được gửi đến SPORTSHOP.";
        // Gửi từ trang đơn hàng thì quay lại trang đơn hàng, gửi từ trang phản hồi thì ở lại trang phản hồi
        return order.HasValue ? RedirectToAction(nameof(Orders)) : RedirectToAction(nameof(Feedback));
    }

    /// <summary>Khách đã nhận sản phẩm này chưa: trong đơn đã giao được chỉ định (nếu có), hoặc trong bất kỳ đơn đã giao nào.</summary>
    bool HasReceivedProduct(int product, int? order) => order is int orderId
        ? shop.Orders(CustomerId, orderId).Any(o => o.Status == "da_giao" && o.Lines.Any(l => l.ProductId == product))
        : store.PurchasedProducts(CustomerId).Any(p => p.Id == product);

    // ===================== Khảo sát =====================

    /// <summary>Danh sách khảo sát; có id thì mở khảo sát đó để làm; done=true hiện màn cảm ơn.</summary>
    public IActionResult Surveys(int? id, bool done = false)
    {
        ViewData["Active"] = "surveys";
        if (!db.IsConnected) return View(SampleSurveys(id));

        var items = store.Surveys(CustomerId);
        var current = id == null ? null : items.FirstOrDefault(s => s.Id == id && !s.Done && s.IsOpen);
        return View(new SurveyPageVm
        {
            Items = items,
            Current = current,
            Questions = current == null ? [] : store.Questions(current.Id),
            JustDone = done,
            Error = TempData["Notice"] as string,
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Surveys(int id)
    {
        if (!db.IsConnected) return RedirectToAction(nameof(Surveys));

        // Mỗi câu hỏi gửi lên dưới tên "q{mã câu hỏi}": mã lựa chọn (radio / checkbox) hoặc nội dung văn bản
        var answers = new Dictionary<int, (List<int>, string?)>();
        foreach (var q in store.Questions(id))
        {
            var values = Request.Form[$"q{q.Id}"].Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToList();
            if (q.Type == "text") answers[q.Id] = ([], values.FirstOrDefault());
            else answers[q.Id] = (values.Select(v => int.TryParse(v, out var n) ? n : 0).Where(n => n > 0).ToList(), null);
        }

        var error = store.SubmitSurvey(id, CustomerId, answers);
        if (error != null)
        {
            TempData["Notice"] = error;
            return RedirectToAction(nameof(Surveys), new { id });
        }
        return RedirectToAction(nameof(Surveys), new { id = (int?)null, done = true });
    }

    /// <summary>Dữ liệu mẫu cho chế độ xem giao diện (chưa có CSDL).</summary>
    static SurveyPageVm SampleSurveys(int? id)
    {
        var items = new List<SurveyItem>
        {
            new() { Id = 1, Title = "Khảo sát trải nghiệm mua sắm 2025", Description = "Chia sẻ cảm nhận về sản phẩm, dịch vụ và trải nghiệm của bạn tại SPORTSHOP.",
                    Deadline = new DateTime(2025, 5, 31), Status = "chua_lam", IsOpenFlag = 1, QuestionCount = StoreData.SurveyQuestions.Count },
            new() { Id = 2, Title = "Thói quen vận động của bạn", Description = "Cảm ơn bạn đã chia sẻ thói quen và mục tiêu luyện tập.",
                    Deadline = new DateTime(2025, 4, 30), Status = "hoan_thanh", CompletedAt = new DateTime(2025, 4, 15), QuestionCount = 3 },
        };
        int optId = 0, qId = 0;
        var questions = StoreData.SurveyQuestions.Select(q =>
            new SurveyQ(++qId, q.Title, q.Type, true, q.Options.Select(o => new SurveyOption(++optId, o)).ToList())).ToList();
        return new SurveyPageVm { Items = items, Current = id == 1 ? items[0] : null, Questions = id == 1 ? questions : [] };
    }

    // ===================== Đơn hàng của tôi =====================

    /// <summary>Danh sách đơn hàng của khách kèm trạng thái do cửa hàng cập nhật (chờ xác nhận → đang chuẩn bị → đang giao → đã giao).</summary>
    public IActionResult Orders()
    {
        ViewData["Active"] = "orders";
        if (!db.IsConnected) return View("~/Views/Shop/NoDb.cshtml");
        return View(shop.Orders(CustomerId));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult CancelOrder(int id)
    {
        var error = db.IsConnected ? shop.CancelOrder(CustomerId, id) : "Chưa kết nối cơ sở dữ liệu.";
        TempData["Notice"] = error ?? "Đã hủy đơn hàng.";
        return RedirectToAction(nameof(Orders));
    }

    /// <summary>Trạng thái hiện tại các đơn của khách (JSON), trang "Đơn hàng của tôi" hỏi định kỳ để biết đơn nào vừa được giao.</summary>
    [HttpGet]
    public IActionResult OrderStatuses()
    {
        if (!db.IsConnected) return Json(Array.Empty<object>());
        return Json(shop.Orders(CustomerId).Select(o => new { id = o.Id, code = o.Code, status = o.Status }));
    }
}
