namespace SportShop.Admin.Models;

// Một dòng của bảng dữ liệu.
public record Row(string[] Cells, string? Url = null, int Id = 0, bool? Locked = null, Dictionary<string, string>? Data = null);

// Một ô lọc của "Tìm kiếm nâng cao". Kind: select (ô chọn, so bằng) | number (từ - đến, dùng >= và <=) | date (từ ngày - đến ngày) | text (chứa). Giá trị lấy từ cột bảng (Col) hoặc từ data-f-<Data> của dòng; Options trống thì lấy các giá trị khác nhau của cột.
public record FilterField(string Key, string Label, string Kind, int Col = -1, string? Data = null, string[]? Options = null);

// Nút "Lọc nhanh": bật sẵn một điều kiện. Op: eq | gte | lte | contains.
public record QuickFilter(string Label, string Key, string Op, string Value);

// Bộ lọc nâng cao của một bảng: Id để lưu bộ lọc, Noun là tên đối tượng trong dòng "Tìm thấy N ...".
public class FilterSpec
{
    public string Id { get; init; } = "";
    public string Noun { get; init; } = "";
    public List<FilterField> Fields { get; init; } = [];
    public List<QuickFilter> Quick { get; init; } = [];
}

// Một bảng dữ liệu trong trang, vẽ bằng Views/Shared/_Section.cshtml.
public class TableSection
{
    public string Heading { get; init; } = "";
    public string[] Columns { get; init; } = [];
    public List<Row> Rows { get; init; } = [];
    public string? AddLabel { get; init; }
    public string? AddId { get; init; }
    public string? ExportUrl { get; init; }
    public bool ShowActions { get; init; }
    public bool Toolbar { get; init; } = true;
    public int PageSize { get; init; } = 15;
    public FilterSpec? Filter { get; init; }

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
