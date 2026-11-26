using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

// Một phản hồi đã gửi, giống một dòng bảng "Phản hồi đã gửi" của Views/Customer/Feedback.cshtml.
public sealed record FeedbackCard(FeedbackResponse Feedback)
{
    public string Code => "#" + Feedback.Id.ToString(CultureInfo.InvariantCulture);
    public string ProductName => string.IsNullOrWhiteSpace(Feedback.ProductName) ? "Phản hồi chung" : Feedback.ProductName!;
    public string Stars => Feedback.Stars is >= 1 and <= 5 ? new string('★', Feedback.Stars.Value) + new string('☆', 5 - Feedback.Stars.Value) : "";
    public bool HasStars => Stars.Length > 0;
    public string Content => Feedback.Content;
    public string SentAt => Feedback.SentAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm");
    public bool IsAnswered => !string.IsNullOrWhiteSpace(Feedback.Reply) || Feedback.Status == "da_tra_loi";
    public string StatusText => IsAnswered ? "Đã trả lời" : Feedback.Status == "dang_xu_ly" ? "Đang xử lý" : "Chờ phản hồi";
    public string? Reply => Feedback.Reply;
    public bool HasReply => !string.IsNullOrWhiteSpace(Reply);
}

// Màn hình Gửi phản hồi giống Views/Customer/Feedback.cshtml, và phần đánh giá đơn đã giao của Views/Customer/Orders.cshtml khi gọi UseOrder().
public sealed class FeedbackViewModel(IContentApi api) : ContentViewModel
{
    private static readonly string[] StarWords = ["Chọn số sao", "Rất tệ", "Chưa tốt", "Bình thường", "Hài lòng", "Rất hài lòng"];
    private IReadOnlyList<EligibleProductResponse>? _orderProducts;
    private EligibleProductResponse? _selectedProduct;
    private int _stars;
    private string _content = "";
    private int _page;
    private long _total;

    public ObservableCollection<EligibleProductResponse> Products { get; } = [];
    public ObservableCollection<FeedbackCard> History { get; } = [];
    public IReadOnlyList<int> StarValues { get; } = [1, 2, 3, 4, 5];
    public int? OrderId { get; private set; }
    public string? OrderCode { get; private set; }
    public bool IsOrderReview => OrderId is not null;
    public string Heading => IsOrderReview ? $"Đánh giá đơn {OrderCode}" : "Trải nghiệm của bạn thế nào?";
    public EligibleProductResponse? SelectedProduct { get => _selectedProduct; set { _selectedProduct = value; Notify(); Notify(nameof(CanSend)); } }
    public int Stars { get => _stars; set { _stars = Math.Clamp(value, 0, 5); Notify(); Notify(nameof(StarsText)); Notify(nameof(CanSend)); } }
    public string StarsText => _stars == 0 ? StarWords[0] : $"{_stars}/5 · {StarWords[_stars]}";
    public string Content { get => _content; set { _content = value ?? ""; Notify(); Notify(nameof(ContentCounter)); } }
    public string ContentCounter => $"{_content.Length}/1000";
    public bool HasProducts => Products.Count > 0;
    public bool NoProducts => HasLoaded && Products.Count == 0;
    public bool CanSend => IsIdle && SelectedProduct is not null;
    public bool HasMore => History.Count < _total;
    public bool ShowHistoryEmpty => HasLoaded && History.Count == 0;
    public string HistoryCount => $"{History.Count:00} phản hồi";
    public string? StatusMessage { get; private set; }
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    // Giới hạn sản phẩm theo một đơn đã giao, gửi kèm OrderId như form đánh giá trong Orders.cshtml.
    public void UseOrder(int orderId, string orderCode, IEnumerable<EligibleProductResponse> products)
    {
        OrderId = orderId;
        OrderCode = orderCode;
        _orderProducts = [.. products.DistinctBy(p => p.Id)];
        Changed();
    }

