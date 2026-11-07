using System.Net;
using System.Net.Http.Json;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

// Test quên mật khẩu.
public sealed class ForgotPasswordTests
{
    // Kiểm tra gửi mã xong chuyển sang bước hai.
    [Fact]
    public async Task SendingCodeMovesToSecondStep()
    {
        var api = new FakeApi();
        var vm = new ForgotPasswordViewModel(api) { Email = " minh@example.com " };
        vm.SendCodeCommand.Execute(null);
        await api.Sent.Task;
        Assert.Equal("minh@example.com", api.Email);
        Assert.True(vm.CodeSent);
    }

    // Kiểm tra email sai không được gửi.
    [Fact]
    public void InvalidEmailIsNotSent()
    {
        var api = new FakeApi();
        var vm = new ForgotPasswordViewModel(api) { Email = "sai-email" };
        vm.SendCodeCommand.Execute(null);
        Assert.Null(api.Email);
        Assert.False(vm.CodeSent);
        Assert.True(vm.HasError);
    }

    // Kiểm tra dữ liệu đặt lại sai không được gửi.
    [Theory]
    [InlineData("123", "moi123", "moi123", "6 chữ số")]
    [InlineData("123456", "ngan", "ngan", "6 ký tự")]
    [InlineData("123456", "moi123", "khac12", "xác nhận")]
    public void InvalidResetIsNotSent(string code, string next, string confirm, string expected)
    {
        var api = new FakeApi();
        var vm = new ForgotPasswordViewModel(api) { Code = code, NewPassword = next, ConfirmPassword = confirm };
        vm.ResetCommand.Execute(null);
        Assert.Null(api.Reset);
        Assert.Contains(expected, vm.ErrorMessage);
    }

    // Kiểm tra mã sai hiện thông báo của API và ở lại trang.
    [Fact]
    public void WrongCodeShowsApiMessageAndStaysOnPage()
    {
        var api = new FakeApi { Failure = new AuthException("Mã xác nhận không đúng hoặc đã hết hạn.") };
        var vm = new ForgotPasswordViewModel(api) { Email = "minh@example.com", Code = "123456", NewPassword = "moi123", ConfirmPassword = "moi123" };
        var completed = false;
        vm.Completed += () => completed = true;
        vm.ResetCommand.Execute(null);
        Assert.False(completed);
        Assert.Equal("Mã xác nhận không đúng hoặc đã hết hạn.", vm.ErrorMessage);
    }

    // Kiểm tra mã đúng thì hoàn tất và xóa mật khẩu đã nhập.
    [Fact]
    public async Task CorrectCodeCompletesAndClearsPasswords()
    {
        var api = new FakeApi();
        var vm = new ForgotPasswordViewModel(api) { Email = "minh@example.com", Code = "123456", NewPassword = "moi123", ConfirmPassword = "moi123" };
        var completed = new TaskCompletionSource();
        vm.Completed += completed.SetResult;
        vm.ResetCommand.Execute(null);
        await completed.Task;
        Assert.Equal(new ResetPasswordRequest("minh@example.com", "123456", "moi123"), api.Reset);
        Assert.Equal("", vm.NewPassword);
    }

    // Kiểm tra API đọc được title lỗi.
    [Fact]
    public async Task ApiReadsProblemTitle()
    {
        var api = new PasswordResetApi(new HttpClient(new Server()) { BaseAddress = new Uri("http://localhost:5080/") });
        var error = await Assert.ThrowsAsync<AuthException>(() => api.ResetAsync(new ResetPasswordRequest("minh@example.com", "123456", "moi123"), default));
        Assert.Equal("Mã xác nhận không đúng hoặc đã hết hạn.", error.Message);
    }

    // HttpMessageHandler giả trả lỗi.
    sealed class Server : HttpMessageHandler
    {
        // Trả phản hồi giả.
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { title = "Mã xác nhận không đúng hoặc đã hết hạn." }) });
    }

    // IPasswordResetApi giả ghi lại lần gọi.
    sealed class FakeApi : IPasswordResetApi
    {
        public string? Email;
        public ResetPasswordRequest? Reset;
        public AuthException? Failure;
        public readonly TaskCompletionSource Sent = new();

        // Ghi lại email hoặc ném lỗi đã cài.
        public Task SendCodeAsync(string email, CancellationToken cancellationToken)
        {
            Email = email;
            Sent.TrySetResult();
            return Task.CompletedTask;
        }

        // Ghi lại yêu cầu hoặc ném lỗi đã cài.
        public Task ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
        {
            if (Failure is not null) return Task.FromException(Failure);
            Reset = request;
            return Task.CompletedTask;
        }
    }
}
