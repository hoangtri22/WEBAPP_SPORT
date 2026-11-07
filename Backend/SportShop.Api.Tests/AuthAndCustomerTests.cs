using System.Net;
using System.Net.Http.Json;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;
using Xunit;

namespace SportShop.Api.Tests;

// Test đăng nhập, mật khẩu và kiểm tra dữ liệu khách không cần CSDL.
public sealed class AuthAndCustomerTests
{
    // Kiểm tra PasswordStorage dùng salt, nhận mật khẩu cũ và từ chối hash sai định dạng.
    [Fact]
    public void PasswordStorage_UsesSaltAndSupportsLegacyWithoutAcceptingMalformedHashes()
    {
        var first = PasswordStorage.Hash("sample-test-password");
        var second = PasswordStorage.Hash("sample-test-password");
        Assert.NotEqual(first, second);
        Assert.True(PasswordStorage.Verify(first, "sample-test-password"));
        Assert.False(PasswordStorage.Verify(first, "wrong-password"));
        Assert.True(PasswordStorage.Verify("legacy-demo", "legacy-demo"));
        Assert.False(PasswordStorage.Verify("$sportshop$999999999999$bad$bad", "sample-test-password"));
        Assert.False(PasswordStorage.Verify("$sportshop$210000$bad$bad", "sample-test-password"));
    }

    // Kiểm tra API khách bắt buộc Bearer và không chuyển hướng.
    [Theory]
    [InlineData("/api/v1/me")]
    [InlineData("/api/v1/me/feedback")]
    [InlineData("/api/v1/me/feedback/eligible-products")]
    [InlineData("/api/v1/me/surveys")]
    [InlineData("/api/v1/me/surveys/1")]
    public async Task CustomerEndpoints_RequireBearerAndNeverRedirect(string path)
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    // Kiểm tra dữ liệu xác thực sai bị từ chối trước khi mở CSDL.
    [Theory]
    [InlineData("/api/v1/auth/login", "{}")]
    [InlineData("/api/v1/auth/register", "{\"username\":\"bad name\",\"email\":\"invalid\",\"phone\":\"1\",\"password\":\"x\"}")]
    [InlineData("/api/v1/auth/refresh", "{}")]
    public async Task AuthValidation_RejectsBadInputWithoutOpeningDatabase(string path, string json)
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        var response = await client.PostAsync(path, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Kiểm tra token sai định dạng trả 401 trước khi mở CSDL.
    [Fact]
    public async Task Bearer_MalformedTokenIsUnauthorizedBeforeDatabase()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "invalid" })).StatusCode);
    }

    // Kiểm tra khảo sát từ chối câu trả lời thiếu, lạ, trùng hoặc sai loại.
    [Fact]
    public void SurveyValidation_RejectsMissingForeignDuplicateAndWrongKindAnswers()
    {
        var questions = new List<SurveyQ> { new(1, "One", "one", true, [new(10, "A"), new(11, "B")]), new(2, "Text", "text", true, []) };
        Assert.NotNull(CustomerStore.ValidateSurveyAnswers(questions, []));
        Assert.NotNull(CustomerStore.ValidateSurveyAnswers(questions, new() { [1] = ([999], null), [2] = ([], "ok") }));
        Assert.NotNull(CustomerStore.ValidateSurveyAnswers(questions, new() { [1] = ([10, 11], null), [2] = ([], "ok") }));
        Assert.NotNull(CustomerStore.ValidateSurveyAnswers(questions, new() { [1] = ([10, 10], null), [2] = ([], "ok") }));
        Assert.NotNull(CustomerStore.ValidateSurveyAnswers(questions, new() { [1] = ([10], null), [2] = ([10], null) }));
        Assert.NotNull(CustomerStore.ValidateSurveyAnswers(questions, new() { [1] = ([10], null), [2] = ([], "ok"), [999] = ([], "foreign") }));
        Assert.Null(CustomerStore.ValidateSurveyAnswers(questions, new() { [1] = ([10], null), [2] = ([], "ok") }));
    }
}
