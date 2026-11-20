using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

// Kiểm tra lại tài khoản trong CSDL để phiên web không vượt qua khóa, xóa hoặc đổi vai trò.
public sealed class CustomerSessionFilter(DbStatus db, CustomerStore customers, GuestStore guests) : IActionFilter
{
    // Trước khi chạy action: khách không còn hoạt động thì xóa session, trang cần đăng nhập chuyển về Account/Login. Khách chưa đăng nhập thì chỉ giữ hồ sơ mua không tài khoản (guest_id) khi hồ sơ còn tồn tại và chưa gắn tài khoản.
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;
        if (!db.IsConnected) return;
        if (session.GetString("customer") is null)
        {
            if (session.GetInt32("guest_id") is int guest && !guests.IsActive(guest)) session.Remove("guest_id");
            return;
        }
        var id = session.GetInt32("customer_id");
        if (id.HasValue && customers.IsActiveCustomer(id.Value)) return;
        session.Clear();
        if (context.Controller is CustomerController or CartController)
            context.Result = new RedirectToActionResult("Login", "Account", null);
    }

    // Không cần xử lý sau action, khai báo vì interface IActionFilter bắt buộc.
    public void OnActionExecuted(ActionExecutedContext context) { }
}
