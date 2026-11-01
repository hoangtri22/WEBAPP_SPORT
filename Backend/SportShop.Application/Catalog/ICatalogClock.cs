namespace SportShop.Application.Catalog;

public interface ICatalogClock
{
    DateTime LocalNow { get; }
    DateTimeOffset WithOffset(DateTime databaseTime);
}
