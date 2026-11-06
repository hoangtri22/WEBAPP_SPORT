using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class CheckoutPage : ContentPage
{
    private readonly CartViewModel _vm;
    private readonly IServiceProvider _services;

    public CheckoutPage(CartViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _services = services;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCheckoutAsync();
    }

    private void OnSelectPaymentMethodTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is PaymentMethodOption option)
            _vm.SelectPaymentMethod(option);
    }

    private async void OnSelectVoucherTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is VoucherOptionItem option)
            await _vm.SelectVoucherOptionAsync(option);
    }

    private async void OnPlaceOrderClicked(object? sender, EventArgs e)
    {
        await _vm.PlaceOrderAsync();
    }

    private async void OnBackToCartClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnViewMyOrdersClicked(object? sender, EventArgs e)
    {
        var ordersPage = _services.GetRequiredService<OrdersPage>();
        await Navigation.PushAsync(ordersPage);
    }

    private async void OnContinueShoppingFromDoneClicked(object? sender, EventArgs e)
    {
        var catalogPage = _services.GetRequiredService<MainPage>();
        if (catalogPage.Parent is null)
            await Navigation.PushAsync(catalogPage);
        else
            await Navigation.PopToRootAsync();
    }
}
