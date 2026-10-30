using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class CrudController(ShopCommands cmd) : Controller
{
    Ctx Me() => new(int.TryParse(User.FindFirst("employee_id")?.Value, out var id) ? id : 0,
                    User.FindFirst("username")?.Value ?? "", User.FindFirst(ClaimTypes.Role)?.Value ?? "");

    // Kiểm tra entity có trong CrudDefs.All và yêu cầu quyền Quản lý nếu mục đó là AdminOnly.
    bool Allowed(string entity, out EntityDef def, out string error)
    {
        error = "";
        if (!CrudDefs.All.TryGetValue(entity ?? "", out def!)) { error = "Không hỗ trợ thao tác này."; return false; }
        if (def.AdminOnly && !Me().IsManager) { error = "Chỉ tài khoản Quản lý mới được thao tác với mục này."; return false; }
        return true;
    }

    // Trả về biểu mẫu thêm/sửa/xem (Views/Crud/_Form.cshtml) cho ngăn trượt; dữ liệu cũ đọc bằng ShopCommands.Load(). Được gọi từ openDrawer() trong wwwroot/js/admin.js.
    [HttpGet]
    public IActionResult Form(string entity, int id = 0, int parent = 0, string mode = "create")
    {
        if (!Allowed(entity, out var def, out var error)) return Content(error, "text/plain; charset=utf-8");
        if (id > 0 && mode == "create") mode = "edit";
        var values = id > 0 ? cmd.Load(entity, id) : [];
        if (id > 0 && entity is not ("customer" or "import" or "survey" or "survey_send") && values.Count == 0)
            return Content("Không tìm thấy dữ liệu.", "text/plain; charset=utf-8");
        if (entity == "order" && id > 0 && values.GetValueOrDefault("trang_thai_don_hang") is "da_giao" or "da_huy") mode = "view";
        var specs = def.Fields.Select(f => f.Type switch
        {
            "interests" => "lookup:sothich",
            "orderlines" => "lookup:bienthe",
            "importlines" => "lookup:bienthe_nhap",
            "variantlines" => "lookup:images",
            "promodetail" => "lookup:sanpham",
            _ => f.Options,
        }).Where(s => !string.IsNullOrEmpty(s)).Distinct();
        bool has(string type) => def.Fields.Any(f => f.Type == type);
        return PartialView("_Form", new FormModel(def, values, mode, id, parent,
            specs.ToDictionary(s => s!, s => cmd.Options(s)),
            id > 0 && has("interests") ? cmd.CustomerInterests(id) : [],
            id > 0 && has("importlines") ? cmd.ImportLines(id) : [],
            id > 0 && has("questions") ? cmd.SurveyQuestions(id) : [],
            id > 0 && has("questions") && cmd.SurveyHasAnswers(id)));
    }

    // Thêm/sửa dữ liệu: gọi ShopCommands.Save() theo entity, trả JSON {ok, message} cho admin.js.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save(string entity, int id = 0)
    {
        if (!Allowed(entity, out _, out var error)) return Json(new Result(false, error));
        return Json(cmd.Save(entity, id, new Form(Request.Form), Me()));
    }

    // Xóa dữ liệu: gọi ShopCommands.Delete() theo entity, trả JSON {ok, message} cho admin.js.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(string entity, int id)
    {
        if (!Allowed(entity, out _, out var error)) return Json(new Result(false, error));
        return Json(cmd.Delete(entity, id, Me()));
    }

    // Thao tác riêng (khóa khách, trả lời / ẩn phản hồi, đóng khảo sát): gọi ShopCommands.Act(), trả JSON cho admin.js.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Act(string action, int id)
        => Json(cmd.Act(action, id, new Form(Request.Form), Me()));
}

public record FormModel(EntityDef Def, Dictionary<string, string> Values, string Mode, int Id, int Parent,
    Dictionary<string, List<Opt>> OptionLists, HashSet<string> Interests, List<(string Name, int Qty, decimal Price, decimal Amount)> ImportLines,
    List<(string Text, string Type, bool Required, string Options)> Questions, bool QuestionsLocked)
{
    public string Val(string name) => Values.TryGetValue(name, out var v) ? v : "";
    public List<Opt> Options(string? spec) => spec != null && OptionLists.TryGetValue(spec, out var l) ? l : [];
    public bool ReadOnly => Mode == "view";
}
