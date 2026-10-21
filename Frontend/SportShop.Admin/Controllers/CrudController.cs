using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

/// <summary>
/// Xử lý chung cho các nút Thêm / Xem / Sửa / Xóa trong web admin.
///  - Form: trả về nội dung biểu mẫu (HTML) để hiện trong ngăn trượt bên phải.
///  - Save / Delete / Act: ghi vào MySQL qua ShopCommands, trả về JSON { ok, message }.
/// Chỉ tài khoản đã đăng nhập mới gọi được (bộ lọc xác thực chung ở Program.cs).
/// </summary>
public class CrudController(ShopCommands cmd) : Controller
{
    // Người đang thao tác, lấy từ cookie đăng nhập
    Ctx Me() => new(int.TryParse(User.FindFirst("employee_id")?.Value, out var id) ? id : 0,
                    User.FindFirst("username")?.Value ?? "", User.FindFirst(ClaimTypes.Role)?.Value ?? "");

    /// <summary>Kiểm tra thực thể có tồn tại và người dùng có quyền (một số thực thể chỉ dành cho Quản lý).</summary>
    bool Allowed(string entity, out EntityDef def, out string error)
    {
        error = "";
        if (!CrudDefs.All.TryGetValue(entity ?? "", out def!)) { error = "Không hỗ trợ thao tác này."; return false; }
        if (def.AdminOnly && !Me().IsManager) { error = "Chỉ tài khoản Quản lý mới được thao tác với mục này."; return false; }
        return true;
    }

    // GET /Crud/Form?entity=customer&id=5&mode=edit  (mode: create | edit | view)
    [HttpGet]
    public IActionResult Form(string entity, int id = 0, int parent = 0, string mode = "create")
    {
        if (!Allowed(entity, out var def, out var error)) return Content(error, "text/plain; charset=utf-8");
        if (id > 0 && mode == "create") mode = "edit";
        var values = id > 0 ? cmd.Load(entity, id) : [];
        if (id > 0 && entity is not ("customer" or "import" or "survey" or "survey_send") && values.Count == 0)
            return Content("Không tìm thấy dữ liệu.", "text/plain; charset=utf-8");
        // Đơn đã giao / đã hủy chỉ xem, không sửa được
        if (entity == "order" && id > 0 && values.GetValueOrDefault("trang_thai_don_hang") is "da_giao" or "da_huy") mode = "view";
        return PartialView("_Form", new FormModel(def, values, mode, id, parent, cmd));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save(string entity, int id = 0)
    {
        if (!Allowed(entity, out _, out var error)) return Json(new Result(false, error));
        return Json(cmd.Save(entity, id, new Form(Request.Form), Me()));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(string entity, int id)
    {
        if (!Allowed(entity, out _, out var error)) return Json(new Result(false, error));
        return Json(cmd.Delete(entity, id, Me()));
    }

    // Thao tác nhanh: khóa khách hàng, trả lời / ẩn phản hồi, đóng khảo sát...
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Act(string action, int id)
        => Json(cmd.Act(action, id, new Form(Request.Form), Me()));
}

/// <summary>Dữ liệu truyền cho biểu mẫu: định nghĩa, giá trị hiện có (khi sửa / xem), chế độ.</summary>
public record FormModel(EntityDef Def, Dictionary<string, string> Values, string Mode, int Id, int Parent, ShopCommands Cmd)
{
    public string Val(string name) => Values.TryGetValue(name, out var v) ? v : "";
    public bool ReadOnly => Mode == "view";
}
