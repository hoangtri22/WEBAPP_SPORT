using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

// Chuẩn bị dữ liệu chung cho Views/Shared/_Layout.cshtml trước khi hiển thị mọi trang: số phản hồi chưa trả lời (huy hiệu menu) và hồ sơ nhân viên đang đăng nhập (tên ở thanh trên), lấy từ ShopData. Đăng ký trong Program.cs.
public class LayoutDataFilter(ShopData db) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ViewResult view || context.Controller is not Controller c) return;
        bool bare = c.ViewBag.Bare == true;
        bool isAdminAccount = string.Equals(c.User.FindFirst("username")?.Value, "admin", StringComparison.OrdinalIgnoreCase);
        int employeeId = int.TryParse(c.User.FindFirst("employee_id")?.Value, out var eid) ? eid : 0;
        view.ViewData["Unanswered"] = bare ? 0 : db.FeedbackItems.Count(f => !f.Answered);
        view.ViewData["Self"] = bare || isAdminAccount ? null : db.Employees.FirstOrDefault(e => e.Id == employeeId);
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