    // Tải sản phẩm được phản hồi (IContentApi.GetEligibleProductsAsync() hoặc sản phẩm của đơn) và lịch sử phản hồi trang đầu.
    public Task LoadAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        var products = _orderProducts ?? await api.GetEligibleProductsAsync(cancellationToken);
        Products.Clear();
        foreach (var product in products) Products.Add(product);
        if (SelectedProduct is null || Products.All(p => p.Id != SelectedProduct.Id)) SelectedProduct = Products.Count == 1 ? Products[0] : null;
        if (!IsOrderReview) ReplaceHistory(await api.GetFeedbackHistoryAsync(1, cancellationToken));
        HasLoaded = true;
    }, cancellationToken);

    // Tải trang lịch sử kế tiếp, bỏ mục trùng mã.
    public Task LoadMoreAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        if (!HasMore) return;
        var page = await api.GetFeedbackHistoryAsync(_page + 1, cancellationToken);
        foreach (var item in page.Items)
            if (History.All(h => h.Feedback.Id != item.Id)) History.Add(new FeedbackCard(item));
        _page = page.Page;
        _total = page.Items.Count == 0 ? History.Count : page.TotalItems;
    }, cancellationToken);

    // Kiểm tra như CustomerController.Feedback() (sản phẩm, 1–5 sao, 1–1000 ký tự) rồi gửi qua IContentApi.SendFeedbackAsync(); lỗi thì giữ nội dung.
    public async Task<bool> SendAsync(CancellationToken cancellationToken = default)
    {
        StatusMessage = null;
        var sent = await RunAsync(async () =>
        {
            var content = Content.Trim();
            if (SelectedProduct is null) throw new ContentException("Vui lòng chọn sản phẩm.");
            if (Stars is < 1 or > 5) throw new ContentException("Vui lòng chọn số sao đánh giá.");
            if (content.Length == 0) throw new ContentException("Vui lòng nhập nội dung phản hồi.");
            if (content.Length > 1000) throw new ContentException("Nội dung phản hồi tối đa 1000 ký tự.");
            await api.SendFeedbackAsync(new FeedbackRequest(SelectedProduct.Id, Stars, content, OrderId), cancellationToken);
            Content = "";
            Stars = 0;
            StatusMessage = "Cảm ơn bạn! Phản hồi đã được gửi đến SPORTSHOP.";
            if (!IsOrderReview)
            {
                try { ReplaceHistory(await api.GetFeedbackHistoryAsync(1, cancellationToken)); }
                catch (ContentException) { /* Phản hồi đã gửi; lỗi tải lại lịch sử không biến thành lỗi gửi. */ }
            }
        }, cancellationToken);
        Changed();
        return sent;
    }

    // Thay lịch sử bằng trang vừa tải.
    private void ReplaceHistory(Contracts.Common.PagedResponse<FeedbackResponse> page)
    {
        History.Clear();
        foreach (var item in page.Items) History.Add(new FeedbackCard(item));
        _page = page.Page;
        _total = page.TotalItems;
    }

    // Báo thêm trạng thái của màn hình phản hồi.
    protected override void Changed()
    {
        base.Changed();
        foreach (var property in new[] { nameof(OrderId), nameof(IsOrderReview), nameof(Heading), nameof(HasProducts), nameof(NoProducts), nameof(CanSend),
                     nameof(HasMore), nameof(ShowHistoryEmpty), nameof(HistoryCount), nameof(StatusMessage), nameof(HasStatus) }) Notify(property);
    }
}

