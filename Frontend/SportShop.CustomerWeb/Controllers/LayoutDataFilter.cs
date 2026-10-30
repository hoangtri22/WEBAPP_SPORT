using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

// Chuẩn bị dữ liệu chung cho các layout và header / footer trước khi hiển thị mọi trang: đã kết nối CSDL chưa (DbStatus), khách đang đăng nhập (CustomerStore.Current(), chưa kết nối thì dùng StoreData mẫu), số món trong giỏ (ShopStore.CartCount()) và địa chỉ web quản trị (Links:AdminUrl). Đăng ký trong Program.cs.
public class LayoutDataFilter(DbStatus db, CustomerStore store, ShopStore shop, IConfiguration config) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ViewResult view) return;
        var session = context.HttpContext.Session;
        bool live = db.IsConnected;
        view.ViewData["DbConnected"] = live;
        view.ViewData["Me"] = (live ? store.Current(session) : null) ?? StoreData.Customer;
        view.ViewData["CartCount"] = live ? shop.CartCount(session) : 0;
        view.ViewData["AdminUrl"] = config["Links:AdminUrl"] ?? "#";
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
