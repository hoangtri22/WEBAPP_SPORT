using System.Net;
using System.Net.Mail;

namespace SportShop.Api.Auth;

// Gửi mã đặt lại mật khẩu, tách interface để test thay bằng bản giả.
public interface IPasswordResetMailer
{
    Task SendAsync(string email, string code, CancellationToken cancellationToken);
}

/// <summary>Gửi OTP qua SMTP theo mục Smtp trong appsettings.Local.json. Chưa cấu hình thì bản Development ghi mã ra log để thử trên máy.</summary>
public sealed class SmtpPasswordResetMailer(IConfiguration configuration, IHostEnvironment environment,
    ILogger<SmtpPasswordResetMailer> logger) : IPasswordResetMailer
{
    // Gửi mã qua SMTP theo cấu hình mục Smtp trong appsettings.
    public async Task SendAsync(string email, string code, CancellationToken cancellationToken)
    {
        var smtp = configuration.GetSection("Smtp");
        var host = smtp["Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            if (!environment.IsDevelopment()) throw new InvalidOperationException("Smtp:Host chưa được cấu hình.");
            logger.LogWarning("Chưa cấu hình Smtp, mã OTP chỉ hiện ở log Development: {Code}", code);
            return;
        }
        var username = smtp["Username"];
        using var client = new SmtpClient(host, smtp.GetValue("Port", 587))
        {
            EnableSsl = smtp.GetValue("EnableSsl", true),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrEmpty(username) ? null : new NetworkCredential(username, smtp["Password"])
        };
        using var message = new MailMessage(new MailAddress(smtp["From"] ?? username!, smtp["FromName"] ?? "SportShop"), new MailAddress(email))
        {
            Subject = "Mã đặt lại mật khẩu SportShop",
            Body = $"Mã đặt lại mật khẩu của bạn là {code}. Mã có hiệu lực trong 10 phút.\n\nNếu bạn không yêu cầu, hãy bỏ qua email này."
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
