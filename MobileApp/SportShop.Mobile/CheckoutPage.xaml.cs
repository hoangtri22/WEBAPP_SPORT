using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang thanh toán, dùng CartViewModel.
public partial class CheckoutPage : ContentPage
{
    private readonly CartViewModel _vm;
    private readonly IServiceProvider _services;

    // Nhận CartViewModel và bộ dịch vụ để mở các trang khác.
    public CheckoutPage(CartViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _services = services;
    }

    // Tải dữ liệu thanh toán qua CartViewModel.LoadCheckoutAsync() khi mở trang.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCheckoutAsync();
    }

    // Chọn phương thức thanh toán.
    private void OnSelectPaymentMethodTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is PaymentMethodOption option)
            _vm.SelectPaymentMethod(option);
    }

    // Chọn voucher.
    private async void OnSelectVoucherTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is VoucherOptionItem option)
            await _vm.SelectVoucherOptionAsync(option);
    }

    // Đặt hàng qua CartViewModel.PlaceOrderAsync().
    private async void OnPlaceOrderClicked(object? sender, EventArgs e)
    {
        await _vm.PlaceOrderAsync();
    }

    // Quay lại giỏ hàng.
    private async void OnBackToCartClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    // Mở trang đơn hàng sau khi đặt xong.
    private async void OnViewMyOrdersClicked(object? sender, EventArgs e)
    {
        var ordersPage = _services.GetRequiredService<OrdersPage>();
        await Navigation.PushAsync(ordersPage);
    }

    // Tiếp tục mua sắm sau khi đặt xong.
    private async void OnContinueShoppingFromDoneClicked(object? sender, EventArgs e)
    {
        var catalogPage = _services.GetRequiredService<MainPage>();
        if (catalogPage.Parent is null)
            await Navigation.PushAsync(catalogPage);
        else
            await Navigation.PopToRootAsync();
    }
}
