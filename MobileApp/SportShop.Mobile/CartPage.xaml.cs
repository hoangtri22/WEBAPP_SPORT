using System.Windows.Input;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class CartPage : ContentPage
{
    private readonly CartViewModel _vm;
    private readonly AuthViewModel _auth;
    private readonly IServiceProvider _services;

    public ICommand RefreshCommand { get; }

    public CartPage(CartViewModel vm, AuthViewModel auth, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _auth = auth;
        _services = services;
        RefreshCommand = new Command(async () => await RefreshCartStateAsync());
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await RefreshCartStateAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshCartStateAsync();
    }

    private async Task RefreshCartStateAsync()
    {
        GuestLoginCard.IsVisible = !_auth.IsSignedIn;
        await _vm.LoadCartAsync();
        EmptyCartCard.IsVisible = _vm.IsEmpty && !_vm.HasError;
        CartContentSection.IsVisible = _vm.HasItems;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        await _auth.LeaveAsync();
    }

    private async void OnContinueShoppingClicked(object? sender, EventArgs e)
    {
        var catalogPage = _services.GetRequiredService<MainPage>();
        if (catalogPage.Parent is null)
            await Navigation.PushAsync(catalogPage);
        else
            await Navigation.PopAsync();
    }

    private async void OnIncreaseQtyClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: CartLineItem item })
        {
            await _vm.ChangeQuantityAsync(item, +1);
            EmptyCartCard.IsVisible = _vm.IsEmpty;
            CartContentSection.IsVisible = _vm.HasItems;
        }
    }

    private async void OnDecreaseQtyClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: CartLineItem item })
        {
            await _vm.ChangeQuantityAsync(item, -1);
            EmptyCartCard.IsVisible = _vm.IsEmpty;
            CartContentSection.IsVisible = _vm.HasItems;
        }
    }

    private async void OnRemoveItemClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: CartLineItem item })
        {
            await _vm.RemoveItemAsync(item);
            EmptyCartCard.IsVisible = _vm.IsEmpty;
            CartContentSection.IsVisible = _vm.HasItems;
        }
    }

    private async void OnProceedCheckoutClicked(object? sender, EventArgs e)
    {
        if (!_vm.CanProceedToCheckout) return;
        var checkoutPage = _services.GetRequiredService<CheckoutPage>();
        await Navigation.PushAsync(checkoutPage);
    }

    private async void OnOpenOrdersClicked(object? sender, EventArgs e)
    {
        var ordersPage = _services.GetRequiredService<OrdersPage>();
        await Navigation.PushAsync(ordersPage);
    }

    private async void OnHomeNavClicked(object? sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopToRootAsync();
    }

    private async void OnCatalogNavClicked(object? sender, EventArgs e)
    {
        var catalogPage = _services.GetRequiredService<MainPage>();
        if (catalogPage.Parent is null)
            await Navigation.PushAsync(catalogPage);
        else
            await Navigation.PopAsync();
    }

    private async void OnCartNavClicked(object? sender, EventArgs e)
    {
        await RefreshCartStateAsync();
    }

    private async void OnAccountNavClicked(object? sender, EventArgs e)
    {
        var action = _auth.IsSignedIn ? "Đăng xuất" : "Đăng nhập / Đăng ký";
        if (await DisplayActionSheetAsync("Tài khoản", "Đóng", null, action) == action)
            await _auth.LeaveAsync();
    }
}
