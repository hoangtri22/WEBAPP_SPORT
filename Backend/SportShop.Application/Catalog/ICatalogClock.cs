namespace SportShop.Application.Catalog;

// Đồng hồ theo múi giờ CSDL, cài đặt ở DatabaseCatalogClock.
public interface ICatalogClock
{
    DateTime LocalNow { get; }
    DateTimeOffset WithOffset(DateTime databaseTime);
}
