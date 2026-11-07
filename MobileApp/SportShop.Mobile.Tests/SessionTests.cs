using System.Net;
using System.Net.Http.Headers;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

// Test SessionManager và AuthorizedHandler.
public sealed class SessionTests
{
    // Tạo bộ token mẫu.
    static TokenResponse Tokens(string name, int minutes = 15) => new("Bearer", "access-" + name, "refresh-" + name, minutes * 60,
        DateTimeOffset.UtcNow.AddMinutes(minutes), DateTimeOffset.UtcNow.AddDays(30), 1);

    // Kiểm tra token còn hạn không bị làm mới.
    [Fact]
    public async Task ValidTokenIsNotRefreshed()
    {
        var store = new MemoryStore(Tokens("a"));
        var api = new CountingApi();
        var session = await RestoredAsync(api, store);
        Assert.Equal("access-a", await session.GetAccessTokenAsync(default));
        Assert.Empty(api.RefreshTokens);
    }

    // Kiểm tra token sắp hết hạn được làm mới và lưu lại.
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

    // Kiểm tra gọi đồng thời chỉ làm mới một lần.
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

    // Kiểm tra người gọi hủy không dừng lần làm mới đang chạy.
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

    // Kiểm tra refresh bị từ chối thì xóa phiên và báo hết hạn.
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

    // Kiểm tra lỗi mạng vẫn giữ phiên.
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

    // Kiểm tra mất mạng khi khôi phục vẫn vào được app.
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

    // Kiểm tra đăng xuất làm mới token hết hạn, thu hồi rồi xóa.
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

    // Kiểm tra đăng xuất khi mất mạng vẫn xóa token.
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

    // Kiểm tra phiên hết hạn đưa người dùng về đăng nhập kèm thông báo.
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

    // Kiểm tra handler gắn Bearer token.
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

    // Kiểm tra lỗi 401 thì làm mới và thử lại một lần.
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

    // Kiểm tra lỗi 401 mà refresh bị từ chối thì trả Unauthorized.
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

    // Kiểm tra request của khách không có header Authorization.
    [Fact]
    public async Task GuestRequestHasNoAuthorizationHeader()
    {
        var session = new SessionManager(new CountingApi(), new MemoryStore(null));
        var server = new FakeServer("access-a");
        using var http = Client(session, server);
        await http.GetAsync("api/v1/me");
        Assert.Null(server.LastAuthorization);
    }

    // Kiểm tra refresh lỗi mạng báo lỗi kết nối.
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

    // Tạo SessionManager đã khôi phục phiên.
    static async Task<SessionManager> RestoredAsync(IAuthApi api, ISessionStore store)
    {
        var session = new SessionManager(api, store);
        await session.RestoreAsync(default);
        return session;
    }

    // Tạo HttpClient dùng AuthorizedHandler và server giả.
    static HttpClient Client(ISessionManager session, HttpMessageHandler server)
        => new(new AuthorizedHandler(session) { InnerHandler = server }) { BaseAddress = new Uri("http://localhost:5080/") };

    // HttpMessageHandler giả chỉ chấp nhận một token.
    sealed class FakeServer(string acceptedToken) : HttpMessageHandler
    {
        public int Calls;
        public AuthenticationHeaderValue? LastAuthorization;

        // Trả 200 với token đúng, 401 với token sai.
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastAuthorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(LastAuthorization?.Parameter == acceptedToken ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));
        }
    }

    // IAuthApi giả đếm số lần làm mới và đăng xuất.
    sealed class CountingApi : IAuthApi
    {
        public readonly List<string> RefreshTokens = [];
        public TaskCompletionSource? Gate;
        public AuthException? Failure;
        public AuthException? LogoutFailure;
        public string? LoggedOut;

        // Đếm lần gọi rồi trả token mới hoặc ném lỗi đã cài.
        public async Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            if (Gate is not null) await Gate.Task;
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            RefreshTokens.Add(refreshToken);
            return Tokens(RefreshTokens.Count.ToString());
        }

        // Đếm lần đăng xuất.
        public Task LogoutAsync(string accessToken, CancellationToken cancellationToken)
        {
            LoggedOut = accessToken;
            return LogoutFailure is null ? Task.CompletedTask : Task.FromException(LogoutFailure);
        }

        // Không dùng trong test này.
        public Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        // Không dùng trong test này.
        public Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        // Không làm gì khi gửi thiết bị.
        public Task RegisterGuestAsync(GuestDeviceRequest device, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    // ISessionStore giả giữ token trong bộ nhớ.
    sealed class MemoryStore(TokenResponse? tokens) : ISessionStore
    {
        string? _access = tokens?.AccessToken;
        DateTimeOffset? _expiry = tokens?.AccessTokenExpiresAt;
        public string? Refresh { get; private set; } = tokens?.RefreshToken;

        // Đặt token thành hết hạn.
        public void Expire() => _expiry = DateTimeOffset.UtcNow.AddMinutes(-1);
        // Đọc access token.
        public Task<string?> GetAccessTokenAsync() => Task.FromResult(_access);
        // Đọc refresh token.
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult(Refresh);
        // Đọc thời điểm hết hạn.
        public Task<DateTimeOffset?> GetAccessExpiryAsync() => Task.FromResult(_expiry);

        // Lưu bộ token.
        public Task SaveAsync(TokenResponse tokens)
        {
            (_access, Refresh, _expiry) = (tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAt);
            return Task.CompletedTask;
        }

        // Xóa token.
        public void Clear() => (_access, Refresh, _expiry) = (null, null, null);
    }

    // IDeviceIdentity giả.
    sealed class Device : IDeviceIdentity
    {
        // Trả thiết bị mẫu.
        public GuestDeviceRequest Current() => new("abcd1234", "Điện thoại Minh", "Samsung SM-A52", "Android", "14");
    }
}
