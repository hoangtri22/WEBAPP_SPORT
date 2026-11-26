using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang thanh toán giống Views/Cart/Checkout.cshtml, đặt xong hiện kết quả như Views/Cart/Done.cshtml; dùng CartViewModel.
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

    // Đặt xong: đưa tab Giỏ hàng về gốc rồi chuyển sang tab Đơn hàng.
    private async void OnViewMyOrdersClicked(object? sender, EventArgs e)
    {
        if (await AppNavigation.OpenTabAsync(AppTab.Orders))
            await Navigation.PopToRootAsync(false);
        else
            await Navigation.PushAsync(_services.GetRequiredService<OrdersPage>());
    }

    // Tiếp tục mua sắm sau khi đặt xong: đưa tab Giỏ hàng về gốc rồi mở tab Sản phẩm.
    private async void OnContinueShoppingFromDoneClicked(object? sender, EventArgs e)
    {
        await AppNavigation.OpenCatalogAsync(Navigation, _services);
        if (Navigation.NavigationStack.Count > 1) await Navigation.PopToRootAsync(false);
    }
}
