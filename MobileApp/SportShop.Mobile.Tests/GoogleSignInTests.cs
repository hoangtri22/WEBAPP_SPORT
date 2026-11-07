using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

// Test đăng nhập Google của app.
public sealed class GoogleSignInTests
{
    static readonly TokenResponse Tokens = new("Bearer", "access", "refresh", 900, DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddDays(30), 1);

    // Kiểm tra thành công thì tạo phiên và vào app.
    [Fact]
    public async Task SuccessStartsSessionAndEntersApp()
    {
        var session = new FakeSession();
        var vm = Auth(session, new FakeGoogle { Result = Tokens });
        var entered = new TaskCompletionSource();
        vm.Entered += entered.SetResult;
        vm.GoogleCommand.Execute(null);
        await entered.Task;
        Assert.Same(Tokens, session.Started);
    }

    // Kiểm tra hủy thì ở lại, không báo lỗi.
    [Fact]
    public void CancelStaysWithoutError()
    {
        var session = new FakeSession();
        var vm = Auth(session, new FakeGoogle());
        vm.GoogleCommand.Execute(null);
        Assert.Null(session.Started);
        Assert.False(vm.HasError);
        Assert.True(vm.IsIdle);
    }

    // Kiểm tra lỗi thì hiện thông báo.
    [Fact]
    public void FailureShowsMessage()
    {
        var vm = Auth(new FakeSession(), new FakeGoogle { Failure = new AuthException("Đăng nhập Google chưa được cấu hình trên máy chủ.") });
        vm.GoogleCommand.Execute(null);
        Assert.Equal("Đăng nhập Google chưa được cấu hình trên máy chủ.", vm.ErrorMessage);
    }

    // Kiểm tra nút tắt khi không có dịch vụ Google.
    [Fact]
    public void ButtonDisabledWithoutGoogleService()
        => Assert.False(new AuthViewModel(new NoAuthApi(), new FakeSession(), new FakeDevice()).GoogleCommand.CanExecute(null));

    // Tạo AuthViewModel với bản giả.
    static AuthViewModel Auth(ISessionManager session, IGoogleSignIn google) => new(new NoAuthApi(), session, new FakeDevice(), google);

    // IGoogleSignIn giả.
    sealed class FakeGoogle : IGoogleSignIn
    {
        public TokenResponse? Result;
        public AuthException? Failure;
        // Trả kết quả đã cài.
        public Task<TokenResponse?> SignInAsync(CancellationToken cancellationToken)
            => Failure is null ? Task.FromResult(Result) : Task.FromException<TokenResponse?>(Failure);
    }

    // ISessionManager giả ghi lại token.
    sealed class FakeSession : ISessionManager
    {
        public TokenResponse? Started;
        public bool IsSignedIn => Started is not null;
        public event Action? Expired { add { } remove { } }
        // Không có phiên để khôi phục.
        public Task<bool> RestoreAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        // Ghi lại token.
        public Task StartAsync(TokenResponse tokens) { Started = tokens; return Task.CompletedTask; }
        // Không có access token.
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        // Không làm mới được.
        public Task<string?> RenewAsync(string rejectedAccessToken, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        // Không làm gì khi đăng xuất.
        public Task SignOutAsync() => Task.CompletedTask;
    }

    // IAuthApi giả không dùng tới.
    sealed class NoAuthApi : IAuthApi
    {
        // Không dùng trong test này.
        public Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        // Không dùng trong test này.
        public Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        // Không dùng trong test này.
        public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        // Không làm gì khi đăng xuất.
        public Task LogoutAsync(string accessToken, CancellationToken cancellationToken) => Task.CompletedTask;
        // Không làm gì khi gửi thiết bị.
        public Task RegisterGuestAsync(GuestDeviceRequest device, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    // IDeviceIdentity giả.
    sealed class FakeDevice : IDeviceIdentity
    {
        // Trả thiết bị mẫu.
        public GuestDeviceRequest Current() => new("abcd1234", "Điện thoại Minh", "Samsung SM-A52", "Android", "14");
    }
}
