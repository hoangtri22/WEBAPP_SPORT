using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;
using SportShop.Contracts.Customer;
using SportShop.CustomerWeb.Services;
using Xunit;

namespace SportShop.Api.Tests;

[Trait("Category", "MySql")]
public sealed class MySqlCustomerTests
{
    [MySqlFact]
    public async Task Tokens_RotateRejectExpiredReplayRevokesAndLogoutPersists()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClient();
        var token = await RegisterAsync(client);
        Authenticate(client, token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
        var refreshed = await (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(token.RefreshToken))).Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(refreshed);
        Assert.NotEqual(token.AccessToken, refreshed.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Authenticate(client, refreshed);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
        factory.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(token.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(refreshed.RefreshToken))).StatusCode);
        token = await RegisterAsync(client);
        factory.Clock.Advance(TimeSpan.FromDays(31));
        Authenticate(client, token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(token.RefreshToken))).StatusCode);

        token = await RegisterAsync(client);
        Authenticate(client, token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(token.RefreshToken))).StatusCode);
    }

    [MySqlFact]
    public async Task ProfilePasswordAndDuplicateAccount_WorkWithSharedWebStore()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClient();
        var username = Username();
        var register = Registration(username);
        var token = await RegisterAsync(client, register);
        Authenticate(client, token);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/auth/register", register)).StatusCode);
        var profile = new UpdateProfileRequest("Nguyễn Kiểm Thử", username + "_updated@example.test", "0900000001", new DateTime(2000, 1, 1), "Khác", []);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/v1/me", profile)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/me", profile with { Birth = DateTime.Today.AddDays(1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/me", profile with { Interests = ["invalid-interest"] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/me/password", new ChangePasswordRequest("wrong-password", "new-test-password"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/me/password", new ChangePasswordRequest(register.Password, "new-test-password"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(token.RefreshToken))).StatusCode);
        var webStore = new CustomerStore(factory.Configuration);
        Assert.Equal(token.CustomerId, webStore.Login(username, "new-test-password").CustomerId);
        Assert.Null(webStore.Login(username, register.Password).CustomerId);
        await using var c = await factory.OpenAsync();
        var stored = await c.ExecuteScalarAsync<string>("SELECT mat_khau FROM TAI_KHOAN WHERE ten_dang_nhap=@username", new { username });
        Assert.NotNull(stored);
        Assert.True(PasswordStorage.IsEncoded(stored));
        Assert.DoesNotContain(register.Password, stored);
    }

    [MySqlFact]
    public async Task LockedDeletedAndEmployeeAccountsCannotUseCustomerTokens()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClient();
        var register = Registration(Username());
        var token = await RegisterAsync(client, register);
        Authenticate(client, token);
        await using var c = await factory.OpenAsync();
        await c.ExecuteAsync("UPDATE KHACH_HANG SET da_xoa=1 WHERE ma_khach_hang=@id", new { id = token.CustomerId });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Username, register.Password))).StatusCode);
        await c.ExecuteAsync("UPDATE KHACH_HANG SET da_xoa=0 WHERE ma_khach_hang=@id", new { id = token.CustomerId });
        await c.ExecuteAsync("UPDATE TAI_KHOAN tk JOIN KHACH_HANG kh ON kh.ma_tai_khoan=tk.ma_tai_khoan SET tk.trang_thai='bi_khoa' WHERE kh.ma_khach_hang=@id", new { id = token.CustomerId });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        var employee = Username();
        await c.ExecuteAsync("""
            INSERT INTO TAI_KHOAN(ten_dang_nhap,email,mat_khau,ma_vai_tro) SELECT @employee,@email,@password,ma_vai_tro FROM VAI_TRO WHERE ten_vai_tro<>'Khách hàng' LIMIT 1
            """, new { employee, email = employee + "@example.test", password = PasswordStorage.Hash("staff-test-password") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(employee, "staff-test-password"))).StatusCode);
    }

    [MySqlFact]
    public async Task Login_FiveFailuresThrottleAndLegacyPasswordUpgrades()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClient();
        var register = Registration(Username());
        var token = await RegisterAsync(client, register);
        await using var c = await factory.OpenAsync();
        await c.ExecuteAsync("UPDATE TAI_KHOAN SET mat_khau=@password WHERE ten_dang_nhap=@username", new { password = register.Password, username = register.Username });
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Username, register.Password))).StatusCode);
        Assert.True(PasswordStorage.IsEncoded((await c.ExecuteScalarAsync<string>("SELECT mat_khau FROM TAI_KHOAN WHERE ten_dang_nhap=@username", new { username = register.Username }))!));
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Username, "bad-password"))).StatusCode);
        var throttled = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Username, register.Password));
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.NotNull(throttled.Headers.RetryAfter);
    }

    [MySqlFact]
    public async Task Surveys_ValidateOwnershipRequiredAnswersDeadlineAndSingleSubmission()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClient();
        var token = await RegisterAsync(client);
        Authenticate(client, token);
        await using var c = await factory.OpenAsync();
        var surveyId = await c.ExecuteScalarAsync<int>("""
            INSERT INTO KHAO_SAT(tieu_de,ngay_bat_dau,ngay_ket_thuc,trang_thai,ma_nhan_vien)
            SELECT 'Mobile API integration test',DATE_SUB(NOW(),INTERVAL 1 DAY),DATE_ADD(NOW(),INTERVAL 1 DAY),'da_gui',ma_nhan_vien FROM NHAN_VIEN LIMIT 1;
            SELECT LAST_INSERT_ID();
            """);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/me/surveys/{surveyId}")).StatusCode);
        await c.ExecuteAsync("INSERT INTO KHAO_SAT_KHACH_HANG(ma_khao_sat,ma_khach_hang) VALUES(@surveyId,@customerId)", new { surveyId, customerId = token.CustomerId });
        var questionId = await c.ExecuteScalarAsync<int>("INSERT INTO CAU_HOI_KHAO_SAT(ma_khao_sat,noi_dung,loai_cau_hoi,bat_buoc) VALUES(@surveyId,'Test choice','mot_lua_chon',1); SELECT LAST_INSERT_ID();", new { surveyId });
        var optionId = await c.ExecuteScalarAsync<int>("INSERT INTO LUA_CHON_CAU_HOI(ma_cau_hoi,noi_dung) VALUES(@questionId,'Choice'); SELECT LAST_INSERT_ID();", new { questionId });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/me/surveys/{surveyId}")).StatusCode);
        var assigned = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/surveys?page=1&pageSize=100");
        Assert.Contains(assigned.GetProperty("items").EnumerateArray(), survey => survey.GetProperty("id").GetInt32() == surveyId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/me/surveys?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/v1/me/surveys/{surveyId}/responses", new SubmitSurveyRequest([]))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/v1/me/surveys/{surveyId}/responses", new SubmitSurveyRequest([new(questionId, [int.MaxValue])]))).StatusCode);
        var valid = new SubmitSurveyRequest([new(questionId, [optionId])]);
        var first = client.PostAsJsonAsync($"/api/v1/me/surveys/{surveyId}/responses", valid);
        var second = client.PostAsJsonAsync($"/api/v1/me/surveys/{surveyId}/responses", valid);
        var statuses = (await Task.WhenAll(first, second)).Select(r => r.StatusCode).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }, statuses);
        Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TRA_LOI_KHAO_SAT WHERE ma_khao_sat=@surveyId AND ma_khach_hang=@id", new { surveyId, id = token.CustomerId }));
        await c.ExecuteAsync("UPDATE KHAO_SAT_KHACH_HANG SET trang_thai='chua_lam' WHERE ma_khao_sat=@surveyId AND ma_khach_hang=@id", new { surveyId, id = token.CustomerId });
        await c.ExecuteAsync("UPDATE KHAO_SAT SET ngay_bat_dau=DATE_SUB(NOW(),INTERVAL 3 DAY),ngay_ket_thuc=DATE_SUB(NOW(),INTERVAL 2 DAY) WHERE ma_khao_sat=@surveyId", new { surveyId });
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/v1/me/surveys/{surveyId}/responses", valid)).StatusCode);
        Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TRA_LOI_KHAO_SAT WHERE ma_khao_sat=@surveyId AND ma_khach_hang=@id", new { surveyId, id = token.CustomerId }));
        var other = await RegisterAsync(client);
        Authenticate(client, other);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/me/surveys/{surveyId}")).StatusCode);
    }

    [MySqlFact]
    public async Task Feedback_RequiresDeliveredOwnedPurchaseAndReturnsStoreReply()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClient();
        var token = await RegisterAsync(client);
        Authenticate(client, token);
        await using var c = await factory.OpenAsync();
        var productId = await c.ExecuteScalarAsync<int>("SELECT sp.ma_san_pham FROM SAN_PHAM sp JOIN BIEN_THE_SP bt ON bt.ma_san_pham=sp.ma_san_pham WHERE sp.trang_thai='hien' LIMIT 1");
        var request = new FeedbackRequest(productId, 5, "Integration review");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/me/feedback", request)).StatusCode);
        var orderId = await c.ExecuteScalarAsync<int>("""
            INSERT INTO DON_HANG(ma_khach_hang,ten_nguoi_nhan,sdt_nguoi_nhan,dia_chi_giao_hang,ma_phuong_thuc_thanh_toan,trang_thai_don_hang,trang_thai_thanh_toan)
            SELECT @customerId,'Test receiver','0900000001','Test address',ma_phuong_thuc_thanh_toan,'da_giao','da_thanh_toan' FROM PHUONG_THUC_THANH_TOAN LIMIT 1;
            SELECT LAST_INSERT_ID();
            """, new { customerId = token.CustomerId });
        await c.ExecuteAsync("INSERT INTO CHI_TIET_DON_HANG(ma_don_hang,ma_bien_the,so_luong,don_gia,thanh_tien) SELECT @orderId,ma_bien_the,1,gia_ban,gia_ban FROM BIEN_THE_SP WHERE ma_san_pham=@productId LIMIT 1", new { orderId, productId });
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/me/feedback", request with { OrderId = orderId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/me/feedback", request with { OrderId = int.MaxValue })).StatusCode);
        var feedbackId = await c.ExecuteScalarAsync<int>("SELECT MAX(ma_phan_hoi) FROM PHAN_HOI WHERE ma_khach_hang=@id", new { id = token.CustomerId });
        await c.ExecuteAsync("INSERT INTO TRA_LOI_PHAN_HOI(ma_phan_hoi,ma_nhan_vien,noi_dung) SELECT @feedbackId,ma_nhan_vien,'Store reply' FROM NHAN_VIEN LIMIT 1", new { feedbackId });
        var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/feedback?pageSize=1");
        Assert.Equal(1, json.GetProperty("totalItems").GetInt32());
        Assert.Equal("Store reply", json.GetProperty("items")[0].GetProperty("reply").GetString());
        var eligible = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/feedback/eligible-products");
        Assert.Contains(eligible.EnumerateArray(), p => p.GetProperty("id").GetInt32() == productId);
        var other = await RegisterAsync(client);
        Authenticate(client, other);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/me/feedback", request with { OrderId = orderId })).StatusCode);
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/feedback");
        Assert.Equal(0, empty.GetProperty("totalItems").GetInt32());
    }

    static string Username() => "apitest_" + Guid.NewGuid().ToString("N")[..18];
    static RegisterRequest Registration(string username) => new(username, username + "@example.test", "0900000001", "test-password-123");
    static async Task<TokenResponse> RegisterAsync(HttpClient client, RegisterRequest? request = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request ?? Registration(Username()));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }
    static void Authenticate(HttpClient client, TokenResponse response) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", response.AccessToken);
}

internal sealed class CustomerApiFactory : WebApplicationFactory<SportShop.Api.Controllers.ProductsController>
{
    public MutableClock Clock { get; } = new();
    public IConfiguration Configuration { get; } = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Default"] = Environment.GetEnvironmentVariable("SPORTSHOP_TEST_MYSQL"),
        ["Media:PublicBaseUrl"] = "http://localhost:5080"
    }).Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var database = new MySqlConnectionStringBuilder(Configuration.GetConnectionString("Default") ?? "").Database;
        if (!database.Contains("test", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Write integration tests require a dedicated database whose name contains 'test'.");
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddConfiguration(Configuration));
        builder.ConfigureTestServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(Clock); });
    }

    public async Task<MySqlConnection> OpenAsync()
    {
        var c = new MySqlConnection(Configuration.GetConnectionString("Default"));
        await c.OpenAsync();
        return c;
    }
}

internal sealed class MutableClock : TimeProvider
{
    DateTimeOffset _now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan elapsed) => _now += elapsed;
}
