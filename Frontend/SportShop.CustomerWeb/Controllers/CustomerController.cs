using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SportShop.CustomerWeb.Controllers;

/// <summary>Khu vực của khách hàng đã đăng nhập: chỉnh sửa thông tin, gửi phản hồi, làm khảo sát.</summary>
public class CustomerController : Controller
{
    /// <summary>Chưa đăng nhập thì chuyển về trang đăng nhập.</summary>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("customer")))
            context.Result = RedirectToAction("Login", "Account");
        base.OnActionExecuting(context);
    }

    public IActionResult Profile() { ViewData["Active"] = "profile"; return View(); }
    public IActionResult Feedback() { ViewData["Active"] = "feedback"; return View(); }
    public IActionResult Surveys() { ViewData["Active"] = "surveys"; return View(); }
}
