namespace SportShop.Admin.Models;

public record Row(string[] Cells, string? Url = null, int Id = 0);

public class TableSection
{
    public string Heading { get; init; } = "";
    public string[] Columns { get; init; } = [];
    public List<Row> Rows { get; init; } = [];
    public string? AddLabel { get; init; }
    public bool ShowActions { get; init; }
    public bool Toolbar { get; init; } = true;
    public int PageSize { get; init; } = 8;

    public string? Entity { get; init; }
    public bool CanEdit { get; init; } = true;
    public bool CanDelete { get; init; } = true;
}

public class TablePage
{
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    public string? Note { get; init; }
    public List<TableSection> Sections { get; init; } = [];
}
