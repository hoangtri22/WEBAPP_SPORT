using System.Net;
using System.Net.Http.Headers;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

public sealed class SessionTests
{
    static TokenResponse Tokens(string name, int minutes = 15) => new("Bearer", "access-" + name, "refresh-" + name, minutes * 60,
        DateTimeOffset.UtcNow.AddMinutes(minutes), DateTimeOffset.UtcNow.AddDays(30), 1);

    [Fact]
    public async Task ValidTokenIsNotRefreshed()
    {
        var store = new MemoryStore(Tokens("a"));
        var api = new CountingApi();
        var session = await RestoredAsync(api, store);
        Assert.Equal("access-a", await session.GetAccessTokenAsync(default));
        Assert.Empty(api.RefreshTokens);
    }

    [Fact]
    public async Task TokenCloseToExpiryIsRefreshedAndSaved()
    {
        var store = new MemoryStore(Tokens("a", minutes: 0));
        var api = new CountingApi();
        var session = await RestoredAsync(api, store);
        Assert.Equal("access-1", await session.GetAccessTokenAsync(default));
        Assert.Equal("refresh-1", store.Refresh);
        Assert.Equal(["refresh-a"], api.RefreshTokens);
    }

    [Fact]
    public async Task ConcurrentCallersShareOneRefresh()
    {
        var store = new MemoryStore(Tokens("a"));
        var api = new CountingApi { Gate = new TaskCompletionSource() };
        var session = await RestoredAsync(api, store);
        store.Expire();
        var calls = Enumerable.Range(0, 8).Select(_ => session.GetAccessTokenAsync(default)).ToArray();
        api.Gate.SetResult();
        var results = await Task.WhenAll(calls);
        Assert.All(results, token => Assert.Equal("access-1", token));
        Assert.Single(api.RefreshTokens);
    }

    [Fact]
    public async Task CallerCancellationDoesNotAbortRunningRefresh()
    {
        var store = new MemoryStore(Tokens("a"));
        var api = new CountingApi { Gate = new TaskCompletionSource() };
        var session = await RestoredAsync(api, store);
        store.Expire();
        using var cancel = new CancellationTokenSource();
        var call = session.GetAccessTokenAsync(cancel.Token);
        cancel.Cancel();
        api.Gate.SetResult();
        await call;
        Assert.Equal("refresh-1", store.Refresh);
    }

    [Fact]
    public async Task RejectedRefreshClearsSessionAndRaisesExpired()
    {
        var store = new MemoryStore(Tokens("a"));
        var api = new CountingApi();
        var session = await RestoredAsync(api, store);
        store.Expire();
        api.Failure = new AuthException("hết hạn", rejected: true);
        var expired = 0;
        session.Expired += () => expired++;
        Assert.Null(await session.GetAccessTokenAsync(default));
        Assert.False(session.IsSignedIn);
        Assert.Null(store.Refresh);
        Assert.Equal(1, expired);
    }

    [Fact]
    public async Task NetworkFailureKeepsSession()
    {
        var store = new MemoryStore(Tokens("a"));
        var api = new CountingApi();
        var session = await RestoredAsync(api, store);
        store.Expire();
        api.Failure = new AuthException("mất mạng");
        await Assert.ThrowsAsync<AuthException>(() => session.GetAccessTokenAsync(default));
        Assert.True(session.IsSignedIn);
        Assert.Equal("refresh-a", store.Refresh);
    }

    [Fact]
    public async Task RestoreOfflineStillEntersApp()
    {
        var store = new MemoryStore(Tokens("a", minutes: 0));
        var api = new CountingApi { Failure = new AuthException("mất mạng") };
        var vm = new AuthViewModel(api, new SessionManager(api, store), new Device());
        var entered = false;
        vm.Entered += () => entered = true;
        await vm.RestoreAsync();
        Assert.True(entered);
        Assert.Equal("refresh-a", store.Refresh);
    }

    [Fact]
    public async Task SignOutRefreshesExpiredTokenThenRevokesAndClears()
    {
        var store = new MemoryStore(Tokens("a"));
        var api = new CountingApi();
        var session = await RestoredAsync(api, store);
        store.Expire();
        await session.SignOutAsync();
        Assert.Equal("access-1", api.LoggedOut);
        Assert.False(session.IsSignedIn);
        Assert.Null(store.Refresh);
    }

    [Fact]
    public async Task SignOutClearsTokensWhenOffline()
    {
        var store = new MemoryStore(Tokens("a"));
        var api = new CountingApi { LogoutFailure = new AuthException("mất mạng") };
        var session = await RestoredAsync(api, store);
        await session.SignOutAsync();
        Assert.False(session.IsSignedIn);
        Assert.Null(store.Refresh);
    }

