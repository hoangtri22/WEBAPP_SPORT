using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SportShop.Application.Catalog;
using SportShop.Application.Health;

namespace SportShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSportShopInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<MySqlConnectionFactory>();
        services.AddSingleton<ICatalogClock, DatabaseCatalogClock>();
        services.AddScoped<ICatalogReader, MySqlCatalogReader>();
        services.AddScoped<IDatabaseReadiness, DatabaseReadiness>();
        return services;
    }
}
