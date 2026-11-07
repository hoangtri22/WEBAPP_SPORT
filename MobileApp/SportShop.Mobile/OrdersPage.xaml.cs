using System.Windows.Input;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang đơn hàng, dùng OrdersViewModel.
public partial class OrdersPage : ContentPage
{
    private readonly OrdersViewModel _vm;
    private readonly IServiceProvider _services;

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

    // Tải đơn hàng khi mở trang.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Luôn lấy lại từ API khi mở trang để thấy ngay đơn vừa đặt trên Web (kể cả "Mua ngay") hoặc trên app.
        await _vm.LoadOrdersAsync(forceReload: true);
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

    // Mở trang sản phẩm để mua hàng.
    private async void OnShopNowClicked(object? sender, EventArgs e)
    {
        var catalogPage = _services.GetRequiredService<MainPage>();
        if (catalogPage.Parent is null)
            await Navigation.PushAsync(catalogPage);
        else
            await Navigation.PopToRootAsync();
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
