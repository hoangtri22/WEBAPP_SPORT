using Microsoft.AspNetCore.Mvc;
using SportShop.CustomerWeb.Models;

namespace SportShop.CustomerWeb.Controllers;

public class InfoController : Controller
{
    public IActionResult Page(string id)
        => InfoPages.All.TryGetValue(id ?? "", out var page) ? View(page) : NotFound();
}
