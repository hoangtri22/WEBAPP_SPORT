namespace SportShop.Admin.Models;

/// <summary>
/// Một dòng của bảng. Url (nếu có) là trang chi tiết mở bằng nút con mắt.
/// Id là mã dòng trong CSDL, dùng cho các nút xem / sửa / xóa (thực thể xác định ở TableSection.Entity).
/// </summary>
public record Row(string[] Cells, string? Url = null, int Id = 0);

/// <summary>Một "thẻ" bảng: thanh công cụ (tìm kiếm, lọc, thêm mới) + bảng + phân trang.</summary>
public class TableSection
{
    /// <summary>Tiêu đề thẻ. Để trống với bảng chính của trang (giống thiết kế).</summary>
    public string Heading { get; init; } = "";
    public string[] Columns { get; init; } = [];
    public List<Row> Rows { get; init; } = [];
    /// <summary>Chữ trên nút xanh bên phải, ví dụ "Thêm mới". Null = không hiện nút.</summary>
    public string? AddLabel { get; init; }
    /// <summary>Hiện cột "Thao tác" gồm 3 nút: xem, sửa, xóa.</summary>
    public bool ShowActions { get; init; }
    /// <summary>Hiện thanh công cụ (tìm kiếm + lọc) và phân trang.</summary>
    public bool Toolbar { get; init; } = true;
    public int PageSize { get; init; } = 8;

    /// <summary>Khóa thực thể trong CrudDefs ("customer", "employee"...): các nút thêm / xem / sửa / xóa sẽ làm việc với thực thể này.</summary>
    public string? Entity { get; init; }
    /// <summary>Cho phép nút sửa / nút xóa (tắt với dữ liệu chỉ xem như phiếu nhập hàng).</summary>
    public bool CanEdit { get; init; } = true;
    public bool CanDelete { get; init; } = true;
}

/// <summary>Trang danh sách dùng chung: tiêu đề + mô tả + một hoặc nhiều thẻ bảng.</summary>
public class TablePage
{
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    /// <summary>Ghi chú nghiệp vụ lấy từ PDF, hiện trong khung xanh nhạt phía trên.</summary>
    public string? Note { get; init; }
    public List<TableSection> Sections { get; init; } = [];
}
