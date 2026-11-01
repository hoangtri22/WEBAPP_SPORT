using Microsoft.Extensions.Configuration;
using SportShop.Application.Catalog;

namespace SportShop.Infrastructure;

public sealed class DatabaseCatalogClock : ICatalogClock
{
    private readonly TimeZoneInfo _databaseZone;
    private readonly TimeProvider _timeProvider;

    public DatabaseCatalogClock(IConfiguration configuration, TimeProvider timeProvider)
    {
        var zoneId = configuration["Database:TimeZoneId"] ?? "Asia/Ho_Chi_Minh";
        _databaseZone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        _timeProvider = timeProvider;
    }

    // Existing MySQL DATETIME values represent wall-clock time in the configured database zone.
    public DateTime LocalNow => TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _databaseZone).DateTime;

    public DateTimeOffset WithOffset(DateTime databaseTime)
    {
        var local = DateTime.SpecifyKind(databaseTime, DateTimeKind.Unspecified);
        if (_databaseZone.IsInvalidTime(local) || _databaseZone.IsAmbiguousTime(local))
            throw new InvalidOperationException("The catalog timestamp cannot be resolved in the configured database time zone.");
        return new DateTimeOffset(local, _databaseZone.GetUtcOffset(local));
    }
}
