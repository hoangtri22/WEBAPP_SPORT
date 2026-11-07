namespace SportShop.Admin.Models;

// Một dòng của bảng dữ liệu.
public record Row(string[] Cells, string? Url = null, int Id = 0, bool? Locked = null);

// Một bảng dữ liệu trong trang, vẽ bằng Views/Shared/_Section.cshtml.
public class TableSection
{
    public string Heading { get; init; } = "";
    public string[] Columns { get; init; } = [];
    public List<Row> Rows { get; init; } = [];
    public string? AddLabel { get; init; }
    public string? ExportUrl { get; init; }
    public bool ShowActions { get; init; }
    public bool Toolbar { get; init; } = true;
    public int PageSize { get; init; } = 15;

    public string? Entity { get; init; }
    public bool CanEdit { get; init; } = true;
    public bool CanDelete { get; init; } = true;
    public string? LockAction { get; init; }
    public string? RestoreAction { get; init; }
    public string RestoreLabel { get; init; } = "Khôi phục";
}

// Dữ liệu trang danh sách dùng chung Views/Shared/Table.cshtml.
public class TablePage
{
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    public string? Note { get; init; }
    public string? DetailHint { get; init; }
    public List<TableSection> Sections { get; init; } = [];
}
