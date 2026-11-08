using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

// Trạng thái tải chung cho các trang nội dung độc lập.
public abstract class ContentViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsBusy { get; private set; }
    public bool IsIdle => !IsBusy;
    public string? ErrorMessage { get; protected set; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasLoaded { get; protected set; }
    protected void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    // Giữ dữ liệu đã xem nếu tải lại lỗi; lỗi không trở thành ngoại lệ trong async command.
    protected async Task<bool> RunAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        if (IsBusy) return false;
        IsBusy = true;
        ErrorMessage = null;
        Changed();
        try { await action(); return true; }
        catch (ContentException ex) { ErrorMessage = ex.Message; return false; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
        finally { IsBusy = false; Changed(); }
    }

    protected virtual void Changed()
    {
        foreach (var property in new[] { nameof(IsBusy), nameof(IsIdle), nameof(HasLoaded), nameof(HasError), nameof(ErrorMessage) }) Notify(property);
    }
}

public sealed record PromotionCard(PromotionDto Promotion)
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    public string Code => Promotion.Code;
    public string Name => Promotion.Name;
    public string Discount => $"Giảm {Promotion.Percent:0.##}%" + (Promotion.MaxDiscount is decimal max ? $" · Tối đa {max.ToString("N0", Vi)}₫" : "");
    public string Conditions => string.IsNullOrWhiteSpace(Promotion.RequiredTier)
        ? "Áp dụng theo điều kiện của chương trình"
        : "Dành cho hạng " + (Promotion.RequiredTier switch { "bac" => "Bạc", "vang" => "Vàng", "thuong" => "Thường", _ => Promotion.RequiredTier });
    public string Validity => $"Hết hạn {Promotion.EndsAt.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy HH:mm}";
    public string Limit => Promotion.PerCustomerLimit is int limit ? $"Tối đa {limit} lượt mỗi khách" : "Kiểm tra điều kiện khi thanh toán";
}

public sealed class PromotionsViewModel(IContentApi api) : ContentViewModel
{
    public ObservableCollection<PromotionCard> Promotions { get; } = [];
    public bool ShowEmpty => HasLoaded && !HasError && Promotions.Count == 0;
    public Task LoadAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        var items = await api.GetPromotionsAsync(cancellationToken);
        Promotions.Clear();
        foreach (var item in items) Promotions.Add(new PromotionCard(item));
        HasLoaded = true;
    }, cancellationToken);

    protected override void Changed() { base.Changed(); Notify(nameof(ShowEmpty)); }
}

public sealed class InfoPagesViewModel(IContentApi api) : ContentViewModel
{
    public ObservableCollection<ContentPageSummary> Pages { get; } = [];
    public ContentPageDetail? Current { get; private set; }
    public string? CurrentSlug { get; private set; }
    public bool ShowList => Current is null;
    public bool ShowPage => Current is not null;
    public bool ShowEmpty => HasLoaded && !HasError && Current is null && Pages.Count == 0;

    // Không mở trang cũ dưới tiêu đề mới khi API thất bại.
    public Task LoadAsync(string? slug = null, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            var pages = await api.GetPagesAsync(cancellationToken);
            Pages.Clear();
            foreach (var page in pages) Pages.Add(page);
            Current = null;
            CurrentSlug = null;
        }
        else
        {
            var page = await api.GetPageAsync(slug, cancellationToken);
            Current = page;
            CurrentSlug = slug;
        }
        HasLoaded = true;
    }, cancellationToken);

    protected override void Changed()
    {
        base.Changed();
        foreach (var property in new[] { nameof(Current), nameof(CurrentSlug), nameof(ShowList), nameof(ShowPage), nameof(ShowEmpty) }) Notify(property);
    }
}

