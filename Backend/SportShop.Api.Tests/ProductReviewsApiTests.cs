using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using SportShop.Contracts.Catalog;
using Xunit;

namespace SportShop.Api.Tests;

// Kiểm tra endpoint đánh giá công khai và tương thích của bộ lọc lịch sử đánh giá riêng.
public sealed class ProductReviewsApiTests
{
    // Các tham số lỗi bị chặn trước khi truy cập CSDL hoặc yêu cầu đăng nhập.
    [Theory]
    [InlineData("0/reviews")]
    [InlineData("-1/reviews")]
    [InlineData("not-a-number/reviews")]
    [InlineData("1/reviews?page=0")]
    [InlineData("1/reviews?page=-1")]
    [InlineData("1/reviews?page=2147483648")]
    [InlineData("1/reviews?pageSize=0")]
    [InlineData("1/reviews?pageSize=101")]
    [InlineData("1/reviews?pageSize=not-a-number")]
    public async Task InvalidPublicQueriesAreRejectedBeforeDatabaseAccess(string path)
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        await CatalogApiTests.AssertProblem(await client.GetAsync("/api/v1/products/" + path),
            HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    // Không cấu hình CSDL phải báo 503 rõ ràng thay vì giả danh sách đánh giá rỗng.
    [Fact]
    public async Task PublicReviewsWithoutConfiguredDatabaseReturnServiceUnavailable()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        await CatalogApiTests.AssertProblem(await client.GetAsync("/api/v1/products/1/reviews"),
            HttpStatusCode.ServiceUnavailable, "DATABASE_UNAVAILABLE");
    }

