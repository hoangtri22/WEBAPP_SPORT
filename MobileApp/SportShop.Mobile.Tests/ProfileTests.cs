using System.Net;
using System.Net.Http.Json;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

// Test hồ sơ, đổi mật khẩu và xử lý phiên của API.
public sealed class ProfileTests
{
    // Tạo hồ sơ mẫu.
    static CustomerMeResponse Me(string gender = "Nam", string birth = "2000-05-20", string phone = "0901234567") => new(1,
        new CustomerProfileResponse("Nguyễn Minh", "NM", "minh", "minh@example.com", phone, birth, gender, "Bạc", 3, 120), ["Chạy bộ"]);

    // Kiểm tra tải hồ sơ điền vào form.
    [Fact]
    public async Task LoadFillsFormFromApi()
    {
        var vm = new ProfileViewModel(new FakeAccount { Profile = Me() });
        await vm.LoadAsync();
        Assert.True(vm.IsLoaded);
        Assert.Equal("Nguyễn Minh", vm.FullName);
        Assert.Equal(new DateTime(2000, 5, 20), vm.Birth);
        Assert.Equal("Nam", vm.Gender);
    }

    // Kiểm tra chưa tải xong thì không lưu được.
    [Fact]
    public async Task CannotSaveBeforeProfileLoads()
    {
        var vm = new ProfileViewModel(new FakeAccount());
        await vm.LoadAsync();
        Assert.False(vm.IsLoaded);
        Assert.True(vm.HasError);
        Assert.True(vm.CanRetry);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    // Kiểm tra lưu giữ sở thích và hiện thông báo.
    [Fact]
    public async Task SaveKeepsInterestsAndShowsStatus()
    {
        var account = new FakeAccount { Profile = Me() };
        var vm = new ProfileViewModel(account);
        await vm.LoadAsync();
        vm.FullName = "  Trần Minh  ";
        vm.SaveCommand.Execute(null);
        await account.Saved.Task;
        Assert.Equal("Trần Minh", account.Update!.FullName);
        Assert.Equal(["Chạy bộ"], account.Update.Interests);
        Assert.Equal(new DateTime(2000, 5, 20), account.Update.Birth);
    }

    // Kiểm tra hồ sơ sai không được gửi.
    [Theory]
    [InlineData("", "minh@example.com", "0901234567", "Nam")]
    [InlineData("Minh", "sai-email", "0901234567", "Nam")]
    [InlineData("Minh", "minh@example.com", "123", "Nam")]
    [InlineData("Minh", "minh@example.com", "0901234567", null)]
    public async Task InvalidProfileIsNotSent(string name, string email, string phone, string? gender)
    {
        var account = new FakeAccount { Profile = Me() };
        var vm = new ProfileViewModel(account);
        await vm.LoadAsync();
        (vm.FullName, vm.Email, vm.Phone, vm.Gender) = (name, email, phone, gender);
        vm.SaveCommand.Execute(null);
        Assert.Null(account.Update);
        Assert.True(vm.HasError);
    }

    // Kiểm tra giới tính lạ phải chọn lại trước khi lưu.
    [Fact]
    public async Task UnknownGenderMustBeChosenBeforeSaving()
    {
        var vm = new ProfileViewModel(new FakeAccount { Profile = Me(gender: "") });
        await vm.LoadAsync();
        Assert.Null(vm.Gender);
    }

    // Kiểm tra đổi mật khẩu sai không được gửi.
    [Theory]
    [InlineData("", "moi123", "moi123", "hiện tại")]
    [InlineData("cu1234", "ngan", "ngan", "6 ký tự")]
    [InlineData("cu1234", "moi123", "khac12", "xác nhận")]
    [InlineData("cu1234", "cu1234", "cu1234", "khác mật khẩu")]
    public void InvalidPasswordChangeIsNotSent(string current, string next, string confirm, string expected)
    {
        var account = new FakeAccount();
        var vm = new ChangePasswordViewModel(account, Auth(new FakeSession()).Vm);
        (vm.CurrentPassword, vm.NewPassword, vm.ConfirmPassword) = (current, next, confirm);
        vm.SubmitCommand.Execute(null);
        Assert.Null(account.Password);
        Assert.Contains(expected, vm.ErrorMessage);
    }

    // Kiểm tra đổi mật khẩu xong thì rời phiên và yêu cầu đăng nhập lại.
    [Fact]
    public async Task PasswordChangeSignsOutAndAsksToLoginAgain()
    {
        var session = new FakeSession();
        var (auth, left) = Auth(session);
        var account = new FakeAccount();
        var vm = new ChangePasswordViewModel(account, auth);
        (vm.CurrentPassword, vm.NewPassword, vm.ConfirmPassword) = ("cu1234", "moi123", "moi123");
        vm.SubmitCommand.Execute(null);
        await left.Task;
        Assert.Equal(new ChangePasswordRequest("cu1234", "moi123"), account.Password);
        Assert.True(session.SignedOut);
        Assert.Contains("Đã đổi mật khẩu", auth.ErrorMessage);
        Assert.Equal("", vm.NewPassword);
    }

    // Kiểm tra sai mật khẩu hiện tại thì giữ phiên.
    [Fact]
    public void WrongCurrentPasswordKeepsSession()
    {
        var session = new FakeSession();
        var account = new FakeAccount { PasswordFailure = new AuthException("Mật khẩu hiện tại không đúng.") };
        var vm = new ChangePasswordViewModel(account, Auth(session).Vm);
        (vm.CurrentPassword, vm.NewPassword, vm.ConfirmPassword) = ("sai123", "moi123", "moi123");
        vm.SubmitCommand.Execute(null);
        Assert.False(session.SignedOut);
        Assert.Equal("Mật khẩu hiện tại không đúng.", vm.ErrorMessage);
    }

    // Kiểm tra API tài khoản đọc được title lỗi.
    [Fact]
    public async Task AccountApiReadsProblemTitle()
    {
        var api = new AccountApi(Client(HttpStatusCode.Conflict, new { title = "Email đã được sử dụng." }));
        var error = await Assert.ThrowsAsync<AuthException>(() => api.UpdateProfileAsync(
            new UpdateProfileRequest("Minh", "minh@example.com", "0901234567", null, "Nam", []), default));
        Assert.Equal("Email đã được sử dụng.", error.Message);
    }

    // Kiểm tra CommerceApi làm mới token qua SessionManager.
    [Fact]
    public async Task CommerceRenewsThroughSessionManager()
    {
        var session = new FakeSession { Renewed = "moi" };
        var server = new TokenServer("moi");
        var api = new CommerceApi(new HttpClient(server) { BaseAddress = new Uri("http://localhost:5080/") }, session);
        await api.GetCartAsync();
        Assert.Equal("cu", session.Rejected);
        Assert.Equal(["cu", "moi"], server.Tokens);
    }

    // Kiểm tra CommerceApi dừng khi không làm mới được phiên.
    [Fact]
    public async Task CommerceStopsWhenSessionCannotBeRenewed()
    {
        var api = new CommerceApi(new HttpClient(new TokenServer("moi")) { BaseAddress = new Uri("http://localhost:5080/") }, new FakeSession());
        var error = await Assert.ThrowsAsync<CommerceException>(() => api.GetCartAsync());
        Assert.Contains("hết hạn", error.Message);
    }

    // Tạo AuthViewModel và tín hiệu báo rời phiên.
    static (AuthViewModel Vm, TaskCompletionSource Left) Auth(ISessionManager session)
    {
        var left = new TaskCompletionSource();
        var vm = new AuthViewModel(new NoAuthApi(), session, new FakeDevice());
        vm.Left += left.SetResult;
        return (vm, left);
    }

    // Tạo HttpClient dùng FixedServer.
    static HttpClient Client(HttpStatusCode status, object body)
        => new(new FixedServer(status, body)) { BaseAddress = new Uri("http://localhost:5080/") };

    // HttpMessageHandler giả trả cố định một phản hồi.
    sealed class FixedServer(HttpStatusCode status, object body) : HttpMessageHandler
    {
        // Trả phản hồi cố định.
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status) { Content = JsonContent.Create(body) });
    }

    // Trả 401 cho mọi token khác token được chấp nhận và ghi lại các token đã nhận.
    sealed class TokenServer(string accepted) : HttpMessageHandler
    {
        public readonly List<string?> Tokens = [];

        // Trả phản hồi giả.
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = request.Headers.Authorization?.Parameter;
            Tokens.Add(token);
            return Task.FromResult(token == accepted
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { items = Array.Empty<object>(), totalQuantity = 0 }) }
                : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }
    }

    // IAccountApi giả ghi lại lần gọi.
    sealed class FakeAccount : IAccountApi
    {
        public CustomerMeResponse? Profile;
        public UpdateProfileRequest? Update;
        public ChangePasswordRequest? Password;
        public AuthException? PasswordFailure;
        public readonly TaskCompletionSource Saved = new();

        // Trả hồ sơ mẫu.
        public Task<CustomerMeResponse?> GetProfileAsync(CancellationToken cancellationToken) => Task.FromResult(Profile);

        // Ghi lại yêu cầu cập nhật hoặc ném lỗi đã cài.
        public Task<CustomerMeResponse> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
        {
            Update = request;
            Saved.TrySetResult();
            return Task.FromResult(Profile!);
        }

        // Ghi lại yêu cầu đổi mật khẩu hoặc ném lỗi đã cài.
        public Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            if (PasswordFailure is not null) return Task.FromException(PasswordFailure);
            Password = request;
            return Task.CompletedTask;
        }
    }

    // ISessionManager giả ghi lại lần gọi.
    sealed class FakeSession : ISessionManager
    {
        public bool SignedOut;
        public string? Renewed;
        public string? Rejected;
        public bool IsSignedIn => !SignedOut;
        public event Action? Expired { add { } remove { } }
        // Luôn có phiên để khôi phục.
        public Task<bool> RestoreAsync(CancellationToken cancellationToken) => Task.FromResult(true);
        // Không làm gì khi bắt đầu phiên.
        public Task StartAsync(TokenResponse tokens) => Task.CompletedTask;
        // Trả access token giả.
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult<string?>("cu");

        // Trả token giả khi làm mới.
        public Task<string?> RenewAsync(string rejectedAccessToken, CancellationToken cancellationToken)
        {
            Rejected = rejectedAccessToken;
            return Task.FromResult(Renewed);
        }

        // Ghi lại lần đăng xuất.
        public Task SignOutAsync()
        {
            SignedOut = true;
            return Task.CompletedTask;
        }
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