public sealed record ReviewCard(ProductReviewDto Review)
{
    public string Author => Review.Author;
    public string Stars => new('★', Review.Stars);
    public string Content => Review.Content;
    public string SentAt => Review.SentAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm");
    public string? Reply => Review.Reply;
    public bool HasReply => !string.IsNullOrWhiteSpace(Reply);
}

public sealed record MyReviewCard(FeedbackResponse Feedback)
{
    public string Stars => Feedback.Stars is >= 1 and <= 5 ? new string('★', Feedback.Stars.Value) : "Phản hồi";
    public string Content => Feedback.Content;
    public string Status => Feedback.Status switch
    {
        "moi" => "Đã gửi · Chờ cửa hàng xử lý",
        "dang_xu_ly" => "Cửa hàng đang xử lý",
        "da_tra_loi" => "Cửa hàng đã trả lời",
        _ => "Đã gửi · " + Feedback.Status
    };
    public string SentAt => Feedback.SentAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm");
    public string? Reply => Feedback.Reply;
    public bool HasReply => !string.IsNullOrWhiteSpace(Reply);
}

public sealed class ReviewsViewModel(IContentApi api, ISessionManager session) : ContentViewModel
{
    private int _productId;
    private int _page;
    private long _total;
    private int _myPage;
    private long _myTotal;
    private int _stars = 5;
    private string _content = "";
    private decimal? _average;
    public ObservableCollection<ReviewCard> Reviews { get; } = [];
    public ObservableCollection<MyReviewCard> MyReviews { get; } = [];
    public IReadOnlyList<int> StarOptions { get; } = [5, 4, 3, 2, 1];
    public string ProductName { get; private set; } = "";
    public bool IsSignedIn => session.IsSignedIn;
    public bool IsGuest => !IsSignedIn;
    public bool IsEligible { get; private set; }
    public bool CanSend => IsIdle && IsSignedIn && IsEligible;
    public bool HasMore => Reviews.Count < _total;
    public bool HasMoreMine => IsSignedIn && MyReviews.Count < _myTotal;
    public bool ShowEmpty => HasLoaded && !HasError && Reviews.Count == 0;
    public bool ShowMyEmpty => IsSignedIn && MyReviews.Count == 0 && string.IsNullOrEmpty(AccountError);
    public string Summary => _average is decimal average ? $"{average:0.0}/5 · {_total} đánh giá" : "Chưa có đánh giá công khai";
    public string EligibilityText => IsGuest ? "Đăng nhập để đánh giá sản phẩm đã nhận hàng."
        : HasAccountError ? "Chưa kiểm tra được quyền đánh giá. Vui lòng tải lại."
        : IsEligible ? "Bạn đã mua và nhận sản phẩm này. Hãy chia sẻ trải nghiệm của bạn."
        : "Bạn chỉ có thể đánh giá sau khi mua và nhận sản phẩm này.";
    public string? AccountError { get; private set; }
    public bool HasAccountError => !string.IsNullOrEmpty(AccountError);
    public string? StatusMessage { get; private set; }
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);
    public int Stars { get => _stars; set { _stars = value; Notify(); } }
    public string Content { get => _content; set { _content = value; Notify(); } }

    // Tải công khai trước; lỗi lịch sử cá nhân không che các đánh giá công khai.
    public Task LoadAsync(int productId, string productName, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        if (_productId != productId)
        {
            _productId = productId;
            ProductName = productName;
            Reviews.Clear();
            MyReviews.Clear();
            _page = _myPage = 0;
            _total = _myTotal = 0;
            _average = null;
            IsEligible = false;
            Content = "";
            StatusMessage = null;
            HasLoaded = false;
        }
        ContentException? publicError = null;
        try
        {
            ReplaceReviews(await api.GetReviewsAsync(productId, 1, cancellationToken));
            HasLoaded = true;
        }
        catch (ContentException ex) { publicError = ex; }
        await LoadAccountAsync(cancellationToken);
        if (publicError is not null) throw publicError;
    }, cancellationToken);

    public Task RefreshAsync(CancellationToken cancellationToken = default) => LoadAsync(_productId, ProductName, cancellationToken);

    public Task LoadMoreAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        if (!HasMore) return;
        var page = await api.GetReviewsAsync(_productId, _page + 1, cancellationToken);
        foreach (var item in page.Items)
            if (!Reviews.Any(r => r.Review.Id == item.Id)) Reviews.Add(new ReviewCard(item));
        _page = page.Page;
        _total = page.Items.Count == 0 ? Reviews.Count : page.TotalItems;
        _average = page.AverageStars;
    }, cancellationToken);

    public Task LoadMoreMineAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        if (!HasMoreMine) return;
        var page = await api.GetMyFeedbackAsync(_productId, _myPage + 1, cancellationToken);
        foreach (var item in page.Items)
            if (!MyReviews.Any(r => r.Feedback.Id == item.Id)) MyReviews.Add(new MyReviewCard(item));
        _myPage = page.Page;
        _myTotal = page.Items.Count == 0 ? MyReviews.Count : page.TotalItems;
    }, cancellationToken);

    // Server kiểm tra lại quyền mua hàng; giữ nguyên nội dung nếu gửi thất bại.
    public Task SendAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        StatusMessage = null;
        if (!IsSignedIn || !IsEligible) throw new ContentException("Bạn chỉ có thể đánh giá sản phẩm đã mua và nhận hàng.");
        var content = Content.Trim();
        if (Stars is < 1 or > 5) throw new ContentException("Vui lòng chọn từ 1 đến 5 sao.");
        if (content.Length is < 1 or > 1000) throw new ContentException("Nội dung đánh giá cần từ 1 đến 1000 ký tự.");
        await api.SendFeedbackAsync(new FeedbackRequest(_productId, Stars, content), cancellationToken);
        Content = "";
        StatusMessage = "Đã gửi đánh giá đến cửa hàng. Bạn có thể theo dõi phản hồi bên dưới; đánh giá công khai phụ thuộc trạng thái hiển thị của cửa hàng.";
        await LoadAccountAsync(cancellationToken);
        try { ReplaceReviews(await api.GetReviewsAsync(_productId, 1, cancellationToken)); }
        catch (ContentException) { /* Đánh giá đã gửi thành công; không mời gửi lại nếu refresh bị lỗi. */ }
    }, cancellationToken);

    private void ReplaceReviews(ProductReviewsResponse page)
    {
        Reviews.Clear();
        foreach (var item in page.Items) Reviews.Add(new ReviewCard(item));
        _page = page.Page;
        _total = page.TotalItems;
        _average = page.AverageStars;
    }

    private async Task LoadAccountAsync(CancellationToken cancellationToken)
    {
        AccountError = null;
        IsEligible = false;
        if (!IsSignedIn) { MyReviews.Clear(); _myTotal = 0; return; }
        try
        {
            var eligible = await api.GetEligibleProductsAsync(cancellationToken);
            IsEligible = eligible.Any(p => p.Id == _productId);
            var history = await api.GetMyFeedbackAsync(_productId, 1, cancellationToken);
            MyReviews.Clear();
            foreach (var item in history.Items) MyReviews.Add(new MyReviewCard(item));
            _myPage = history.Page;
            _myTotal = history.TotalItems;
        }
        catch (ContentException ex) { AccountError = ex.Message; }
    }

    protected override void Changed()
    {
        base.Changed();
        foreach (var property in new[] { nameof(ProductName), nameof(IsSignedIn), nameof(IsGuest), nameof(IsEligible), nameof(CanSend),
                     nameof(HasMore), nameof(HasMoreMine), nameof(ShowEmpty), nameof(ShowMyEmpty), nameof(Summary), nameof(EligibilityText),
                     nameof(AccountError), nameof(HasAccountError), nameof(StatusMessage), nameof(HasStatus) }) Notify(property);
    }
}
