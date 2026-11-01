using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SportShop.CustomerWeb.Services;

namespace SportShop.CustomerWeb.Controllers;

// Recheck persisted account state so a web session cannot bypass a lock/deletion/role change.
public sealed class CustomerSessionFilter(DbStatus db, CustomerStore customers) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;
        if (session.GetString("customer") is null || !db.IsConnected) return;
        var id = session.GetInt32("customer_id");
        if (id.HasValue && customers.IsActiveCustomer(id.Value)) return;
        session.Clear();
        if (context.Controller is CustomerController or CartController)
            context.Result = new RedirectToActionResult("Login", "Account", null);
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