    // Bộ lọc productId mới của lịch sử riêng không chấp nhận mã sản phẩm không hợp lệ.
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public async Task PrivateHistoryRejectsInvalidProductFilter(string productId)
    {
        using var factory = new CommerceValidationFactory("Customer");
        using var client = factory.CreateClient();
        await CatalogApiTests.AssertProblem(await client.GetAsync("/api/v1/me/feedback?productId=" + productId),
            HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    // OpenAPI công bố schema phản hồi và không yêu cầu bearer cho đánh giá công khai.
    [Fact]
    public async Task OpenApiDocumentsPublicProductReviewsWithoutAuthentication()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var operation = document.GetProperty("paths").GetProperty("/api/v1/products/{productId}/reviews").GetProperty("get");
        Assert.True(!operation.TryGetProperty("security", out var security) || security.GetArrayLength() == 0);
        Assert.True(operation.GetProperty("responses").TryGetProperty("404", out _));
        Assert.True(operation.GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/json").TryGetProperty("schema", out _));
        var schema = document.GetProperty("components").GetProperty("schemas").GetProperty("ProductReviewsResponse");
        Assert.True(schema.GetProperty("properties").TryGetProperty("averageStars", out _));
    }

    // CSDL thật: ẩn phản hồi riêng, giữ phân trang/tổng/sao thống nhất, không lộ dữ liệu khách hoặc sản phẩm ẩn.
    [MySqlFact, Trait("Category", "MySql")]
    public async Task PublicReviewsRespectVisibilityPrivacyPaginationAndProductAvailability()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClient();
        await using var connection = await factory.OpenAsync();
        var categoryId = await connection.ExecuteScalarAsync<int>("INSERT INTO LOAI_SP(ten_loai) VALUES(@name); SELECT LAST_INSERT_ID();",
            new { name = "Review test " + Guid.NewGuid().ToString("N") });
        var productId = 0;
        try
        {
            productId = await connection.ExecuteScalarAsync<int>("""
                INSERT INTO SAN_PHAM(ma_loai,ten_san_pham,don_vi_tinh,trang_thai)
                VALUES(@categoryId,'Review test product','cái','hien'); SELECT LAST_INSERT_ID();
                """, new { categoryId });
            var route = $"/api/v1/products/{productId}/reviews";
            var empty = await client.GetFromJsonAsync<ProductReviewsResponse>(route);
            Assert.NotNull(empty);
            Assert.Empty(empty.Items);
            Assert.Equal(0, empty.TotalItems);
            Assert.Null(empty.AverageStars);

            var customerId = await connection.ExecuteScalarAsync<int>("SELECT ma_khach_hang FROM KHACH_HANG ORDER BY ma_khach_hang LIMIT 1");
            var sentAt = new DateTime(2026, 11, 8, 10, 30, 0, DateTimeKind.Unspecified);
            async Task<int> AddReview(int? stars, string content, int visible = 1, string type = "danh_gia") =>
                await connection.ExecuteScalarAsync<int>("""
                    INSERT INTO PHAN_HOI(ma_khach_hang,ma_san_pham,loai,so_sao,noi_dung,an_hien,ngay_gui)
                    VALUES(@customerId,@productId,@type,@stars,@content,@visible,@sentAt); SELECT LAST_INSERT_ID();
                    """, new { customerId, productId, type, stars, content, visible, sentAt });

            var oldestId = await AddReview(5, "Visible five stars");
            var middleId = await AddReview(3, "Visible three stars");
            await AddReview(5, "Private hidden review", visible: 0);
            await AddReview(5, "Private complaint", type: "khieu_nai");
            await AddReview(null, "Review without stars");
            var newestId = await AddReview(1, "Visible one star");
            foreach (var reply in new[] { "Old staff reply", "Latest staff reply" })
                await connection.ExecuteAsync("""
                    INSERT INTO TRA_LOI_PHAN_HOI(ma_phan_hoi,ma_nhan_vien,noi_dung)
                    SELECT @newestId,ma_nhan_vien,@reply FROM NHAN_VIEN ORDER BY ma_nhan_vien LIMIT 1
                    """, new { newestId, reply });

            var first = await client.GetFromJsonAsync<ProductReviewsResponse>(route + "?pageSize=2");
            Assert.NotNull(first);
            Assert.Equal(3, first.TotalItems);
            Assert.Equal(3m, first.AverageStars);
            Assert.Equal(new[] { newestId, middleId }, first.Items.Select(item => item.Id));
            Assert.Equal("Latest staff reply", first.Items[0].Reply);
            Assert.All(first.Items, item =>
            {
                Assert.Equal("Khách hàng", item.Author);
                Assert.Equal(new DateTimeOffset(sentAt, TimeSpan.FromHours(7)), item.SentAt);
            });
            var second = await client.GetFromJsonAsync<ProductReviewsResponse>(route + "?page=2&pageSize=2");
            Assert.NotNull(second);
            Assert.Equal(oldestId, Assert.Single(second.Items).Id);
            Assert.Equal(first.TotalItems, second.TotalItems);
            Assert.Equal(first.AverageStars, second.AverageStars);
            var beyondLast = await client.GetFromJsonAsync<ProductReviewsResponse>(route + "?page=2147483647&pageSize=100");
            Assert.NotNull(beyondLast);
            Assert.Empty(beyondLast.Items);
            Assert.Equal(3, beyondLast.TotalItems);

            var json = await client.GetFromJsonAsync<JsonElement>(route);
            foreach (var item in json.GetProperty("items").EnumerateArray())
                Assert.Equal(new[] { "author", "content", "id", "reply", "sentAt", "stars" }, item.EnumerateObject().Select(p => p.Name).Order());
            using var ownFactory = new CommerceValidationFactory("Customer", Environment.GetEnvironmentVariable("SPORTSHOP_TEST_MYSQL")!, customerId);
            using var ownClient = ownFactory.CreateClient();
            var own = await ownClient.GetFromJsonAsync<JsonElement>($"/api/v1/me/feedback?productId={productId}&pageSize=2");
            Assert.Equal(6, own.GetProperty("totalItems").GetInt64());
            Assert.Equal(2, own.GetProperty("items").GetArrayLength());
            Assert.All(own.GetProperty("items").EnumerateArray(), item => Assert.Equal(productId, item.GetProperty("productId").GetInt32()));
            var otherProduct = await ownClient.GetFromJsonAsync<JsonElement>("/api/v1/me/feedback?productId=2147483647");
            Assert.Equal(0, otherProduct.GetProperty("totalItems").GetInt64());
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/me/feedback?productId={productId}")).StatusCode);

            await connection.ExecuteAsync("UPDATE SAN_PHAM SET trang_thai='an' WHERE ma_san_pham=@productId", new { productId });
            await CatalogApiTests.AssertProblem(await client.GetAsync(route), HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
            await connection.ExecuteAsync("UPDATE SAN_PHAM SET trang_thai='hien' WHERE ma_san_pham=@productId", new { productId });
            var archivedId = await connection.ExecuteScalarAsync<int?>("SELECT ma_loai FROM LOAI_SP WHERE ten_loai='[Đã xóa]'");
            if (archivedId is int existingArchived)
                await connection.ExecuteAsync("UPDATE SAN_PHAM SET ma_loai=@existingArchived WHERE ma_san_pham=@productId", new { existingArchived, productId });
            else
                await connection.ExecuteAsync("UPDATE LOAI_SP SET ten_loai='[Đã xóa]' WHERE ma_loai=@categoryId", new { categoryId });
            await CatalogApiTests.AssertProblem(await client.GetAsync(route), HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
            await CatalogApiTests.AssertProblem(await client.GetAsync("/api/v1/products/2147483647/reviews"), HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
        }
        finally
        {
            await connection.ExecuteAsync("""
                DELETE r FROM TRA_LOI_PHAN_HOI r JOIN PHAN_HOI ph ON ph.ma_phan_hoi=r.ma_phan_hoi WHERE ph.ma_san_pham=@productId;
                DELETE FROM PHAN_HOI WHERE ma_san_pham=@productId;
                DELETE FROM SAN_PHAM WHERE ma_san_pham=@productId;
                DELETE FROM LOAI_SP WHERE ma_loai=@categoryId;
                """, new { productId, categoryId });
        }
    }
}
