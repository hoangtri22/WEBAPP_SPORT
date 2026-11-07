using SportShop.Api.Auth;
using Xunit;

namespace SportShop.Api.Tests;

// Test ticket, state và tách tên của đăng nhập Google.
public sealed class GoogleLoginTests
{
    const string Verifier = "verifier-cua-app-dang-nhap-google-du-do-dai-43-ky-tu";

    // Đồng hồ giả để thử hết hạn.
    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        // Trả thời gian giả.
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // Kiểm tra ticket chỉ dùng được một lần với verifier đúng.
    [Fact]
    public void TicketWorksOnceWithMatchingVerifier()
    {
        var tickets = new GoogleLoginTickets(new Clock());
        var ticket = tickets.NewTicket(7, GoogleLoginTickets.Challenge(Verifier));
        Assert.Equal(7, tickets.TakeTicket(ticket, Verifier));
        Assert.Null(tickets.TakeTicket(ticket, Verifier));
    }

    // Kiểm tra verifier sai làm ticket mất hiệu lực.
    [Fact]
    public void WrongVerifierBurnsTicket()
    {
        var tickets = new GoogleLoginTickets(new Clock());
        var ticket = tickets.NewTicket(7, GoogleLoginTickets.Challenge(Verifier));
        Assert.Null(tickets.TakeTicket(ticket, "verifier-khac"));
        Assert.Null(tickets.TakeTicket(ticket, Verifier));
    }

    // Kiểm tra ticket và state hết hạn.
    [Fact]
    public void TicketAndStateExpire()
    {
        var clock = new Clock();
        var tickets = new GoogleLoginTickets(clock);
        var ticket = tickets.NewTicket(7, GoogleLoginTickets.Challenge(Verifier));
        var state = tickets.NewState("challenge");
        clock.Now = clock.Now.AddMinutes(11);
        Assert.Null(tickets.TakeTicket(ticket, Verifier));
        Assert.Null(tickets.TakeState(state));
    }

    // Kiểm tra state chỉ dùng được một lần.
    [Fact]
    public void StateWorksOnce()
    {
        var tickets = new GoogleLoginTickets(new Clock());
        var state = tickets.NewState("challenge");
        Assert.Equal("challenge", tickets.TakeState(state));
        Assert.Null(tickets.TakeState(state));
    }

    // Kiểm tra tên đăng nhập tạo ra hợp lệ.
    [Theory]
    [InlineData("nguyen.van-a+shop@gmail.com")]
    [InlineData("a.very.long.address.that.goes.on.and.on.and.on@gmail.com")]
    [InlineData("...@gmail.com")]
    public void GeneratedUsernameIsValid(string email)
        => Assert.Matches("^[a-zA-Z0-9_]{3,30}$", GoogleAccountStore.NewUsername(email));

    // Kiểm tra tách tên theo dữ liệu Google.
    [Fact]
    public void NameUsesGoogleParts()
    {
        var user = new GoogleUser("1", "minh@gmail.com", true, "Minh", "Nguyễn Văn", "Nguyễn Văn Minh");
        Assert.Equal(("Nguyễn Văn", "Minh"), GoogleAccountStore.SplitName(user));
    }

    // Kiểm tra lấy tên từ họ tên đầy đủ, rồi đến email.
    [Fact]
    public void NameFallsBackToFullNameThenEmail()
    {
        Assert.Equal(("Nguyễn Văn", "Minh"), GoogleAccountStore.SplitName(new GoogleUser("1", "m@gmail.com", true, null, null, "Nguyễn Văn Minh")));
        Assert.Equal(("", "minh"), GoogleAccountStore.SplitName(new GoogleUser("1", "minh@gmail.com", true, null, null, null)));
    }
}
