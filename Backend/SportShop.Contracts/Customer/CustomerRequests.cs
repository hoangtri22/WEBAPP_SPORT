using System.ComponentModel.DataAnnotations;

namespace SportShop.Contracts.Customer;

// Yêu cầu đăng nhập.
public sealed record LoginRequest(
    [Required, StringLength(100)] string Username,
    [Required, StringLength(128)] string Password);

// Yêu cầu đăng ký tài khoản.
public sealed record RegisterRequest(
    [Required, StringLength(30, MinimumLength = 3), RegularExpression(@"^[a-zA-Z0-9_]+$")] string Username,
    [Required, EmailAddress, StringLength(100)] string Email,
    [Required, RegularExpression(@"^0\d{9}$")] string Phone,
    [Required, StringLength(128, MinimumLength = 6)] string Password);

// Thông tin thiết bị khách gửi lên khi vào app.
public sealed record GuestDeviceRequest(
    [Required, RegularExpression(@"^[a-zA-Z0-9-]{8,64}$")] string DeviceKey,
    [Required, StringLength(100)] string DeviceName,
    [Required, StringLength(100)] string Model,
    [Required, StringLength(30)] string Platform,
    [Required, StringLength(30)] string OsVersion);

// Yêu cầu làm mới token.
public sealed record RefreshRequest([Required, StringLength(128)] string RefreshToken);

// Bộ token trả về sau khi đăng nhập hoặc làm mới.
public sealed record TokenResponse(string TokenType, string AccessToken, string RefreshToken,
    int ExpiresIn, DateTimeOffset AccessTokenExpiresAt, DateTimeOffset RefreshTokenExpiresAt, int CustomerId);

// Yêu cầu cập nhật hồ sơ.
public sealed record UpdateProfileRequest(
    [Required, StringLength(101)] string FullName,
    [Required, EmailAddress, StringLength(100)] string Email,
    [Required, RegularExpression(@"^0\d{9}$")] string Phone,
    DateTime? Birth,
    [Required, RegularExpression("^(Nam|Nữ|Khác)$")] string Gender,
    [Required, MaxLength(100)] string[] Interests);

// Yêu cầu đổi mật khẩu.
public sealed record ChangePasswordRequest(
    [Required, StringLength(128)] string CurrentPassword,
    [Required, StringLength(128, MinimumLength = 6)] string NewPassword);

// Yêu cầu gửi mã quên mật khẩu.
public sealed record ForgotPasswordRequest([Required, EmailAddress, StringLength(100)] string Email);

// Yêu cầu đặt lại mật khẩu bằng mã 6 số.
public sealed record ResetPasswordRequest(
    [Required, EmailAddress, StringLength(100)] string Email,
    [Required, RegularExpression(@"^[0-9]{6}$")] string Code,
    [Required, StringLength(128, MinimumLength = 6)] string NewPassword);

// Yêu cầu đổi ticket Google lấy token.
public sealed record GoogleExchangeRequest([Required, StringLength(64)] string Ticket, [Required, StringLength(128)] string Verifier);

// Yêu cầu gửi phản hồi sản phẩm.
public sealed record FeedbackRequest(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(1, 5)] int Stars,
    [Required, StringLength(1000)] string Content,
    [Range(1, int.MaxValue)] int? OrderId = null);

// Câu trả lời cho một câu hỏi khảo sát.
public sealed record SurveyAnswerRequest(
    [Range(1, int.MaxValue)] int QuestionId,
    [Required, MaxLength(100)] List<int> Options,
    [StringLength(500)] string? Text = null);

// Yêu cầu nộp khảo sát.
public sealed record SubmitSurveyRequest([Required, MaxLength(100)] List<SurveyAnswerRequest> Answers);

// Phản hồi của khách trả cho app.
public sealed record FeedbackResponse(int Id, int? ProductId, string? ProductName, int? Stars, string Content,
    DateTimeOffset SentAt, string Status, string? Reply);

// Khảo sát trong danh sách trả cho app.
public sealed record SurveyResponse(int Id, string Title, string Description, DateTimeOffset Deadline,
    string Status, DateTimeOffset? CompletedAt, long QuestionCount, bool IsOpen, bool Done);

// Hồ sơ khách trả cho app.
public sealed record CustomerProfileResponse(string FullName, string Initials, string Username, string Email, string Phone,
    string Birth, string Gender, string Tier, int Orders, int Points);
// Hồ sơ kèm sở thích của khách đang đăng nhập.
public sealed record CustomerMeResponse(int CustomerId, CustomerProfileResponse Profile, IReadOnlyCollection<string> Interests);
// Sản phẩm khách được phản hồi.
public sealed record EligibleProductResponse(int Id, string Name);
// Một lựa chọn của câu hỏi khảo sát.
public sealed record SurveyOptionResponse(int Id, string Text);
// Một câu hỏi khảo sát.
public sealed record SurveyQuestionResponse(int Id, string Title, string Type, bool Required, IReadOnlyList<SurveyOptionResponse> Options);
// Khảo sát kèm các câu hỏi.
public sealed record SurveyDetailResponse(SurveyResponse Survey, IReadOnlyList<SurveyQuestionResponse> Questions);
// Thông báo sau khi gửi phản hồi.
public sealed record FeedbackCreatedResponse(string Message);
