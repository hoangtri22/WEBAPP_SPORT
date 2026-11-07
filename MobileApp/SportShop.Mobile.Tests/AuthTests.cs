using System.Net;
using System.Net.Http.Json;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

// Test đăng nhập, đăng ký và vào bằng tư cách khách.
public sealed class AuthTests
{
    static readonly TokenResponse Tokens = new("Bearer", "access", "refresh", 900, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1);

    // Kiểm tra đăng nhập lưu token và vào app.
    [Fact]
    public async Task LoginSavesTokensAndEntersApp()
    {
        var api = new FakeApi();
        var session = new SessionManager(api, new FakeSession());
        var vm = new AuthViewModel(api, session, new FakeDevice()) { Username = " minhanh ", Password = "123456" };
        var entered = new TaskCompletionSource();
        vm.Entered += entered.SetResult;
        vm.SubmitCommand.Execute(null);
        await entered.Task;
        Assert.Equal("minhanh", api.Login?.Username);
        Assert.True(session.IsSignedIn);
        Assert.Equal("", vm.Password);
    }

    // Kiểm tra đăng ký được kiểm tra dữ liệu trước khi gọi API.
    [Fact]
    public async Task RegisterValidatesBeforeCallingApi()
    {
        var api = new FakeApi();
        var vm = new AuthViewModel(api, new SessionManager(api, new FakeSession()), new FakeDevice());
        vm.ShowRegisterCommand.Execute(null);
        vm.Username = "minhanh"; vm.Email = "sai-email"; vm.Phone = "0912345678"; vm.Password = "123456";
        vm.SubmitCommand.Execute(null);
        await Task.Yield();
        Assert.Equal("Email chưa đúng định dạng.", vm.ErrorMessage);
        Assert.Null(api.Register);
    }

    // Kiểm tra khách vẫn vào được app khi không gửi được thiết bị.
    [Fact]
    public async Task GuestStillEntersWhenDeviceCannotBeSent()
    {
        var api = new FakeApi { Failure = new AuthException("offline") };
        var vm = new AuthViewModel(api, new SessionManager(api, new FakeSession()), new FakeDevice());
        var entered = new TaskCompletionSource();
        vm.Entered += entered.SetResult;
        vm.GuestCommand.Execute(null);
        await entered.Task;
        Assert.Equal("Điện thoại Minh", api.Guest?.DeviceName);
    }

    // Kiểm tra refresh bị từ chối thì xóa phiên đã lưu.
    [Fact]
    public async Task RejectedRefreshClearsSavedSession()
    {
        var session = new FakeSession { Refresh = "old" };
        var api = new FakeApi { Failure = new AuthException("hết hạn", rejected: true) };
        var vm = new AuthViewModel(api, new SessionManager(api, session), new FakeDevice());
        await vm.RestoreAsync();
        Assert.Null(session.Refresh);
    }

    // Kiểm tra title lỗi của API thành thông báo đăng nhập.
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

    // Kiểm tra thông tin thiết bị được gửi lên API.
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

    // HttpMessageHandler giả trả phản hồi theo hàm respond.
    sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        // Trả phản hồi giả.
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    // IAuthApi giả ghi lại lần gọi.
    sealed class FakeApi : IAuthApi
    {
        public LoginRequest? Login;
        public RegisterRequest? Register;
        public GuestDeviceRequest? Guest;
        public AuthException? Failure;

        // Trả token giả hoặc ném lỗi đã cài.
        public Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        {
            Login = request;
            return Failure is null ? Task.FromResult(Tokens) : Task.FromException<TokenResponse>(Failure);
        }

        // Trả token giả và ghi lại yêu cầu đăng ký.
        public Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
        {
            Register = request;
            return Task.FromResult(Tokens);
        }

        // Trả token giả khi làm mới.
        public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
            => Failure is null ? Task.FromResult(Tokens) : Task.FromException<TokenResponse>(Failure);

        // Không làm gì khi đăng xuất.
        public Task LogoutAsync(string accessToken, CancellationToken cancellationToken) => Task.CompletedTask;

        // Ghi lại thiết bị hoặc ném lỗi đã cài.
        public Task RegisterGuestAsync(GuestDeviceRequest device, CancellationToken cancellationToken)
        {
            Guest = device;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    // ISessionStore giả giữ token trong bộ nhớ.
    sealed class FakeSession : ISessionStore
    {
        public string? Refresh;
        // Trả access token giả.
        public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>("access");
        // Trả refresh token giả.
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult(Refresh);
        // Trả không có hạn token.
        public Task<DateTimeOffset?> GetAccessExpiryAsync() => Task.FromResult<DateTimeOffset?>(null);
        // Không lưu gì.
        public Task SaveAsync(TokenResponse tokens) => Task.CompletedTask;
        // Xóa refresh token giả.
        public void Clear() => Refresh = null;
    }

    // IDeviceIdentity giả.
    sealed class FakeDevice : IDeviceIdentity
    {
        // Trả thiết bị mẫu.
        public GuestDeviceRequest Current() => new("abcd1234", "Điện thoại Minh", "Samsung SM-A52", "Android", "14");
    }
}
