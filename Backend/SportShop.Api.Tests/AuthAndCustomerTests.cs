using System.Net;
using System.Net.Http.Json;
using SportShop.CustomerWeb.Models;
using SportShop.CustomerWeb.Services;
using Xunit;

namespace SportShop.Api.Tests;

public sealed class AuthAndCustomerTests
{
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

    [Fact]
    public async Task Bearer_MalformedTokenIsUnauthorizedBeforeDatabase()
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "invalid" })).StatusCode);
    }

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
