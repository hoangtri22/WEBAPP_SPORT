using System.Net;
using SportShop.CustomerWeb.Models;
using Xunit;

namespace SportShop.Api.Tests;

public sealed class ContentAndOpenApiTests
{
    [Fact]
    public async Task EveryWebInformationPageIsAvailableWithTheSameContentWithoutDatabase()
    {
        using var factory = new CatalogApiFactory(replaceDatabaseServices: false);
        using var client = factory.Client();
        var summaries = await CatalogApiTests.ReadJson(await client.GetAsync("/api/v1/pages"));
        Assert.Equal(InfoPages.All.Keys.Order(), summaries.EnumerateArray().Select(p => p.GetProperty("slug").GetString()).Order());
        foreach (var (slug, page) in InfoPages.All)
        {
            using var response = await client.GetAsync($"/api/v1/pages/{slug}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await CatalogApiTests.ReadJson(response);
            Assert.Equal(page.Title, json.GetProperty("title").GetString());
            Assert.Equal(page.Intro, json.GetProperty("intro").GetString());
            Assert.Equal(page.Sections.Length, json.GetProperty("sections").GetArrayLength());
        }
        await CatalogApiTests.AssertProblem(await client.GetAsync("/api/v1/pages/missing"), HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
    }

    [Theory]
    [InlineData("GET", "/api/v1/me")]
    [InlineData("PUT", "/api/v1/me")]
    [InlineData("POST", "/api/v1/me/password")]
    [InlineData("GET", "/api/v1/me/feedback")]
    [InlineData("POST", "/api/v1/me/feedback")]
    [InlineData("GET", "/api/v1/me/surveys")]
    [InlineData("POST", "/api/v1/me/surveys/1/responses")]
    [InlineData("POST", "/api/v1/auth/logout")]
    public async Task StaffRoleCannotUseCustomerEndpoints(string method, string path)
    {
        using var factory = new CommerceValidationFactory("Staff");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocumentsAuthenticationTypedResponsesAndAllWebFeatureGroups()
    {
        using var factory = new CatalogApiFactory(replaceDatabaseServices: false);
        using var client = factory.Client();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await CatalogApiTests.ReadJson(response);
        var paths = document.GetProperty("paths");
        foreach (var path in new[] { "/api/v1/auth/register", "/api/v1/auth/login", "/api/v1/auth/refresh", "/api/v1/auth/logout",
            "/api/v1/me", "/api/v1/me/password", "/api/v1/products", "/api/v1/products/{productId}", "/api/v1/categories",
            "/api/v1/promotions", "/api/v1/interests", "/api/v1/payment-methods", "/api/v1/cart", "/api/v1/cart/items",
            "/api/v1/cart/items/{variantId}", "/api/v1/checkout", "/api/v1/checkout/quote", "/api/v1/checkout/vouchers",
            "/api/v1/orders", "/api/v1/orders/{orderId}", "/api/v1/orders/{orderId}/cancel", "/api/v1/me/feedback",
            "/api/v1/me/feedback/eligible-products", "/api/v1/me/surveys", "/api/v1/me/surveys/{id}",
            "/api/v1/me/surveys/{id}/responses", "/api/v1/pages", "/api/v1/pages/{slug}" })
            Assert.True(paths.TryGetProperty(path, out _), path);
        Assert.Equal("http", document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("type").GetString());
        Assert.True(paths.GetProperty("/api/v1/orders").GetProperty("post").GetProperty("security").GetArrayLength() > 0);
        Assert.True(paths.GetProperty("/api/v1/me").GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/json").TryGetProperty("schema", out _));
        Assert.True(paths.GetProperty("/api/v1/orders").GetProperty("post").GetProperty("responses").TryGetProperty("401", out _));
        var export = Environment.GetEnvironmentVariable("SPORTSHOP_EXPORT_OPENAPI");
        if (!string.IsNullOrWhiteSpace(export))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(export))!);
            await File.WriteAllTextAsync(export, await response.Content.ReadAsStringAsync());
        }
    }
}
