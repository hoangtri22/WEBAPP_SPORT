using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SportShop.Api.Tests;

[Trait("Category", "MySql")]
public sealed class MySqlCatalogHttpTests
{
    [MySqlFact]
    public async Task PublicCatalogAndReadinessUseRealDatabaseThroughHttp()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClient();
        foreach (var path in new[] { "/health/ready", "/api/v1/categories", "/api/v1/promotions", "/api/v1/interests", "/api/v1/payment-methods" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        }
        var products = await client.GetFromJsonAsync<JsonElement>("/api/v1/products?pageSize=1&inStockOnly=true");
        Assert.True(products.GetProperty("totalItems").GetInt64() > 0);
        var id = products.GetProperty("items")[0].GetProperty("id").GetInt32();
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/products/{id}");
        Assert.Equal(id, detail.GetProperty("id").GetInt32());
        Assert.NotEmpty(detail.GetProperty("variants").EnumerateArray());
        var literalSearch = await client.GetFromJsonAsync<JsonElement>("/api/v1/products?q=%25_%21&inStockOnly=false");
        Assert.True(literalSearch.GetProperty("totalItems").GetInt64() > 0, "Import mobile-edge-cases.sql when running the full verification script.");
    }
}
