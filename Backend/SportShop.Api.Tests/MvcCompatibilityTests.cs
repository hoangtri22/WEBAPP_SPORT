using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using SportShop.Contracts.Customer;
using SportShop.CustomerWeb.Models;
using Xunit;
using CustomerHome = SportShop.CustomerWeb.Controllers.HomeController;
using AdminAccount = SportShop.Admin.Controllers.AccountController;

namespace SportShop.Api.Tests;

[Trait("Category", "MySql")]
public sealed class MvcCompatibilityTests
{
    [MySqlFact]
    public async Task CustomerWeb_RendersRealHomeCatalogAndEveryInfoPageAsHtml()
    {
        using var web = new MvcHostFactory<CustomerHome>();
        using var client = web.Client();
        await AssertHtmlAsync(await client.GetAsync("/"));
        var catalog = await AssertHtmlAsync(await client.GetAsync("/Shop?sort=price_asc"));
        Assert.Contains("/Shop/Detail/", catalog, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Chức năng này sẽ hoạt động khi", WebUtility.HtmlDecode(catalog));
        foreach (var slug in InfoPages.All.Keys)
        {
            var html = await AssertHtmlAsync(await client.GetAsync("/Info/Page/" + slug));
            Assert.Contains(InfoPages.All[slug].Title, WebUtility.HtmlDecode(html));
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Info/Page/no-such-page")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Customer/Profile")).StatusCode);
    }

    [MySqlFact]
    public async Task MobileCreatedHashedAccount_CanLoginWebAndSharesProfileAndCartBothWays()
    {
        using var api = new CustomerApiFactory();
        using var mobile = api.CreateClient();
        var username = "mvcapi_" + Guid.NewGuid().ToString("N")[..18];
        var registration = new RegisterRequest(username, username + "@example.test", "0900000001", "mvc-test-password");
        var registered = await mobile.PostAsJsonAsync("/api/v1/auth/register", registration);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var token = (await registered.Content.ReadFromJsonAsync<TokenResponse>())!;
        mobile.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var web = new MvcHostFactory<CustomerHome>();
        using var browser = web.Client();
        var form = await LoginFormAsync(browser);
        var login = await browser.PostAsync("/Account/Login", Form(form, ("username", username), ("password", registration.Password)));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var profile = await AssertHtmlAsync(await browser.GetAsync("/Customer/Profile"));
        Assert.Contains(username + "@example.test", profile);

        var fullName = "Nguyễn Khách Mobile";
        var updated = await mobile.PutAsJsonAsync("/api/v1/me", new UpdateProfileRequest(fullName, registration.Email, registration.Phone, new DateTime(2000, 1, 1), "Nam", []));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        profile = await AssertHtmlAsync(await browser.GetAsync("/Customer/Profile"));
        Assert.Contains(fullName, WebUtility.HtmlDecode(profile));
        var profileToken = AntiForgery(profile);
        var webProfileUpdate = await browser.PostAsync("/Customer/Profile", Form(profileToken,
            ("fullName", "Nguyễn Khách Web"), ("email", registration.Email), ("phone", registration.Phone), ("gender", "Nam"), ("birth", "2000-01-01")));
        Assert.Equal(HttpStatusCode.Redirect, webProfileUpdate.StatusCode);
        var mobileProfile = await mobile.GetFromJsonAsync<JsonElement>("/api/v1/me");
        Assert.Equal("Nguyễn Khách Web", mobileProfile.GetProperty("profile").GetProperty("fullName").GetString());

        await using var c = await api.OpenAsync();
        var variantId = await c.ExecuteScalarAsync<int>("""
            SELECT bt.ma_bien_the FROM BIEN_THE_SP bt JOIN SAN_PHAM sp ON sp.ma_san_pham=bt.ma_san_pham
            JOIN LOAI_SP l ON l.ma_loai=sp.ma_loai
            WHERE bt.so_luong_ton>=3 AND bt.trang_thai='hien' AND sp.trang_thai='hien' AND l.ten_loai<>'[Đã xóa]' LIMIT 1
            """);
        Assert.True(variantId > 0);
        Assert.Equal(HttpStatusCode.OK, (await mobile.PostAsJsonAsync("/api/v1/cart/items", new { variantId, quantity = 1 })).StatusCode);
        var cart = await AssertHtmlAsync(await browser.GetAsync("/Cart"));
        Assert.Contains("name=\"variantId\" value=\"" + variantId + "\"", cart);
        var updatedCart = await browser.PostAsync("/Cart/Update", Form(AntiForgery(cart), ("variantId", variantId.ToString()), ("qty", "2")));
        Assert.Equal(HttpStatusCode.Redirect, updatedCart.StatusCode);
        var mobileCart = await mobile.GetFromJsonAsync<JsonElement>("/api/v1/cart");
        Assert.Equal(2, mobileCart.GetProperty("items")[0].GetProperty("quantity").GetInt32());

        var noCsrf = await browser.PostAsync("/Cart/Remove", new FormUrlEncodedContent(new Dictionary<string, string> { ["variantId"] = variantId.ToString() }));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        await c.ExecuteAsync("""
            UPDATE TAI_KHOAN tk JOIN KHACH_HANG kh ON kh.ma_tai_khoan=tk.ma_tai_khoan
            SET tk.trang_thai='bi_khoa' WHERE kh.ma_khach_hang=@customerId
            """, new { customerId = token.CustomerId });
        var lockedCart = await browser.GetAsync("/Cart");
        Assert.Equal(HttpStatusCode.Redirect, lockedCart.StatusCode);
        Assert.Equal("/Account/Login", lockedCart.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Customer/Profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await mobile.GetAsync("/api/v1/me")).StatusCode);
        await browser.GetAsync("/Account/Logout");
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Customer/Profile")).StatusCode);
    }

    [MySqlFact]
    public async Task AdminWeb_DemoLoginStillWorksAndCustomerIsRejected()
    {
        using var admin = new MvcHostFactory<AdminAccount>();
        using var browser = admin.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Home")).StatusCode);
        var antiForgery = await LoginFormAsync(browser);
        var login = await browser.PostAsync("/Account/Login", Form(antiForgery, ("username", "admin"), ("password", "123456")));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        await AssertHtmlAsync(await browser.GetAsync("/Home"));
        await browser.GetAsync("/Account/Logout");

        using var api = new CustomerApiFactory();
        using var mobile = api.CreateClient();
        var username = "mvcusr_" + Guid.NewGuid().ToString("N")[..18];
        var registration = new RegisterRequest(username, username + "@example.test", "0900000001", "mvc-test-password");
        Assert.Equal(HttpStatusCode.Created, (await mobile.PostAsJsonAsync("/api/v1/auth/register", registration)).StatusCode);
        antiForgery = await LoginFormAsync(browser);
        var rejected = await browser.PostAsync("/Account/Login", Form(antiForgery, ("username", username), ("password", registration.Password)));
        var html = await AssertHtmlAsync(rejected);
        Assert.Contains("login-error", html);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Home")).StatusCode);
    }

    static async Task<string> AssertHtmlAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
        return html;
    }

    static async Task<string> LoginFormAsync(HttpClient client) => AntiForgery(await AssertHtmlAsync(await client.GetAsync("/Account/Login")));
    static string AntiForgery(string html)
    {
        var match = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(match.Success, "The real MVC form must emit an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    static FormUrlEncodedContent Form(string token, params (string Name, string Value)[] values)
        => new(values.Append(("__RequestVerificationToken", token)).Select(v => new KeyValuePair<string, string>(v.Item1, v.Item2)));
}

internal sealed class MvcHostFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint> where TEntryPoint : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("SPORTSHOP_TEST_MYSQL") ?? "";
        if (!new MySqlConnectionStringBuilder(connectionString).Database.Contains("test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MVC write integration tests require a dedicated test database.");
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "SportShop.slnx"))) repo = repo.Parent;
        if (repo is null) throw new InvalidOperationException("Could not locate the repository for Razor views.");
        builder.UseContentRoot(Path.Combine(repo.FullName, "Frontend", typeof(TEntryPoint).Assembly.GetName().Name!));
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["Paths:Images"] = Path.Combine(repo.FullName, "Backend", "Images"),
            ["Paths:Uploads"] = Path.Combine(repo.FullName, "Frontend", "Uploads"),
            ["Commerce:LegacyCartPath"] = Path.Combine(repo.FullName, "Backend", "TestResults", "no-legacy-carts.json")
        }));
    }

    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
        BaseAddress = new Uri("https://localhost")
    });
}
