namespace SportShop.Mobile.ViewModels;

// Trang nguồn và VM được giữ trong NavigationStack; bản ghi này chỉ neo vị trí của đúng tập kết quả đó.
public sealed record ProductListReturnPoint(string SourceRoute, int ProductId, int ProductIndex,
    int FirstVisibleProductId, int FirstVisibleIndex, double ScrollX, double ScrollY,
    int? ProductTopPixels, int? FirstTopPixels, int LoadedCount, int ResultVersion,
    string SearchText, string AppliedFilters, string SortCode);

// Giữ vị trí cần quay về của danh sách sản phẩm.
public sealed class ProductListReturnState
{
    public ProductListReturnPoint? Saved { get; private set; }
    public bool IsPending { get; private set; }

    // Lưu vị trí trước khi rời danh sách.
    public void Capture(ProductListReturnPoint point)
    {
        Saved = point;
        IsPending = true;
    }

    // Yêu cầu khôi phục lại vị trí đã lưu.
    public void RequestRestore() => IsPending = Saved is not null;

    // Chỉ dùng neo một lần sau khi layout có dữ liệu; kết quả mới không nhận vị trí của bộ lọc cũ.
    public bool TryConsume(int resultVersion, bool layoutReady, out ProductListReturnPoint? point)
    {
        point = null;
        if (!IsPending || Saved is null) return false;
        if (Saved.ResultVersion != resultVersion) { IsPending = false; return false; }
        if (!layoutReady) return false;
        point = Saved;
        IsPending = false;
        return true;
    }
}
