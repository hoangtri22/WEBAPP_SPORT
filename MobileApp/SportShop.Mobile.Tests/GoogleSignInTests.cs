using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

public sealed class GoogleSignInTests
{
    static readonly TokenResponse Tokens = new("Bearer", "access", "refresh", 900, DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddDays(30), 1);

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

    [Fact]
    public void FailureShowsMessage()
    {
        var vm = Auth(new FakeSession(), new FakeGoogle { Failure = new AuthException("Đăng nhập Google chưa được cấu hình trên máy chủ.") });
        vm.GoogleCommand.Execute(null);
        Assert.Equal("Đăng nhập Google chưa được cấu hình trên máy chủ.", vm.ErrorMessage);
    }

    [Fact]
    public void ButtonDisabledWithoutGoogleService()
        => Assert.False(new AuthViewModel(new NoAuthApi(), new FakeSession(), new FakeDevice()).GoogleCommand.CanExecute(null));

    static AuthViewModel Auth(ISessionManager session, IGoogleSignIn google) => new(new NoAuthApi(), session, new FakeDevice(), google);

    sealed class FakeGoogle : IGoogleSignIn
    {
        public TokenResponse? Result;
        public AuthException? Failure;
        public Task<TokenResponse?> SignInAsync(CancellationToken cancellationToken)
            => Failure is null ? Task.FromResult(Result) : Task.FromException<TokenResponse?>(Failure);
    }

    sealed class FakeSession : ISessionManager
    {
        public TokenResponse? Started;
        public bool IsSignedIn => Started is not null;
        public event Action? Expired { add { } remove { } }
        public Task<bool> RestoreAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public Task StartAsync(TokenResponse tokens) { Started = tokens; return Task.CompletedTask; }
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<string?> RenewAsync(string rejectedAccessToken, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task SignOutAsync() => Task.CompletedTask;
    }

    sealed class NoAuthApi : IAuthApi
    {
        public Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LogoutAsync(string accessToken, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RegisterGuestAsync(GuestDeviceRequest device, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    sealed class FakeDevice : IDeviceIdentity
    {
        public GuestDeviceRequest Current() => new("abcd1234", "Điện thoại Minh", "Samsung SM-A52", "Android", "14");
    }
}
