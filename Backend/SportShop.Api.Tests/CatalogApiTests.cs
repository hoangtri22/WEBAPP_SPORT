using System.Net;
using System.Text;
using System.Text.Json;
using SportShop.Application;
using Xunit;

namespace SportShop.Api.Tests;

// Test API catalog với dữ liệu giả.
public sealed class CatalogApiTests
{
    // Kiểm tra danh sách sản phẩm có giới hạn mặc định và thông tin phân trang.
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

    // Kiểm tra bộ lọc hợp lệ được chuyển xuống reader và từ khóa được chuẩn hóa.
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

    // Các mảng lặp được bind và chuẩn hóa cùng các điều kiện cũ, không thay contract phân trang.
    [Fact]
    public async Task ProductList_ForwardsCombinedAdvancedFilters()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products?categoryId=2&q=giay&sort=price_desc&inStockOnly=false&page=2&pageSize=3&minPrice=0&maxPrice=2500000.50&sizes=%2042%20&sizes=43&sizes=42&colors=Xanh&colors=%C4%90en");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var search = Assert.IsType<SportShop.Application.Catalog.ProductSearch>(factory.Reader.LastSearch);
        Assert.Equal(0m, search.MinPrice);
        Assert.Equal(2500000.50m, search.MaxPrice);
        Assert.Equal(new[] { "42", "43" }, search.Sizes);
        Assert.Equal(new[] { "Xanh", "Đen" }, search.Colors);
        Assert.Equal(2, search.CategoryId);
        Assert.Equal("giay", search.Q);
        Assert.Equal("price_desc", search.Sort);
        Assert.False(search.InStockOnly);
        Assert.Equal(2, search.Page);
        Assert.Equal(3, search.PageSize);
    }

    // Danh sách chọn size/màu có route riêng, hỗ trợ cả biến thể hết hàng nếu yêu cầu.
    [Fact]
    public async Task FilterOptions_ReturnsValuesAndHonorsStockFlag()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products/filter-options?inStockOnly=false");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJson(response);
        Assert.Equal(new[] { "42", "43" }, payload.GetProperty("sizes").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(new[] { "Xanh", "Đen" }, payload.GetProperty("colors").EnumerateArray().Select(item => item.GetString()));
        Assert.False(factory.Reader.LastOptionsInStockOnly);
    }

    // Endpoint liên quan suy ra danh mục từ sản phẩm và loại ID trước khi giới hạn kết quả.
    [Fact]
    public async Task Related_UsesCurrentCategoryExcludesCurrentProductAndPreservesSummaryContract()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products/7/related?limit=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single((await ReadJson(response)).EnumerateArray());
        Assert.Equal(8, item.GetProperty("id").GetInt32());
        Assert.Equal(2, item.GetProperty("categoryId").GetInt32());
        Assert.Equal("VND", item.GetProperty("currency").GetString());
        var search = factory.Reader.LastSearch!;
        Assert.Equal(2, search.CategoryId);
        Assert.Equal(7, search.ExcludeProductId);
        Assert.True(search.InStockOnly);
        Assert.Equal(1, search.PageSize);
    }

    // Sản phẩm không công khai/không tồn tại không cung cấp gợi ý.
    [Fact]
    public async Task Related_UnknownProductReturns404WithoutListingCatalog()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products/999/related");
        await AssertProblem(response, HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
        Assert.Equal(0, factory.Reader.ProductListCalls);
    }

    // Giới hạn query trên endpoint mới vẫn dùng ProblemDetails validation chuẩn của API.
    [Theory]
    [InlineData("/api/v1/products/0/related")]
    [InlineData("/api/v1/products/7/related?limit=0")]
    [InlineData("/api/v1/products/7/related?limit=21")]
    [InlineData("/api/v1/products/filter-options?inStockOnly=invalid")]
    public async Task NewCatalogEndpoints_RejectInvalidQueries(string route)
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync(route);
        await AssertProblem(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Equal(0, factory.Reader.ProductListCalls);
    }

    // Quá nhiều lựa chọn không tạo danh sách tham số SQL không giới hạn.
    [Theory]
    [InlineData("sizes")]
    [InlineData("colors")]
    public async Task ProductList_RejectsMoreThanFiftyFilterValues(string parameter)
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products?" + string.Join('&', Enumerable.Range(1, 51).Select(i => $"{parameter}={i}")));
        await AssertProblem(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Equal(0, factory.Reader.ProductListCalls);
    }

    // Kiểm tra truy vấn sai bị từ chối trước khi gọi reader.
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
    [InlineData("minPrice=-1")]
    [InlineData("maxPrice=-1")]
    [InlineData("minPrice=abc")]
    [InlineData("maxPrice=10000000000")]
    [InlineData("minPrice=20&maxPrice=10")]
    [InlineData("sizes=%20")]
    [InlineData("sizes=123456789012345678901")]
    [InlineData("colors=%20")]
    [InlineData("colors=1234567890123456789012345678901")]
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

    // Kiểm tra từ khóa dài hơn 100 ký tự bị từ chối.
    [Fact]
    public async Task ProductList_RejectsSearchLongerThanOneHundredCharacters()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products?q=" + new string('a', 101));
        await AssertProblem(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Equal(0, factory.Reader.ProductListCalls);
    }

    // Kiểm tra từ khóa dài tối đa và trang lớn không bị tràn số.
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

    // Kiểm tra chi tiết sản phẩm trả đúng trường công khai và giữ biến thể hết hàng.
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

    // Kiểm tra đường dẫn ảnh không an toàn không bị lộ.
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

    // Kiểm tra URL ảnh dùng host cấu hình, không theo host của request.
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

    // Kiểm tra thiếu ảnh thì trả null.
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

    // Kiểm tra sản phẩm không tồn tại trả 404 dạng JSON.
    [Fact]
    public async Task ProductDetail_UnknownProductReturnsJson404()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/api/v1/products/999");
        await AssertProblem(response, HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
    }

    // Kiểm tra các danh sách công khai trả JSON đúng kiểu.
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

    // Kiểm tra thời gian khuyến mãi dùng múi giờ CSDL.
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

    // Kiểm tra lỗi reader trả ProblemDetails đã che thông tin kể cả khi yêu cầu HTML.
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

    // Kiểm tra catalog không có API ghi công khai.
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

    // Đọc JSON từ phản hồi.
    internal static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    // Kiểm tra phản hồi lỗi đúng dạng ProblemDetails.
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

    // Kiểm tra phản hồi không chứa trường riêng tư.
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
