using System.Windows.Input;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Tab Đơn hàng giống Views/Customer/Orders.cshtml: lọc, tìm, theo dõi trạng thái 8 giây một lần, hủy đơn chờ xác nhận, đánh giá đơn đã giao.
public partial class OrdersPage : ContentPage
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(8);
    private readonly OrdersViewModel _vm;
    private readonly IServiceProvider _services;
    private IDispatcherTimer? _pollTimer;
    private CancellationTokenSource? _pollCts;
    private bool _isPageActive;
    private bool _isPollingInFlight;

    public ICommand RefreshCommand { get; }

    // Nhận OrdersViewModel và bộ dịch vụ để mở các trang khác.
    public OrdersPage(OrdersViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _services = services;
        RefreshCommand = new Command(async () => await _vm.LoadOrdersAsync(forceReload: true));
    }

    // Kéo làm mới: tải lại đơn hàng.
    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await _vm.LoadOrdersAsync(forceReload: true);
    }

    // Tải đơn hàng khi mở trang và bắt đầu chu kỳ tự động kiểm tra trạng thái đơn hàng định kỳ (8 giây/lần giống Web).
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isPageActive = true;
        App.AppResumed -= OnAppResumed;
        App.AppStopped -= OnAppStopped;
        App.AppResumed += OnAppResumed;
        App.AppStopped += OnAppStopped;

        // Luôn lấy lại từ API khi mở trang để thấy ngay đơn vừa đặt trên Web (kể cả "Mua ngay") hoặc trên app.
        await _vm.LoadOrdersAsync(forceReload: true);

        if (_isPageActive)
            StartOrderStatusPolling();
    }

    // Dừng chu kỳ tự cập nhật khi rời màn hình Đơn hàng.
    protected override void OnDisappearing()
    {
        _isPageActive = false;
        App.AppResumed -= OnAppResumed;
        App.AppStopped -= OnAppStopped;
        StopOrderStatusPolling();
        base.OnDisappearing();
    }

    // Khi ứng dụng quay lại foreground trong lúc đang mở OrdersPage: tiếp tục timer và kiểm tra ngay trạng thái mới.
    private async void OnAppResumed()
    {
        if (!_isPageActive) return;
        StartOrderStatusPolling();
        await PollOrderStatusesAsync();
    }

    // Khi ứng dụng xuống background: tạm dừng timer polling để tiết kiệm tài nguyên.
    private void OnAppStopped()
    {
        StopOrderStatusPolling();
    }

    // Bắt đầu kiểm tra trạng thái đơn theo PollInterval.
    private void StartOrderStatusPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = new CancellationTokenSource();

        if (_pollTimer is null)
        {
            _pollTimer = Dispatcher.CreateTimer();
            _pollTimer.Interval = PollInterval;
            _pollTimer.IsRepeating = true;
            _pollTimer.Tick += OnPollTimerTick;
        }

        if (!_pollTimer.IsRunning)
            _pollTimer.Start();
    }

    // Dừng timer và hủy request kiểm tra đang chạy.
    private void StopOrderStatusPolling()
    {
        if (_pollTimer is { IsRunning: true })
            _pollTimer.Stop();

        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    // Đến hạn thì kiểm tra trạng thái đơn.
    private async void OnPollTimerTick(object? sender, EventArgs e)
    {
        await PollOrderStatusesAsync();
    }

    // Lấy thay đổi qua OrdersViewModel.RefreshOrdersSilentlyAsync() và báo cho khách; _isPollingInFlight chặn hai lượt chạy chồng nhau.
    private async Task PollOrderStatusesAsync()
    {
        if (!_isPageActive || _isPollingInFlight) return;
        _isPollingInFlight = true;
        try
        {
            var token = _pollCts?.Token ?? CancellationToken.None;
            var changes = await _vm.RefreshOrdersSilentlyAsync(token);
            if (changes.Count > 0 && _isPageActive)
            {
                await DisplayAlertAsync(
                    "Cập nhật trạng thái đơn hàng",
                    string.Join("\n", changes),
                    "Đóng");
            }
        }
        finally
        {
            _isPollingInFlight = false;
        }
    }

    // Chọn đơn để xem chi tiết.
    private void OnOrderCardTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is OrderMasterCard card)
            _vm.SelectedOrder = card;
    }

    // Lọc đơn theo tab trạng thái.
    private void OnStatusTabTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is OrderStatusTabItem tab)
            _vm.SelectedStatusFilter = tab.Key;
    }

    // Lọc đơn theo nút trạng thái.
    private void OnStatusFilterClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string status })
            _vm.SelectedStatusFilter = status;
        else if (sender is Button { CommandParameter: OrderStatusTabItem tab })
            _vm.SelectedStatusFilter = tab.Key;
    }

    // Đánh giá sản phẩm của đơn đã giao: khách cần đăng nhập vì API phản hồi chỉ dành cho tài khoản; mở FeedbackPage giới hạn theo đơn.
    private async void OnReviewOrderClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: OrderMasterCard card } || !card.CanReview) return;
        var auth = _services.GetRequiredService<AuthViewModel>();
        if (!auth.IsSignedIn)
        {
            if (await DisplayAlertAsync("Cần đăng nhập", "Đăng nhập tài khoản có số điện thoại của đơn này để gửi đánh giá.", "Đăng nhập", "Để sau"))
                await auth.LeaveAsync();
            return;
        }
        var page = _services.GetRequiredService<FeedbackPage>();
        page.UseOrder(card.Id, card.Code, card.Lines.Select(l => new SportShop.Contracts.Customer.EligibleProductResponse(l.ProductId, l.Name)));
        await Navigation.PushAsync(page);
    }

    // Mở trang sản phẩm để mua hàng.
    private async void OnShopNowClicked(object? sender, EventArgs e)
    {
        await AppNavigation.OpenCatalogAsync(Navigation, _services);
    }

    // Hỏi xác nhận rồi hủy đơn.
    private async void OnCancelOrderCardClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: OrderMasterCard card } || !card.CanCancel)
            return;

        var confirm = await DisplayAlertAsync(
            "Xác nhận hủy đơn",
            $"Hủy đơn hàng {card.Code}?",
            "Hủy đơn",
            "Đóng");

        if (confirm)
            await _vm.CancelOrderAsync(card);
    }
}
