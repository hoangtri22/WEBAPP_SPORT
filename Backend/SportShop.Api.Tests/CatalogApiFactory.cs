using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SportShop.Application.Catalog;
using SportShop.Application.Health;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;

namespace SportShop.Api.Tests;

internal sealed class CatalogApiFactory(
    bool replaceDatabaseServices = true,
    string environment = "Development",
    string mediaBaseUrl = "http://localhost:5080") : WebApplicationFactory<SportShop.Api.Controllers.ProductsController>
{
    public FakeCatalogReader Reader { get; } = new();
    public FakeDatabaseReadiness Readiness { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "",
                ["Media:PublicBaseUrl"] = mediaBaseUrl,
                ["Database:TimeZoneId"] = "Asia/Ho_Chi_Minh"
            }));
        if (replaceDatabaseServices)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICatalogReader>();
                services.RemoveAll<IDatabaseReadiness>();
                services.AddSingleton<ICatalogReader>(Reader);
                services.AddSingleton<IDatabaseReadiness>(Readiness);
            });
        }
    }

    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri(environment == "Production" ? "https://localhost:5081" : "http://localhost:5080")
    });
}

internal sealed class FakeCatalogReader : ICatalogReader
{
    public Exception? Failure { get; set; }
    public ProductSearch? LastSearch { get; private set; }
    public int ProductListCalls { get; private set; }
    public string? ImagePath { get; set; } = "/images/giay-pegasus.jpg";
    public string? VariantImagePath { get; set; } = "/uploads/unavailable-test-fixture-731a9820.jpg";

    CatalogProduct Product => new(7, "Giày chạy bộ", 2, "Giày", "đôi", "Mô tả sản phẩm", ImagePath, 1200000m, 1300000m, 4);

    void ThrowIfFailed()
    {
        if (Failure is not null) throw Failure;
    }

    public Task<IReadOnlyList<CategoryDto>> CategoriesAsync(CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        return Task.FromResult<IReadOnlyList<CategoryDto>>([new(2, "Giày", 1)]);
    }

    public Task<PagedResponse<CatalogProduct>> ProductsAsync(ProductSearch search, CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        ProductListCalls++;
        LastSearch = search;
        IReadOnlyList<CatalogProduct> products = search.Page == 1 ? [Product] : [];
        return Task.FromResult(new PagedResponse<CatalogProduct>(products, search.Page, search.PageSize, 23));
    }

    public Task<CatalogProductDetail?> ProductAsync(int productId, CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        return Task.FromResult(productId == 7
            ? new CatalogProductDetail(Product,
                [new(12, "42", "Xanh", 1200000m, 4, VariantImagePath), new(13, "43", "Đen", 1300000m, 0, null)])
            : null);
    }

    public Task<IReadOnlyList<CatalogPromotion>> PromotionsAsync(CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        return Task.FromResult<IReadOnlyList<CatalogPromotion>>(
            [new(3, "SPORT5", "Ưu đãi thể thao", 5m, 50000m, null, 1,
                new DateTime(2026, 10, 1), new DateTime(2026, 10, 31, 23, 59, 59))]);
    }

    public Task<IReadOnlyList<InterestDto>> InterestsAsync(CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        return Task.FromResult<IReadOnlyList<InterestDto>>([new(1, "Chạy bộ")]);
    }

    public Task<IReadOnlyList<PaymentMethodDto>> PaymentMethodsAsync(CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        return Task.FromResult<IReadOnlyList<PaymentMethodDto>>([new(1, "cod", "Thanh toán khi nhận hàng")]);
    }
}

internal sealed class FakeDatabaseReadiness : IDatabaseReadiness
{
    public Exception? Failure { get; set; }

    public Task CheckAsync(CancellationToken cancellationToken) => Failure is not null
        ? Task.FromException(Failure)
        : Task.CompletedTask;
}
