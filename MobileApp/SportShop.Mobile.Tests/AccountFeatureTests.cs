using System.Net;
using System.Net.Http.Json;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;
using Xunit;

namespace SportShop.Mobile.Tests;

// Test các chức năng tài khoản bổ sung cho giống web: phản hồi, khảo sát, sở thích trong hồ sơ, danh mục Trang chủ.
public sealed class AccountFeatureTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(7));

    // Kiểm tra lịch sử phản hồi gọi api/v1/me/feedback không kèm productId và khảo sát gọi đúng đường dẫn nộp bài.
    [Fact]
    public async Task ContentApiCallsFeedbackHistoryAndSurveyEndpoints()
    {
        var paths = new List<string>();
        var handler = new Handler(request =>
        {
            paths.Add(request.Method + " " + request.RequestUri!.PathAndQuery);
            return request.RequestUri!.AbsolutePath switch
            {
                "/api/v1/me/feedback" => Json(new PagedResponse<FeedbackResponse>([Feedback(5, "da_tra_loi", "Cảm ơn bạn")], 1, 20, 1)),
                "/api/v1/me/surveys" => Json(new PagedResponse<SurveyResponse>([Survey(2, done: false, open: true)], 1, 100, 1)),
                "/api/v1/me/surveys/2" => Json(new SurveyDetailResponse(Survey(2, false, true), [Question(10, "one", true)])),
                _ => new HttpResponseMessage(HttpStatusCode.NoContent),
            };
        });
        var api = new ContentApi(Client(handler), Client(handler));

        var history = await api.GetFeedbackHistoryAsync(1);
        var surveys = await api.GetSurveysAsync();
        var detail = await api.GetSurveyAsync(2);
        await api.SubmitSurveyAsync(2, new SubmitSurveyRequest([new SurveyAnswerRequest(10, [101])]));

        Assert.Equal("Cảm ơn bạn", Assert.Single(history.Items).Reply);
        Assert.Equal(2, Assert.Single(surveys).Id);
        Assert.Single(detail.Questions);
        Assert.Contains("GET /api/v1/me/feedback?page=1&pageSize=20", paths);
        Assert.Contains("POST /api/v1/me/surveys/2/responses", paths);
    }

    // Kiểm tra form phản hồi chặn khi chưa chọn sao như CustomerController.Feedback() và giữ nội dung đã nhập.
    [Fact]
    public async Task FeedbackRequiresStarsAndKeepsContent()
    {
        var api = new FakeContent { Eligible = [new(7, "Nike Pegasus")] };
        var vm = new FeedbackViewModel(api);
        await vm.LoadAsync();
        vm.Content = "Giày êm";

        Assert.False(await vm.SendAsync());
        Assert.Equal("Vui lòng chọn số sao đánh giá.", vm.ErrorMessage);
        Assert.Equal("Giày êm", vm.Content);
        Assert.Null(api.Sent);
    }

    // Kiểm tra gửi phản hồi thành công thì xóa form, tải lại lịch sử và đánh giá từ đơn hàng gửi kèm OrderId.
    [Fact]
    public async Task FeedbackFromOrderSendsOrderIdAndResetsForm()
    {
        var api = new FakeContent();
        var vm = new FeedbackViewModel(api);
        vm.UseOrder(33, "DH033", [new(7, "Nike Pegasus"), new(7, "Nike Pegasus"), new(8, "Tất chạy bộ")]);
        await vm.LoadAsync();
        Assert.Equal(2, vm.Products.Count);
        vm.SelectedProduct = vm.Products[1];
        vm.Stars = 4;
        vm.Content = "  Vừa chân  ";

        Assert.True(await vm.SendAsync());
        Assert.Equal(new FeedbackRequest(8, 4, "Vừa chân", 33), api.Sent);
        Assert.Equal("", vm.Content);
        Assert.Equal(0, vm.Stars);
        Assert.True(vm.HasStatus);
        Assert.Equal(0, api.HistoryCalls);
    }

    // Kiểm tra trạng thái phản hồi: có trả lời của cửa hàng thì hiện Đã trả lời.
    [Fact]
    public void FeedbackCardShowsAnsweredStatus()
    {
        Assert.Equal("Đã trả lời", new FeedbackCard(Feedback(1, "moi", "Đã xử lý")).StatusText);
        Assert.Equal("Chờ phản hồi", new FeedbackCard(Feedback(2, "moi", null)).StatusText);
        Assert.Equal("★★★★☆", new FeedbackCard(Feedback(3, "moi", null)).Stars);
    }

    // Kiểm tra danh sách khảo sát đếm khảo sát đang chờ và đã hoàn thành như phần tóm tắt của Surveys.cshtml.
    [Fact]
    public async Task SurveysCountPendingAndDone()
    {
        var vm = new SurveysViewModel(new FakeContent { Surveys = [Survey(1, false, true), Survey(2, true, false), Survey(3, false, false)] });
        await vm.LoadAsync();
        Assert.Equal(1, vm.PendingCount);
        Assert.Equal(1, vm.DoneCount);
        Assert.Equal("ĐÃ HẾT HẠN", vm.Surveys[2].StateText);
        Assert.False(vm.Surveys[2].CanAnswer);
    }

    // Kiểm tra form khảo sát không sang câu sau khi câu bắt buộc còn trống, câu một lựa chọn chỉ giữ một đáp án và nộp đúng câu trả lời.
    [Fact]
    public async Task SurveyFormValidatesRequiredAndSubmits()
    {
        var api = new FakeContent
        {
            Detail = new SurveyDetailResponse(Survey(4, false, true), [Question(10, "one", true), Question(11, "many", false), Question(12, "text", false)]),
        };
        var vm = new SurveyFormViewModel(api);
        await vm.LoadAsync(4);

        await vm.NextAsync();
        Assert.Equal(0, vm.Index);
        Assert.Equal("Vui lòng trả lời câu hỏi bắt buộc này.", vm.ErrorMessage);

        vm.Current!.Choose(vm.Current.Options[0]);
        vm.Current.Choose(vm.Current.Options[1]);
        Assert.Single(vm.Current.Options, o => o.IsSelected);
        await vm.NextAsync();
        Assert.Equal(1, vm.Index);
        await vm.NextAsync();
        vm.Current!.Text = " Rất tốt ";
        Assert.Equal("Gửi khảo sát", vm.NextText);
        await vm.NextAsync();

        Assert.True(vm.IsCompleted);
        Assert.Equal(2, api.Submitted!.Answers.Count);
        Assert.Equal([102], api.Submitted.Answers[0].Options);
        Assert.Equal("Rất tốt", api.Submitted.Answers[1].Text);
    }

    // Kiểm tra khảo sát đã làm thì không mở form.
    [Fact]
    public async Task SurveyFormRejectsCompletedSurvey()
    {
        var vm = new SurveyFormViewModel(new FakeContent { Detail = new SurveyDetailResponse(Survey(5, true, true), [Question(10, "one", true)]) });
        await vm.LoadAsync(5);
        Assert.False(vm.ShowForm);
        Assert.Equal("Bạn đã hoàn thành khảo sát này rồi.", vm.ErrorMessage);
    }

    // Kiểm tra hồ sơ hiện mọi sở thích từ GET api/v1/interests, đánh dấu sở thích đã chọn, lưu theo lựa chọn mới và tính % hoàn thiện như Profile.cshtml.
    [Fact]
    public async Task ProfileLoadsInterestOptionsAndSavesSelection()
    {
        var account = new FakeAccount
        {
            Profile = new(1, new CustomerProfileResponse("Nguyễn Minh", "NM", "minh", "minh@example.com", "0901234567", "", "Nam", "Bạc", 3, 120), ["Chạy bộ"]),
        };
        var vm = new ProfileViewModel(account, new FakeCatalog());
        await vm.LoadAsync();

        Assert.Equal(["Chạy bộ", "Bóng đá", "Gym"], vm.Interests.Select(i => i.Name));
        Assert.True(vm.Interests[0].IsSelected);
        Assert.Equal(83, vm.CompletionPercent);
        vm.ToggleInterest(vm.Interests[0]);
        vm.ToggleInterest(vm.Interests[2]);
        vm.SaveCommand.Execute(null);
        await account.Saved.Task;
        Assert.Equal(["Gym"], account.Update!.Interests);
    }

    // Kiểm tra thẻ danh mục Trang chủ hiện số sản phẩm và xoay vòng icon.
    [Fact]
    public void HomeCategoryShowsCountAndCyclesIcons()
    {
        var first = new HomeCategory(new CategoryDto(1, "Giày", 12), 0);
        var fifth = new HomeCategory(new CategoryDto(5, "Phụ kiện", 3), 4);
        Assert.Equal("12 sản phẩm", first.CountText);
        Assert.Equal(first.Icon, fifth.Icon);
    }

    static FeedbackResponse Feedback(int id, string status, string? reply) => new(id, 7, "Nike Pegasus", 4, "Êm chân", Now, status, reply);
    static SurveyResponse Survey(int id, bool done, bool open) => new(id, "Khảo sát " + id, "Mô tả", Now.AddDays(5), done ? "hoan_thanh" : "chua_lam",
        done ? Now : null, 3, open, done);
    static SurveyQuestionResponse Question(int id, string type, bool required) => new(id, "Câu " + id, type, required,
        type == "text" ? [] : [new(id * 10 + 1, "A"), new(id * 10 + 2, "B")]);
    static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://shop.test/") };
    static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    // HttpMessageHandler giả trả phản hồi theo hàm cài sẵn.
    sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }

    // IContentApi giả cho phản hồi và khảo sát, ghi lại yêu cầu đã gửi.
    sealed class FakeContent : IContentApi
    {
        public IReadOnlyList<EligibleProductResponse> Eligible = [];
        public IReadOnlyList<SurveyResponse> Surveys = [];
        public SurveyDetailResponse? Detail;
        public FeedbackRequest? Sent;
        public SubmitSurveyRequest? Submitted;
        public int HistoryCalls;

        public Task<IReadOnlyList<PromotionDto>> GetPromotionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PromotionDto>>([]);
        public Task<IReadOnlyList<ContentPageSummary>> GetPagesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentPageSummary>>([]);
        public Task<ContentPageDetail> GetPageAsync(string slug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductReviewsResponse> GetReviewsAsync(int productId, int page, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EligibleProductResponse>> GetEligibleProductsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Eligible);
        public Task<PagedResponse<FeedbackResponse>> GetMyFeedbackAsync(int productId, int page, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        // Ghi lại phản hồi đã gửi.
        public Task SendFeedbackAsync(FeedbackRequest request, CancellationToken cancellationToken = default) { Sent = request; return Task.CompletedTask; }
        // Đếm số lần tải lịch sử.
        public Task<PagedResponse<FeedbackResponse>> GetFeedbackHistoryAsync(int page, CancellationToken cancellationToken = default)
        {
            HistoryCalls++;
            return Task.FromResult(new PagedResponse<FeedbackResponse>([], page, 20, 0));
        }
        public Task<IReadOnlyList<SurveyResponse>> GetSurveysAsync(CancellationToken cancellationToken = default) => Task.FromResult(Surveys);
        public Task<SurveyDetailResponse> GetSurveyAsync(int surveyId, CancellationToken cancellationToken = default) => Task.FromResult(Detail!);
        // Ghi lại bài khảo sát đã nộp.
        public Task SubmitSurveyAsync(int surveyId, SubmitSurveyRequest request, CancellationToken cancellationToken = default) { Submitted = request; return Task.CompletedTask; }
    }

    // ICatalogApi giả chỉ trả danh sách sở thích.
    sealed class FakeCatalog : ICatalogApi
    {
        public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<InterestDto>> GetInterestsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InterestDto>>([new(1, "Chạy bộ"), new(2, "Bóng đá"), new(3, "Gym")]);
    }

    // IAccountApi giả ghi lại yêu cầu cập nhật hồ sơ.
    sealed class FakeAccount : IAccountApi
    {
        public CustomerMeResponse? Profile;
        public UpdateProfileRequest? Update;
        public readonly TaskCompletionSource Saved = new();
        public Task<CustomerMeResponse?> GetProfileAsync(CancellationToken cancellationToken) => Task.FromResult(Profile);
        // Ghi lại yêu cầu cập nhật và trả hồ sơ cũ.
        public Task<CustomerMeResponse> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
        {
            Update = request;
            Saved.TrySetResult();
            return Task.FromResult(Profile!);
        }
        public Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
