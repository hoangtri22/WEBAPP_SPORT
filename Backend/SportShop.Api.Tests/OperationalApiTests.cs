using System.Net;
using SportShop.Application;
using Xunit;

namespace SportShop.Api.Tests;

public sealed class OperationalApiTests
{
    [Fact]
    public async Task HealthReadiness_ReportsSuccessWhenRequiredSchemaIsAvailable()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await CatalogApiTests.ReadJson(response);
        Assert.Equal("ready", payload.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthLiveness_DoesNotDependOnDatabase()
    {
        using var factory = new CatalogApiFactory();
        factory.Readiness.Failure = new DatabaseUnavailableException("Must not be queried by liveness");
        using var client = factory.Client();
        using var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await CatalogApiTests.ReadJson(response);
        Assert.Equal("healthy", payload.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthReadiness_ReturnsSanitizedJsonWhenDatabaseUnavailable()
    {
        using var factory = new CatalogApiFactory();
        const string secret = "Sensitive-database-configuration-marker";
        factory.Readiness.Failure = new DatabaseUnavailableException(secret);
        using var client = factory.Client();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await client.SendAsync(request);
        await CatalogApiTests.AssertProblem(response, HttpStatusCode.ServiceUnavailable, "DATABASE_UNAVAILABLE");
        Assert.DoesNotContain(secret, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MissingConfiguration_RealDatabaseServicesFailClosedWithoutNetworkCalls()
    {
        using var factory = new CatalogApiFactory(replaceDatabaseServices: false);
        using var client = factory.Client();
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        using var ready = await client.GetAsync("/health/ready");
        await CatalogApiTests.AssertProblem(ready, HttpStatusCode.ServiceUnavailable, "DATABASE_UNAVAILABLE");
        using var products = await client.GetAsync("/api/v1/products");
        await CatalogApiTests.AssertProblem(products, HttpStatusCode.ServiceUnavailable, "DATABASE_UNAVAILABLE");
        using var login = await client.PostAsync("/api/v1/auth/login", new StringContent(
            "{\"username\":\"test-customer\",\"password\":\"test-password\"}", System.Text.Encoding.UTF8, "application/json"));
        await CatalogApiTests.AssertProblem(login, HttpStatusCode.ServiceUnavailable, "DATABASE_UNAVAILABLE");
    }

    [Fact]
    public async Task DevelopmentOpenApi_DocumentsReadEndpointsWithoutDatabase()
    {
        using var factory = new CatalogApiFactory(replaceDatabaseServices: false);
        using var client = factory.Client();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await CatalogApiTests.ReadJson(response);
        Assert.True(document.TryGetProperty("openapi", out _));
        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/products", out var products));
        Assert.True(products.TryGetProperty("get", out _));
        Assert.False(products.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/v1/products/{productId}", out _));
    }

    [Fact]
    public async Task ProductionOpenApi_IsNotPubliclyExposed()
    {
        using var factory = new CatalogApiFactory(replaceDatabaseServices: false, environment: "Production");
        using var client = factory.Client();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExistingImage_IsServedWithImageContentTypeWithoutDatabase()
    {
        using var factory = new CatalogApiFactory(replaceDatabaseServices: false);
        using var client = factory.Client();
        using var response = await client.GetAsync("/images/giay-pegasus.jpg");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 2);
        Assert.Equal(0xff, bytes[0]);
        Assert.Equal(0xd8, bytes[1]);
    }

    [Theory]
    [InlineData("/images/unavailable-test-fixture-731a9820.jpg")]
    [InlineData("/uploads/.gitkeep")]
    [InlineData("/images/%2e%2e/Database/sports_shop.sql")]
    public async Task StaticFiles_DoNotServeMissingFilesOrNonImageContent(string path)
    {
        using var factory = new CatalogApiFactory(replaceDatabaseServices: false);
        using var client = factory.Client();
        using var response = await client.GetAsync(path);
        await CatalogApiTests.AssertProblem(response, HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
    }
}
