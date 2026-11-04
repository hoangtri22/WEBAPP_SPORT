using System.Net;
using System.Net.Http.Json;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

public sealed class AuthTests
{
    static readonly TokenResponse Tokens = new("Bearer", "access", "refresh", 900, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1);

    [Fact]
    public async Task LoginSavesTokensAndEntersApp()
    {
        var api = new FakeApi();
        var session = new FakeSession();
        var vm = new AuthViewModel(api, session, new FakeDevice()) { Username = " minhanh ", Password = "123456" };
        var entered = new TaskCompletionSource();
        vm.Entered += entered.SetResult;
        vm.SubmitCommand.Execute(null);
        await entered.Task;
        Assert.Equal("minhanh", api.Login?.Username);
        Assert.True(session.IsSignedIn);
        Assert.Equal("", vm.Password);
    }

    [Fact]
    public async Task RegisterValidatesBeforeCallingApi()
    {
        var api = new FakeApi();
        var vm = new AuthViewModel(api, new FakeSession(), new FakeDevice());
        vm.ShowRegisterCommand.Execute(null);
        vm.Username = "minhanh"; vm.Email = "sai-email"; vm.Phone = "0912345678"; vm.Password = "123456";
        vm.SubmitCommand.Execute(null);
        await Task.Yield();
        Assert.Equal("Email chưa đúng định dạng.", vm.ErrorMessage);
        Assert.Null(api.Register);
    }

    [Fact]
    public async Task GuestStillEntersWhenDeviceCannotBeSent()
    {
        var api = new FakeApi { Failure = new AuthException("offline") };
        var vm = new AuthViewModel(api, new FakeSession(), new FakeDevice());
        var entered = new TaskCompletionSource();
        vm.Entered += entered.SetResult;
        vm.GuestCommand.Execute(null);
        await entered.Task;
        Assert.Equal("Điện thoại Minh", api.Guest?.DeviceName);
    }

    [Fact]
    public async Task RejectedRefreshClearsSavedSession()
    {
        var session = new FakeSession { Refresh = "old" };
        var vm = new AuthViewModel(new FakeApi { Failure = new AuthException("hết hạn", rejected: true) }, session, new FakeDevice());
        await vm.RestoreAsync();
        Assert.Null(session.Refresh);
    }

    [Fact]
    public async Task ApiProblemTitleBecomesLoginError()
    {
        using var http = new HttpClient(new Stub(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = JsonContent.Create(new { title = "Thông tin đăng nhập không đúng." })
        })) { BaseAddress = new Uri("http://localhost:5080/") };
        var error = await Assert.ThrowsAsync<AuthException>(() => new AuthApi(http).LoginAsync(new LoginRequest("a", "b"), default));
        Assert.Equal("Thông tin đăng nhập không đúng.", error.Message);
        Assert.True(error.Rejected);
    }

    [Fact]
    public async Task GuestDeviceIsPostedToApi()
    {
        Uri? requested = null;
        using var http = new HttpClient(new Stub(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        })) { BaseAddress = new Uri("http://localhost:5080/") };
        await new AuthApi(http).RegisterGuestAsync(new FakeDevice().Current(), default);
        Assert.Equal("/api/v1/guest-devices", requested?.AbsolutePath);
    }

    sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    sealed class FakeApi : IAuthApi
    {
        public LoginRequest? Login;
        public RegisterRequest? Register;
        public GuestDeviceRequest? Guest;
        public AuthException? Failure;

        public Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        {
            Login = request;
            return Failure is null ? Task.FromResult(Tokens) : Task.FromException<TokenResponse>(Failure);
        }

        public Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
        {
            Register = request;
            return Task.FromResult(Tokens);
        }

        public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
            => Failure is null ? Task.FromResult(Tokens) : Task.FromException<TokenResponse>(Failure);

        public Task LogoutAsync(string accessToken, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RegisterGuestAsync(GuestDeviceRequest device, CancellationToken cancellationToken)
        {
            Guest = device;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    sealed class FakeSession : ISessionStore
    {
        public string? Refresh;
        public bool IsSignedIn { get; private set; }
        public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>("access");
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult(Refresh);
        public Task SaveAsync(TokenResponse tokens) { IsSignedIn = true; return Task.CompletedTask; }
        public void Clear() { IsSignedIn = false; Refresh = null; }
    }

    sealed class FakeDevice : IDeviceIdentity
    {
        public GuestDeviceRequest Current() => new("abcd1234", "Điện thoại Minh", "Samsung SM-A52", "Android", "14");
    }
}
