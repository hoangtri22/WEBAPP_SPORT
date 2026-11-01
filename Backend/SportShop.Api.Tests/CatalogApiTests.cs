using System.Net;
using System.Text;
using System.Text.Json;
using SportShop.Application;
using Xunit;

namespace SportShop.Api.Tests;

public sealed class CatalogApiTests
{
    [Fact]
    public async Task ProductList_UsesBoundedDefaultsAndReturnsPaginationMetadata()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJson(response);
        Assert.Equal(1, payload.GetProperty("page").GetInt32());
        Assert.Equal(20, payload.GetProperty("pageSize").GetInt32());
        Assert.Equal(23, payload.GetProperty("totalItems").GetInt64());
        Assert.Equal(2, payload.GetProperty("totalPages").GetInt64());
        Assert.Single(payload.GetProperty("items").EnumerateArray());
        var search = Assert.IsType<SportShop.Application.Catalog.ProductSearch>(factory.Reader.LastSearch);
        Assert.Equal("new", search.Sort);
        Assert.True(search.InStockOnly);
        Assert.Null(search.CategoryId);
        Assert.Null(search.Q);
    }

    [Fact]
    public async Task ProductList_ForwardsValidatedFiltersAndNormalizesSearch()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products?categoryId=2&q=%20giay%20&sort=price_asc&inStockOnly=false&page=2&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var search = Assert.IsType<SportShop.Application.Catalog.ProductSearch>(factory.Reader.LastSearch);
        Assert.Equal(2, search.CategoryId);
        Assert.Equal("giay", search.Q);
        Assert.Equal("price_asc", search.Sort);
        Assert.False(search.InStockOnly);
        Assert.Equal(2, search.Page);
        Assert.Equal(10, search.PageSize);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=not-a-number")]
    [InlineData("page=2147483648")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=not-a-number")]
    [InlineData("categoryId=0")]
    [InlineData("categoryId=-1")]
    [InlineData("categoryId=not-a-number")]
    [InlineData("sort=price;%20DROP%20TABLE")]
    [InlineData("inStockOnly=not-a-boolean")]
    public async Task ProductList_RejectsInvalidQueriesBeforeReader(string query)
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products?" + query);
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await client.SendAsync(request);
        var payload = await AssertProblem(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.True(payload.GetProperty("errors").EnumerateObject().Any());
        Assert.Equal(0, factory.Reader.ProductListCalls);
    }

    [Fact]
    public async Task ProductList_RejectsSearchLongerThanOneHundredCharacters()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products?q=" + new string('a', 101));
        await AssertProblem(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Equal(0, factory.Reader.ProductListCalls);
    }

    [Fact]
    public async Task ProductList_AllowsMaximumSearchLengthAndLargePageWithoutOverflow()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products?page=2147483647&pageSize=100&q=" + new string('a', 100));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJson(response);
        Assert.Empty(payload.GetProperty("items").EnumerateArray());
        Assert.Equal(int.MaxValue, payload.GetProperty("page").GetInt32());
        Assert.Equal(100, factory.Reader.LastSearch!.PageSize);
    }

    [Fact]
    public async Task ProductDetail_MapsPublicFieldsAndKeepsSoldOutVariants()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products/7");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJson(response);
        Assert.Equal(7, payload.GetProperty("id").GetInt32());
        Assert.Equal("VND", payload.GetProperty("currency").GetString());
        Assert.Equal(1200000m, payload.GetProperty("minPrice").GetDecimal());
        Assert.True(payload.GetProperty("inStock").GetBoolean());
        Assert.Equal("http://localhost:5080/images/giay-pegasus.jpg", payload.GetProperty("imageUrl").GetString());
        var variants = payload.GetProperty("variants").EnumerateArray().ToArray();
        Assert.Equal(2, variants.Length);
        Assert.Equal(JsonValueKind.Null, variants[0].GetProperty("imageUrl").ValueKind);
        Assert.Equal(0, variants[1].GetProperty("stock").GetInt32());
        Assert.Equal(JsonValueKind.Null, variants[1].GetProperty("imageUrl").ValueKind);
        AssertNoPrivateFields(payload);
    }

    [Theory]
    [InlineData("/images/../private.txt")]
    [InlineData("/images/%2e%2e/private.txt")]
    [InlineData("/images/%2e%2e%2fprivate.txt")]
    [InlineData("/images/%252e%252e%252fprivate.txt")]
    [InlineData("/images/%5c..%5cprivate.txt")]
    [InlineData("https://untrusted.example/image.jpg")]
    [InlineData("//untrusted.example/image.jpg")]
    [InlineData("/private/image.jpg")]
    public async Task ProductDetail_DoesNotExposeUnsafeMediaPaths(string path)
    {
        using var factory = new CatalogApiFactory();
        factory.Reader.ImagePath = path;
        factory.Reader.VariantImagePath = path;
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products/7");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJson(response);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("imageUrl").ValueKind);
        Assert.All(payload.GetProperty("variants").EnumerateArray(), variant =>
            Assert.Equal(JsonValueKind.Null, variant.GetProperty("imageUrl").ValueKind));
    }

    [Fact]
    public async Task ProductDetail_UsesConfiguredMediaHostWithoutDependingOnRequestHost()
    {
        using var factory = new CatalogApiFactory(mediaBaseUrl: "https://media.example.com/");
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products/7");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJson(response);
        Assert.Equal("https://media.example.com/images/giay-pegasus.jpg", payload.GetProperty("imageUrl").GetString());
    }

    [Fact]
    public async Task ProductDetail_MissingImageReturnsNullInsteadOfBrokenMediaUrl()
    {
        using var factory = new CatalogApiFactory();
        factory.Reader.ImagePath = "/images/unavailable-test-fixture-731a9820.jpg";
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products/7");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJson(response);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("imageUrl").ValueKind);
    }

    [Fact]
    public async Task ProductDetail_UnknownProductReturnsJson404()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products/999");
        await AssertProblem(response, HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
    }

    [Theory]
    [InlineData("categories", "name")]
    [InlineData("interests", "name")]
    [InlineData("payment-methods", "code")]
    public async Task PublicReferenceLists_ReturnTypedJson(string resource, string expectedField)
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/" + resource);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJson(response);
        var item = Assert.Single(payload.EnumerateArray());
        Assert.True(item.TryGetProperty(expectedField, out _));
        AssertNoPrivateFields(payload);
    }

    [Fact]
    public async Task Promotions_ReturnTimestampWithConfiguredDatabaseOffset()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/promotions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJson(response);
        var promotion = Assert.Single(payload.EnumerateArray());
        Assert.Equal("SPORT5", promotion.GetProperty("code").GetString());
        Assert.EndsWith("+07:00", promotion.GetProperty("endsAt").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReaderFailures_ReturnSanitizedProblemDetailsEvenForHtmlAccept(bool databaseUnavailable)
    {
        const string secret = "Sensitive-connection-and-stack-marker";
        using var factory = new CatalogApiFactory();
        factory.Reader.Failure = databaseUnavailable
            ? new DatabaseUnavailableException(secret)
            : new InvalidOperationException(secret);
        using var client = factory.Client();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await client.SendAsync(request);
        await AssertProblem(response,
            databaseUnavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.InternalServerError,
            databaseUnavailable ? "DATABASE_UNAVAILABLE" : "INTERNAL_ERROR");
        Assert.DoesNotContain(secret, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/v1/products")]
    [InlineData("/api/v1/products/7")]
    [InlineData("/api/v1/categories")]
    [InlineData("/api/v1/cart")]
    public async Task ReadOnlyCatalog_DoesNotExposePublicWriteEndpoints(string route)
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.PostAsync(route, new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    internal static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    internal static async Task<JsonElement> AssertProblem(HttpResponseMessage response, HttpStatusCode expectedStatus, string code)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var payload = await ReadJson(response);
        Assert.Equal((int)expectedStatus, payload.GetProperty("status").GetInt32());
        Assert.Equal(code, payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
        return payload;
    }

    static void AssertNoPrivateFields(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payload.EnumerateArray()) AssertNoPrivateFields(item);
        }
        else if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in payload.EnumerateObject())
            {
                Assert.DoesNotContain(field.Name.ToLowerInvariant(), new[] { "password", "passwordhash", "cost", "costprice", "gia_von", "mat_khau" });
                AssertNoPrivateFields(field.Value);
            }
        }
    }
}
