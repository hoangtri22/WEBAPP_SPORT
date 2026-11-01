using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SportShop.CustomerWeb.Services;
using Xunit;

namespace SportShop.Api.Tests;

public sealed class CommerceApiTests
{
    public static TheoryData<string, string> ProtectedEndpoints => new()
    {
        { "GET", "/api/v1/cart" }, { "POST", "/api/v1/cart/items" },
        { "PUT", "/api/v1/cart/items/1" }, { "DELETE", "/api/v1/cart/items/1" },
        { "DELETE", "/api/v1/cart" }, { "GET", "/api/v1/checkout" },
        { "POST", "/api/v1/checkout/quote" }, { "GET", "/api/v1/checkout/vouchers" },
        { "GET", "/api/v1/orders" }, { "GET", "/api/v1/orders/1" },
        { "POST", "/api/v1/orders" }, { "POST", "/api/v1/orders/1/cancel" }
    };

    [Theory, MemberData(nameof(ProtectedEndpoints))]
    public async Task CommerceRequiresCustomerAuthentication(string method, string path)
    {
        using var factory = new CatalogApiFactory();
        using var client = factory.Client();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);
        await CatalogApiTests.AssertProblem(response, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
    }

    [Theory, MemberData(nameof(ProtectedEndpoints))]
    public async Task StaffRoleCannotUseCustomerCommerce(string method, string path)
    {
        using var factory = new CommerceValidationFactory("Admin");
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/cart/items", "{\"variantId\":1,\"quantity\":0}")]
    [InlineData("POST", "/api/v1/cart/items", "{\"variantId\":0,\"quantity\":1}")]
    [InlineData("POST", "/api/v1/cart/items", "{\"variantId\":1,\"quantity\":1000}")]
    [InlineData("PUT", "/api/v1/cart/items/1", "{\"quantity\":-1}")]
    [InlineData("DELETE", "/api/v1/cart/items/0", null)]
    [InlineData("GET", "/api/v1/orders?page=0", null)]
    [InlineData("GET", "/api/v1/orders?pageSize=101", null)]
    [InlineData("GET", "/api/v1/orders?status=unknown", null)]
    [InlineData("GET", "/api/v1/orders/0", null)]
    [InlineData("POST", "/api/v1/orders/0/cancel", null)]
    [InlineData("POST", "/api/v1/orders", "{\"name\":\"A\",\"phone\":\"0901234567\",\"address\":\"123 Street\",\"paymentMethodId\":1}")]
    public async Task InvalidRequestsFailBeforeDatabaseAccess(string method, string path, string? body)
    {
        using var factory = new CommerceValidationFactory("Customer");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null) request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        await CatalogApiTests.AssertProblem(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(499999, 30000)]
    [InlineData(500000, 0)]
    [InlineData(500001, 0)]
    public void SharedShippingPolicyMatchesWebCheckout(decimal subtotal, decimal expected)
        => Assert.Equal(expected, ShopStore.ShippingFor(subtotal));
}

internal sealed class CommerceValidationFactory(string role, string connectionString = "", int customerId = 1) : WebApplicationFactory<SportShop.Api.Controllers.ProductsController>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString, ["Media:PublicBaseUrl"] = "http://localhost:5080"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddSingleton(new CommerceTestRole(role, customerId));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "CommerceTest";
                options.DefaultChallengeScheme = "CommerceTest";
                options.DefaultForbidScheme = "CommerceTest";
            }).AddScheme<AuthenticationSchemeOptions, CommerceTestHandler>("CommerceTest", _ => { });
        });
    }
}

internal sealed record CommerceTestRole(string Value, int CustomerId);
internal sealed class CommerceTestHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, CommerceTestRole role) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier,role.CustomerId.ToString()), new(ClaimTypes.Role,role.Value)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