    [Fact]
    public async Task ExpiredSessionReturnsUserToLoginWithMessage()
    {
        var store = new MemoryStore(Tokens("a"));
        var api = new CountingApi();
        var session = new SessionManager(api, store);
        var vm = new AuthViewModel(api, session, new Device());
        var left = 0;
        vm.Left += () => left++;
        await vm.RestoreAsync();
        store.Expire();
        api.Failure = new AuthException("hết hạn", rejected: true);
        await session.GetAccessTokenAsync(default);
        Assert.Equal(1, left);
        Assert.Equal("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", vm.ErrorMessage);
    }

    [Fact]
    public async Task HandlerAttachesBearerToken()
    {
        var session = await RestoredAsync(new CountingApi(), new MemoryStore(Tokens("a")));
        var server = new FakeServer("access-a");
        using var http = Client(session, server);
        var response = await http.GetAsync("api/v1/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, server.Calls);
    }

    [Fact]
    public async Task Handler401RefreshesAndRetriesOnce()
    {
        var api = new CountingApi();
        var session = await RestoredAsync(api, new MemoryStore(Tokens("a")));
        var server = new FakeServer("access-1");
        using var http = Client(session, server);
        var response = await http.GetAsync("api/v1/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, server.Calls);
        Assert.Single(api.RefreshTokens);
    }

    [Fact]
    public async Task Handler401WithRejectedRefreshReturnsUnauthorized()
    {
        var api = new CountingApi { Failure = new AuthException("hết hạn", rejected: true) };
        var session = await RestoredAsync(api, new MemoryStore(Tokens("a")));
        var server = new FakeServer("không-khớp");
        using var http = Client(session, server);
        var response = await http.GetAsync("api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, server.Calls);
        Assert.False(session.IsSignedIn);
    }

    [Fact]
    public async Task GuestRequestHasNoAuthorizationHeader()
    {
        var session = new SessionManager(new CountingApi(), new MemoryStore(null));
        var server = new FakeServer("access-a");
        using var http = Client(session, server);
        await http.GetAsync("api/v1/me");
        Assert.Null(server.LastAuthorization);
    }

    [Fact]
    public async Task HandlerReportsOfflineRefreshAsConnectionError()
    {
        var store = new MemoryStore(Tokens("a"));
        var api = new CountingApi();
        var session = await RestoredAsync(api, store);
        store.Expire();
        api.Failure = new AuthException("mất mạng");
        using var http = Client(session, new FakeServer("access-a"));
        Assert.Null(await new AccountApi(http).GetProfileAsync(default));
        Assert.True(session.IsSignedIn);
    }

    static async Task<SessionManager> RestoredAsync(IAuthApi api, ISessionStore store)
    {
        var session = new SessionManager(api, store);
        await session.RestoreAsync(default);
        return session;
    }

    static HttpClient Client(ISessionManager session, HttpMessageHandler server)
        => new(new AuthorizedHandler(session) { InnerHandler = server }) { BaseAddress = new Uri("http://localhost:5080/") };

    sealed class FakeServer(string acceptedToken) : HttpMessageHandler
    {
        public int Calls;
        public AuthenticationHeaderValue? LastAuthorization;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastAuthorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(LastAuthorization?.Parameter == acceptedToken ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));
        }
    }

    sealed class CountingApi : IAuthApi
    {
        public readonly List<string> RefreshTokens = [];
        public TaskCompletionSource? Gate;
        public AuthException? Failure;
        public AuthException? LogoutFailure;
        public string? LoggedOut;

        public async Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            if (Gate is not null) await Gate.Task;
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            RefreshTokens.Add(refreshToken);
            return Tokens(RefreshTokens.Count.ToString());
        }

        public Task LogoutAsync(string accessToken, CancellationToken cancellationToken)
        {
            LoggedOut = accessToken;
            return LogoutFailure is null ? Task.CompletedTask : Task.FromException(LogoutFailure);
        }

        public Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RegisterGuestAsync(GuestDeviceRequest device, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    sealed class MemoryStore(TokenResponse? tokens) : ISessionStore
    {
        string? _access = tokens?.AccessToken;
        DateTimeOffset? _expiry = tokens?.AccessTokenExpiresAt;
        public string? Refresh { get; private set; } = tokens?.RefreshToken;

        public void Expire() => _expiry = DateTimeOffset.UtcNow.AddMinutes(-1);
        public Task<string?> GetAccessTokenAsync() => Task.FromResult(_access);
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult(Refresh);
        public Task<DateTimeOffset?> GetAccessExpiryAsync() => Task.FromResult(_expiry);

        public Task SaveAsync(TokenResponse tokens)
        {
            (_access, Refresh, _expiry) = (tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAt);
            return Task.CompletedTask;
        }

        public void Clear() => (_access, Refresh, _expiry) = (null, null, null);
    }

    sealed class Device : IDeviceIdentity
    {
        public GuestDeviceRequest Current() => new("abcd1234", "Điện thoại Minh", "Samsung SM-A52", "Android", "14");
    }
}