// Một khảo sát trong danh sách, ba trạng thái như Views/Customer/Surveys.cshtml: đang diễn ra, đã hoàn thành, đã hết hạn.
public sealed record SurveyCard(SurveyResponse Survey)
{
    public int Id => Survey.Id;
    public string Title => Survey.Title;
    public string Description => Survey.Description;
    public bool CanAnswer => !Survey.Done && Survey.IsOpen;
    public string StateText => Survey.Done ? "ĐÃ HOÀN THÀNH" : Survey.IsOpen ? "ĐANG DIỄN RA" : "ĐÃ HẾT HẠN";
    public string BadgeText => Survey.Done ? "Đã gửi" : Survey.IsOpen ? "Thực hiện" : "Hết hạn";
    public string DateText => Survey.Done
        ? (Survey.CompletedAt?.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy") ?? "")
        : "Hạn: " + Survey.Deadline.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy");
    public string MetaText => $"{Survey.QuestionCount} câu hỏi · khoảng {Math.Max(1, Survey.QuestionCount)} phút";
    public string Icon => Survey.Done ? "ic_check_circle_teal.png" : Survey.IsOpen ? "ic_clipboard_list_teal.png" : "ic_calendar_clock_muted.png";
}

// Danh sách khảo sát của khách: tải qua IContentApi.GetSurveysAsync(), đếm khảo sát đang chờ và đã hoàn thành như phần tóm tắt của Surveys.cshtml.
public sealed class SurveysViewModel(IContentApi api) : ContentViewModel
{
    public ObservableCollection<SurveyCard> Surveys { get; } = [];
    public int PendingCount => Surveys.Count(s => s.CanAnswer);
    public int DoneCount => Surveys.Count(s => s.Survey.Done);
    public string PendingText => $"{PendingCount:00} khảo sát đang chờ";
    public string DoneText => $"Đã hoàn thành {DoneCount} khảo sát";
    public bool ShowEmpty => HasLoaded && !HasError && Surveys.Count == 0;

    // Tải lại danh sách; API đã sắp khảo sát chưa làm lên trước.
    public Task LoadAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        var items = await api.GetSurveysAsync(cancellationToken);
        Surveys.Clear();
        foreach (var item in items) Surveys.Add(new SurveyCard(item));
        HasLoaded = true;
    }, cancellationToken);

    // Báo thêm số đếm và trạng thái trống.
    protected override void Changed()
    {
        base.Changed();
        foreach (var property in new[] { nameof(PendingCount), nameof(DoneCount), nameof(PendingText), nameof(DoneText), nameof(ShowEmpty) }) Notify(property);
    }
}

// Một lựa chọn của câu hỏi khảo sát; IsMultiple để giao diện vẽ ô vuông (chọn nhiều) hay ô tròn (chọn một).
public sealed class SurveyOptionItem(SurveyOptionResponse option, bool isMultiple = false) : INotifyPropertyChanged
{
    private bool _isSelected;
    public event PropertyChangedEventHandler? PropertyChanged;
    public int Id => option.Id;
    public bool IsMultiple => isMultiple;
    public string Text => option.Text;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }
}

// Một câu hỏi khảo sát với câu trả lời đang nhập; Type theo CAU_HOI_KHAO_SAT: one, many, scale, text.
public sealed class SurveyQuestionItem(SurveyQuestionResponse question, int index, int total)
{
    public int Id => question.Id;
    public string Title => question.Title;
    public string Type => question.Type;
    public bool Required => question.Required;
    public bool IsText => Type == "text";
    public bool IsChoice => !IsText;
    public bool IsMultiple => Type == "many";
    public string StepText => $"CÂU HỎI {index + 1:00}/{total:00}" + (Required ? " · BẮT BUỘC" : "");
    public string Hint => Type switch
    {
        "many" => "Có thể chọn nhiều phương án.",
        "text" => "Nhập câu trả lời của bạn.",
        "scale" => "Chọn mức điểm từ thấp đến cao.",
        _ => "Chọn phương án phù hợp nhất với bạn.",
    };
    public IReadOnlyList<SurveyOptionItem> Options { get; } = [.. question.Options.Select(o => new SurveyOptionItem(o, question.Type == "many"))];
    public string Text { get; set; } = "";
    public bool IsAnswered => IsText ? !string.IsNullOrWhiteSpace(Text) : Options.Any(o => o.IsSelected);

