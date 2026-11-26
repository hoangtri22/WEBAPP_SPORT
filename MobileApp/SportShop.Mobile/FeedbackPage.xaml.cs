using System.ComponentModel;
using SportShop.Contracts.Customer;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang Gửi phản hồi giống Views/Customer/Feedback.cshtml; mở từ đơn đã giao thì chỉ đánh giá sản phẩm của đơn đó; dùng FeedbackViewModel.
public partial class FeedbackPage : ContentPage
{
    private readonly FeedbackViewModel _vm;
    private readonly List<ImageButton> _stars = [];

    // Nhận FeedbackViewModel và dựng 5 nút sao như .rating-input của web.
    public FeedbackPage(FeedbackViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        foreach (var value in vm.StarValues)
        {
            var star = new ImageButton
            {
                Source = "ic_star_muted.png", WidthRequest = 44, HeightRequest = 44, Padding = 8,
                AutomationId = $"FeedbackStar{value}",
            };
            SemanticProperties.SetDescription(star, $"{value} sao");
            star.Clicked += (_, _) => _vm.Stars = value;
            _stars.Add(star);
            StarRow.Children.Add(star);
        }
        _vm.PropertyChanged += OnViewModelChanged;
    }

    // Giới hạn sản phẩm theo đơn đã giao; gọi trước khi mở trang.
    public void UseOrder(int orderId, string orderCode, IEnumerable<EligibleProductResponse> products)
    {
        _vm.UseOrder(orderId, orderCode, products);
        Title = "Đánh giá đơn hàng";
        HistorySection.IsVisible = false;
    }

    // Tải sản phẩm được phản hồi và lịch sử khi mở trang lần đầu.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_vm.HasLoaded) await _vm.LoadAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e) => await _vm.LoadAsync();
    private async void OnLoadMoreClicked(object? sender, EventArgs e) => await _vm.LoadMoreAsync();

    // Gửi phản hồi qua FeedbackViewModel.SendAsync(); đánh giá từ đơn hàng thì báo xong rồi quay lại tab Đơn hàng.
    private async void OnSendClicked(object? sender, EventArgs e)
    {
        if (await _vm.SendAsync() && _vm.IsOrderReview)
        {
            await DisplayAlertAsync("Đã gửi đánh giá", _vm.StatusMessage ?? "", "Đóng");
            await Navigation.PopAsync();
        }
    }

    // Tô sao theo số sao đang chọn.
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FeedbackViewModel.Stars)) return;
        for (var i = 0; i < _stars.Count; i++)
            _stars[i].Source = i < _vm.Stars ? "ic_star_fill_orange.png" : "ic_star_muted.png";
    }
}
