using System.ComponentModel.DataAnnotations;

namespace SportShop.Contracts.Customer;

public sealed record LoginRequest(
    [Required, StringLength(100)] string Username,
    [Required, StringLength(128)] string Password);

public sealed record RegisterRequest(
    [Required, StringLength(30, MinimumLength = 3), RegularExpression(@"^[a-zA-Z0-9_]+$")] string Username,
    [Required, EmailAddress, StringLength(100)] string Email,
    [Required, RegularExpression(@"^0\d{9}$")] string Phone,
    [Required, StringLength(128, MinimumLength = 6)] string Password);

public sealed record RefreshRequest([Required, StringLength(128)] string RefreshToken);

public sealed record TokenResponse(string TokenType, string AccessToken, string RefreshToken,
    int ExpiresIn, DateTimeOffset AccessTokenExpiresAt, DateTimeOffset RefreshTokenExpiresAt, int CustomerId);

public sealed record UpdateProfileRequest(
    [Required, StringLength(101)] string FullName,
    [Required, EmailAddress, StringLength(100)] string Email,
    [Required, RegularExpression(@"^0\d{9}$")] string Phone,
    DateTime? Birth,
    [Required, RegularExpression("^(Nam|Nữ|Khác)$")] string Gender,
    [Required, MaxLength(100)] string[] Interests);

public sealed record ChangePasswordRequest(
    [Required, StringLength(128)] string CurrentPassword,
    [Required, StringLength(128, MinimumLength = 6)] string NewPassword);

public sealed record FeedbackRequest(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(1, 5)] int Stars,
    [Required, StringLength(1000)] string Content,
    [Range(1, int.MaxValue)] int? OrderId = null);

public sealed record SurveyAnswerRequest(
    [Range(1, int.MaxValue)] int QuestionId,
    [Required, MaxLength(100)] List<int> Options,
    [StringLength(500)] string? Text = null);

public sealed record SubmitSurveyRequest([Required, MaxLength(100)] List<SurveyAnswerRequest> Answers);

public sealed record FeedbackResponse(int Id, int? ProductId, string? ProductName, int? Stars, string Content,
    DateTimeOffset SentAt, string Status, string? Reply);

public sealed record SurveyResponse(int Id, string Title, string Description, DateTimeOffset Deadline,
    string Status, DateTimeOffset? CompletedAt, long QuestionCount, bool IsOpen, bool Done);

public sealed record CustomerProfileResponse(string FullName, string Initials, string Username, string Email, string Phone,
    string Birth, string Gender, string Tier, int Orders, int Points);
public sealed record CustomerMeResponse(int CustomerId, CustomerProfileResponse Profile, IReadOnlyCollection<string> Interests);
public sealed record EligibleProductResponse(int Id, string Name);
public sealed record SurveyOptionResponse(int Id, string Text);
public sealed record SurveyQuestionResponse(int Id, string Title, string Type, bool Required, IReadOnlyList<SurveyOptionResponse> Options);
public sealed record SurveyDetailResponse(SurveyResponse Survey, IReadOnlyList<SurveyQuestionResponse> Questions);
public sealed record FeedbackCreatedResponse(string Message);