    // Chọn một phương án: câu nhiều lựa chọn thì bật tắt, câu một lựa chọn thì bỏ chọn các phương án khác.
    public void Choose(SurveyOptionItem option)
    {
        if (IsMultiple) { option.IsSelected = !option.IsSelected; return; }
        foreach (var item in Options) item.IsSelected = item == option;
    }

    // Đổi câu trả lời sang SurveyAnswerRequest; câu không bắt buộc bỏ trống thì không gửi.
    public SurveyAnswerRequest? ToAnswer() => !IsAnswered ? null
        : IsText ? new SurveyAnswerRequest(Id, [], Text.Trim())
        : new SurveyAnswerRequest(Id, [.. Options.Where(o => o.IsSelected).Select(o => o.Id)]);
}

// Làm khảo sát từng câu như form của Surveys.cshtml: chặn sang câu sau khi câu bắt buộc còn trống, nộp qua IContentApi.SubmitSurveyAsync().
public sealed class SurveyFormViewModel(IContentApi api) : ContentViewModel
{
    private int _index;
    public int SurveyId { get; private set; }
    public string Title { get; private set; } = "";
    public IReadOnlyList<SurveyQuestionItem> Questions { get; private set; } = [];
    public SurveyQuestionItem? Current => _index < Questions.Count ? Questions[_index] : null;
    public int Index => _index;
    public bool IsFirst => _index == 0;
    public bool IsLast => _index >= Questions.Count - 1;
    public double Progress => Questions.Count == 0 ? 0 : (_index + 1d) / Questions.Count;
    public string ProgressText => Questions.Count == 0 ? "" : $"{_index + 1}/{Questions.Count}";
    public string NextText => IsLast ? "Gửi khảo sát" : "Câu tiếp theo";
    public bool IsCompleted { get; private set; }
    public bool ShowForm => HasLoaded && !IsCompleted && Questions.Count > 0;

    // Tải câu hỏi qua IContentApi.GetSurveyAsync(); khảo sát đã làm hoặc hết hạn thì báo lỗi thay vì mở form.
    public Task LoadAsync(int surveyId, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        var detail = await api.GetSurveyAsync(surveyId, cancellationToken);
        if (detail.Survey.Done) throw new ContentException("Bạn đã hoàn thành khảo sát này rồi.");
        if (!detail.Survey.IsOpen) throw new ContentException("Khảo sát đã hết hạn hoặc chưa bắt đầu.");
        SurveyId = surveyId;
        Title = detail.Survey.Title;
        Questions = [.. detail.Questions.Select((q, i) => new SurveyQuestionItem(q, i, detail.Questions.Count))];
        _index = 0;
        IsCompleted = false;
        HasLoaded = true;
    }, cancellationToken);

    // Sang câu sau; câu cuối thì nộp. Câu bắt buộc chưa trả lời thì giữ lại và báo lỗi.
    public async Task NextAsync(CancellationToken cancellationToken = default)
    {
        if (Current is { Required: true, IsAnswered: false })
        {
            ErrorMessage = "Vui lòng trả lời câu hỏi bắt buộc này.";
            Changed();
            return;
        }
        ErrorMessage = null;
        if (!IsLast) { _index++; Changed(); return; }
        await RunAsync(async () =>
        {
            var answers = Questions.Select(q => q.ToAnswer()).OfType<SurveyAnswerRequest>().ToList();
            await api.SubmitSurveyAsync(SurveyId, new SubmitSurveyRequest(answers), cancellationToken);
            IsCompleted = true;
        }, cancellationToken);
    }

    // Quay lại câu trước.
    public void Back()
    {
        if (IsFirst) return;
        _index--;
        ErrorMessage = null;
        Changed();
    }

    // Báo thêm vị trí câu hỏi và trạng thái hoàn tất.
    protected override void Changed()
    {
        base.Changed();
        foreach (var property in new[] { nameof(Title), nameof(Questions), nameof(Current), nameof(Index), nameof(IsFirst), nameof(IsLast), nameof(Progress),
                     nameof(ProgressText), nameof(NextText), nameof(IsCompleted), nameof(ShowForm) }) Notify(property);
    }
}
