using System.Net;
using System.Net.Http.Json;
using SportShop.Contracts.Commerce;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

public sealed class CartBadgeTests
{
    [Fact]
    public void BadgeUsesTotalQuantityAndNotNumberOfLinesAndClearsForNextSession()
    {
        var badge = new CartBadgeState();
        var changed = new List<string?>();
        badge.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        badge.Update(Cart());

        Assert.Equal(7, badge.Count);
        Assert.Equal("7", badge.DisplayCount);
        Assert.True(badge.HasItems);
        Assert.Contains(nameof(CartBadgeState.Count), changed);
        Assert.Contains(nameof(CartBadgeState.HasItems), changed);
        Assert.Contains(nameof(CartBadgeState.DisplayCount), changed);

        badge.Update(Cart() with { TotalQuantity = 120 });
        Assert.Equal(120, badge.Count);
        Assert.Equal("99+", badge.DisplayCount);

        badge.Clear();
        Assert.Equal(0, badge.Count);
        Assert.Equal("0", badge.DisplayCount);
        Assert.False(badge.HasItems);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("add")]
    [InlineData("set")]
    [InlineData("remove")]
    public async Task SuccessfulCartResponsesPublishServerQuantity(string operation)
    {
        var badge = new CartBadgeState();
        using var http = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(Cart()) });
        var api = new CommerceApi(http, new GuestSession(), cartBadge: badge);

        var cart = operation switch
        {
            "add" => await api.AddCartItemAsync(11, 3),
            "set" => await api.SetCartItemQuantityAsync(11, 3),
            "remove" => await api.RemoveCartItemAsync(11),
            _ => await api.GetCartAsync()
        };

        Assert.Equal(cart.TotalQuantity, badge.Count);
        Assert.Equal(7, badge.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckoutAndQuoteSynchronizeTheCurrentCart(bool quote)
    {
        var badge = new CartBadgeState();
        using var http = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new CheckoutDto(Cart(), new ReceiverDto("", "", ""), [], [],
                0, 0, 700_000m, true))
        });
        var api = new CommerceApi(http, new GuestSession(), cartBadge: badge);

        if (quote) await api.QuoteCheckoutAsync(null);
        else await api.GetCheckoutAsync();

        Assert.Equal(7, badge.Count);
    }

    [Fact]
    public async Task RejectedAddKeepsExistingQuantity()
    {
        var badge = new CartBadgeState();
        badge.Update(Cart());
        using var http = Client(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        { Content = JsonContent.Create(new { title = "Sản phẩm không còn đủ hàng." }) });
        var api = new CommerceApi(http, new GuestSession(), cartBadge: badge);

        await Assert.ThrowsAsync<CommerceException>(() => api.AddCartItemAsync(11, 3));

        Assert.Equal(7, badge.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearAndConfirmedOrderResetTheBadgeOnlyAfterSuccess(bool order)
    {
        var badge = new CartBadgeState();
        badge.Update(Cart());
        using var http = Client(_ => order
            ? new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(Order()) }
            : new HttpResponseMessage(HttpStatusCode.NoContent));
        var api = new CommerceApi(http, new GuestSession(), cartBadge: badge);

        if (order) await api.PlaceOrderAsync(new PlaceOrderRequest("QA", "0901234567", "QA address", 1, null), "qa-order");
        else await api.ClearCartAsync();

        Assert.False(badge.HasItems);
        Assert.Equal(0, badge.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedClearOrOrderDoesNotEraseTheBadge(bool order)
    {
        var badge = new CartBadgeState();
        badge.Update(Cart());
        using var http = Client(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        { Content = JsonContent.Create(new { title = "Máy chủ chưa sẵn sàng." }) });
        var api = new CommerceApi(http, new GuestSession(), cartBadge: badge);

        await Assert.ThrowsAsync<CommerceException>(() => order
            ? api.PlaceOrderAsync(new PlaceOrderRequest("QA", "0901234567", "QA address", 1, null), "qa-order")
            : api.ClearCartAsync());

        Assert.Equal(7, badge.Count);
    }

    private static CartDto Cart() => new(
        [new CartItemDto(11, 1, "Áo", "M", "Đen", null, 100_000m, 3, 20, 300_000m, true),
         new CartItemDto(12, 2, "Quần", "L", "Xanh", null, 100_000m, 4, 20, 400_000m, true)],
        7, 700_000m);

    private static OrderDto Order() => new(1, "QA01", DateTimeOffset.UtcNow, "cho_xac_nhan", "Chờ xác nhận",
        "Chưa thanh toán", "COD", new ReceiverDto("QA", "0901234567", "QA address"), null,
        null, null, 700_000m, 0, 0, 700_000m, null, true, []);

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(new Server(response)) { BaseAddress = new Uri("http://localhost:5080/") };

    private sealed class Server(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }

    private sealed class GuestSession : ISessionManager
    {
        public bool IsSignedIn => false;
        public event Action? Expired { add { } remove { } }
        public Task<bool> RestoreAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public Task StartAsync(TokenResponse tokens) => Task.CompletedTask;
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<string?> RenewAsync(string rejectedAccessToken, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task SignOutAsync() => Task.CompletedTask;
    }
}
