using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;
using Xunit;

namespace SportShop.Api.Tests;

// Test catalog qua HTTP với CSDL thật.
[Trait("Category", "MySql")]
public sealed class MySqlCatalogHttpTests
{
    // Kiểm tra API catalog và readiness chạy đúng với CSDL thật.
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

    // Đi hết HTTP → truy vấn MySQL cho lọc nâng cao, danh sách lựa chọn và gợi ý cùng danh mục.
    [MySqlFact]
    public async Task AdvancedFiltersAndRelatedUseRealDatabaseThroughHttp()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClient();
        var products = await client.GetFromJsonAsync<PagedResponse<ProductSummaryDto>>("/api/v1/products?pageSize=1");
        var product = Assert.Single(products!.Items);
        var detail = await client.GetFromJsonAsync<ProductDetailDto>($"/api/v1/products/{product.Id}");
        var variant = detail!.Variants.First(value => value.Stock > 0);
        var query = $"/api/v1/products?categoryId={product.CategoryId}&q={Uri.EscapeDataString(product.Name)}&minPrice={variant.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)}&maxPrice={variant.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (!string.IsNullOrWhiteSpace(variant.Size)) query += "&sizes=" + Uri.EscapeDataString(variant.Size);
        if (!string.IsNullOrWhiteSpace(variant.Color)) query += "&colors=" + Uri.EscapeDataString(variant.Color);
        var filtered = await client.GetFromJsonAsync<PagedResponse<ProductSummaryDto>>(query);
        Assert.Contains(filtered!.Items, item => item.Id == product.Id);
        var options = await client.GetFromJsonAsync<ProductFilterOptionsDto>("/api/v1/products/filter-options?inStockOnly=false");
        if (!string.IsNullOrWhiteSpace(variant.Size)) Assert.Contains(variant.Size.Trim(), options!.Sizes);
        if (!string.IsNullOrWhiteSpace(variant.Color)) Assert.Contains(variant.Color.Trim(), options!.Colors);
        var related = await client.GetFromJsonAsync<ProductSummaryDto[]>($"/api/v1/products/{product.Id}/related?limit=3");
        Assert.InRange(related!.Length, 0, 3);
        Assert.All(related, item =>
        {
            Assert.NotEqual(product.Id, item.Id);
            Assert.Equal(product.CategoryId, item.CategoryId);
            Assert.True(item.InStock);
        });
        Assert.Equal(related.Length, related.DistinctBy(item => item.Id).Count());
    }
}
