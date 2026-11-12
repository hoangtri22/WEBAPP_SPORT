using System.Net;
using System.Net.Http.Json;
using SportShop.Contracts.Catalog;
using SportShop.Mobile.Services;
using Xunit;

namespace SportShop.Mobile.Tests;

public sealed class RelatedCatalogApiTests
{
    [Fact]
    public async Task RelatedRequestUsesViewedProductIdAndPreservesBackendCardData()
    {
        Uri? requested = null;
        var item = new ProductSummaryDto(42, "Áo cùng loại", 3, "Áo", "chiếc",
            "https://shop.example/images/ao.jpg", 150_000, 200_000, true);
        using var http = Client((request, _) =>
        {
            requested = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(new[] { item }) });
        });
        var result = await new CatalogApi(http).GetRelatedProductsAsync(17);
        Assert.Equal("/api/v1/products/17/related?limit=8", requested!.PathAndQuery);
        Assert.Equal(item, Assert.Single(result));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{\"id\":0}]")]
    [InlineData("{\"items\":[]}")]
    [InlineData("<html>gateway error</html>")]
    public async Task InvalidRelatedResponsesBecomeRetryableErrors(string json)
    {
        using var http = Client((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(json) }));
        await Assert.ThrowsAsync<CatalogException>(() => new CatalogApi(http).GetRelatedProductsAsync(17));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidViewedProductDoesNotSendARequest(int productId)
    {
        var calls = 0;
        using var http = Client((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<ProductSummaryDto>()) });
        });
        await Assert.ThrowsAsync<CatalogException>(() => new CatalogApi(http).GetRelatedProductsAsync(productId));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task RelatedFailureDoesNotExposeServerDiagnostics()
    {
        using var http = Client((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        { Content = new StringContent("private database diagnostics") }));
        var error = await Assert.ThrowsAsync<CatalogException>(() => new CatalogApi(http).GetRelatedProductsAsync(17));
        Assert.DoesNotContain("database diagnostics", error.Message);
    }

    [Fact]
    public async Task RelatedNetworkFailureAndTimeoutAreRetryableButCancellationRemainsCancellation()
    {
        using var offline = Client((_, _) => throw new HttpRequestException());
        await Assert.ThrowsAsync<CatalogException>(() => new CatalogApi(offline).GetRelatedProductsAsync(17));
        using var timeout = Client((_, _) => throw new TaskCanceledException());
        await Assert.ThrowsAsync<CatalogException>(() => new CatalogApi(timeout).GetRelatedProductsAsync(17));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        using var http = Client((_, token) => Task.FromCanceled<HttpResponseMessage>(token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CatalogApi(http).GetRelatedProductsAsync(17, canceled.Token));
    }

    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        => new(new Handler(respond)) { BaseAddress = new Uri("http://localhost:5080/") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }
}
