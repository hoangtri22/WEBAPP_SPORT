namespace SportShop.Contracts.Common;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items, int Page, int PageSize, long TotalItems)
{
    public long TotalPages => TotalItems == 0 ? 0 : (TotalItems - 1) / PageSize + 1;
}
