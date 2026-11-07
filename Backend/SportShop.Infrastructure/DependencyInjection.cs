using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SportShop.Application.Catalog;
using SportShop.Application.Health;

namespace SportShop.Infrastructure;

// Đăng ký dịch vụ của tầng Infrastructure.
public static class DependencyInjection
{
    // Đăng ký các dịch vụ đọc MySQL của API. Gọi từ Program.cs.
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
