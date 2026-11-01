using Microsoft.AspNetCore.Mvc;
using SportShop.Admin.Models;

namespace SportShop.Admin.Controllers;

public class ProductsController(ShopData db, ShopCommands cmd) : Controller
{
    // Xuất sản phẩm đang bán và biến thể ra Excel (cột A–I); cũng là file mẫu cho nút "Nhập Excel" (ImportExcel).
    public IActionResult ExportExcel()
    {
        using var book = new ClosedXML.Excel.XLWorkbook();
        var ws = book.Worksheets.Add("Sản phẩm");
        string[] heads = ["Tên sản phẩm", "Loại sản phẩm", "Đơn vị tính", "Mô tả", "Kích cỡ", "Màu sắc", "Giá bán", "Giá vốn", "Tồn kho"];
        for (int i = 0; i < heads.Length; i++) ws.Cell(1, i + 1).Value = heads[i];
        ws.Range(1, 1, 1, heads.Length).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromHtml("#d6ebe7"));
        var variants = db.ActiveVariants.ToLookup(v => v.ProductId);
        int r = 1;
        foreach (var p in db.ActiveProducts)
        {
            var list = variants[p.Id].ToList();
            foreach (var v in list.Count == 0 ? [null] : list.Select(x => (Variant?)x))
            {
                r++;
                ws.Cell(r, 1).Value = p.Name; ws.Cell(r, 2).Value = p.Category; ws.Cell(r, 3).Value = p.Unit; ws.Cell(r, 4).Value = p.Description;
                if (v == null) continue;
                ws.Cell(r, 5).Value = v.Size ?? ""; ws.Cell(r, 6).Value = v.Color ?? "";
                ws.Cell(r, 7).Value = v.Price; ws.Cell(r, 8).Value = v.Cost; ws.Cell(r, 9).Value = v.Stock;
            }
        }
        ws.Range(2, 7, Math.Max(r, 2), 8).Style.NumberFormat.Format = "#,##0";
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        book.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"san-pham-{DateTime.Today:yyyyMMdd}.xlsx");
    }

    // Nhập sản phẩm và biến thể từ file Excel (ShopCommands.ImportProducts); kết quả báo lại ở trang Quản lý sản phẩm.
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ImportExcel(IFormFile? file)
    {
        if (file == null || file.Length == 0 || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            TempData["ImportError"] = "Vui lòng chọn file Excel định dạng .xlsx.";
        else if (file.Length > 5 * 1024 * 1024)
            TempData["ImportError"] = "File Excel tối đa 5 MB.";
        else
        {
            using var stream = file.OpenReadStream();
            var result = cmd.ImportProducts(stream);
            TempData[result.Ok ? "ImportOk" : "ImportError"] = result.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // Quản lý loại sản phẩm: danh sách từ ShopData.CategoryList, số sản phẩm đếm từ ShopData.Products.
    public IActionResult Categories()
    {
        ViewBag.Menu = "categories";
        var rows = db.CategoryList.Select(c => new Row(
            [$"LSP{c.Id:D3}", c.Name, $"{db.Products.Count(p => p.Category == c.Name)} sản phẩm"], Url.Action(nameof(CategoryDetail), new { id = c.Id }), c.Id)).ToList();

        return View("~/Views/Shared/Table.cshtml", new TablePage
        {
            Title = "Quản lý loại sản phẩm",
            Subtitle = "Sắp xếp danh mục hàng hóa",
            Sections = [new() { Columns = ["Mã loại", "Tên loại sản phẩm", "Số sản phẩm"], Rows = rows, AddLabel = "Thêm mới", ShowActions = true, Entity = "category" }]
        });
    }

    // Quản lý sản phẩm: lấy loại, sản phẩm đang bán, biến thể theo sản phẩm và các dòng bảng biến thể từ ShopData, truyền sang Products/Index.cshtml qua ProductsVm.
    public IActionResult Index()
    {
        ViewBag.Menu = "products";
        var variantRows = db.ActiveVariants.Select(v => new Row(
            [$"BT{v.Id:D3}", db.Product(v.ProductId).Name, v.Size ?? "-", v.Color ?? "-", Fmt.Money(v.Price), Fmt.Money(v.Cost), v.Stock.ToString()], Url.Action("Detail", new { id = v.ProductId }), v.Id)).ToList();
        return View(new ProductsVm(db.Categories, db.ActiveProducts, db.Variants.GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.ToList()), variantRows));
    }

    // Chi tiết loại sản phẩm (Views/Products/CategoryDetail.cshtml): các sản phẩm thuộc loại lấy từ ShopData.Products và ShopData.Variants.
    public IActionResult CategoryDetail(int id)
    {
        ViewBag.Menu = "categories";
        var c = db.CategoryList.FirstOrDefault(x => x.Id == id);
        if (c == null) return NotFound();
        var products = db.Products.Where(p => p.Category == c.Name).ToList();
        var ids = products.Select(p => p.Id).ToHashSet();
        return View(new CategoryDetailVm(c, products, db.Variants.Where(v => ids.Contains(v.ProductId)).GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.ToList())));
    }

    // Chi tiết sản phẩm (Views/Products/Detail.cshtml): thông tin SAN_PHAM, các biến thể và số lượng đã bán từ ShopData (Products, Variants, Orders).
    public IActionResult Detail(int id)
    {
        ViewBag.Menu = "products";
        var p = db.Products.FirstOrDefault(x => x.Id == id);
        if (p == null) return NotFound();
        var vs = db.Variants.Where(v => v.ProductId == p.Id).ToList();
        var vids = vs.Select(v => v.Id).ToHashSet();
        return View(new ProductDetailVm(p, vs, db.Orders.Where(o => o.Invoiced).SelectMany(o => o.Lines).Where(l => vids.Contains(l.VariantId)).ToList()));
    }
}
