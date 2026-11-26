using System.Windows.Input;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Tab Giỏ hàng giống Views/Cart/Index.cshtml: sửa số lượng theo tồn kho, gợi ý mua thêm, tóm tắt và nút thanh toán cố định; dùng CartViewModel.
public partial class CartPage : ContentPage
{
    private readonly CartViewModel _vm;
    private readonly AuthViewModel _auth;
    private readonly IServiceProvider _services;

    public ICommand RefreshCommand { get; }

    // Nhận CartViewModel, AuthViewModel và bộ dịch vụ để mở các trang khác.
    public CartPage(CartViewModel vm, AuthViewModel auth, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _auth = auth;
        _services = services;
        RefreshCommand = new Command(async () => await RefreshCartStateAsync());
    }

    // Kéo làm mới: tải lại giỏ hàng.
    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await RefreshCartStateAsync();
    }

    // Tải lại giỏ mỗi khi mở trang.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshCartStateAsync();
    }

    // Tải giỏ qua CartViewModel.LoadCartAsync() rồi ẩn hiện khu vực đăng nhập, giỏ trống và danh sách.
    private async Task RefreshCartStateAsync()
    {
        GuestLoginCard.IsVisible = !_auth.IsSignedIn;
        await _vm.LoadCartAsync();
        EmptyCartCard.IsVisible = _vm.IsEmpty && !_vm.HasError;
        CartContentSection.IsVisible = _vm.HasItems;
    }

    // Rời phiên khách để quay về trang đăng nhập qua AuthViewModel.LeaveAsync().
    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        await _auth.LeaveAsync();
    }

    // Quay về tab Sản phẩm.
    private async void OnContinueShoppingClicked(object? sender, EventArgs e)
    {
        await AppNavigation.OpenCatalogAsync(Navigation, _services);
    }

    // Tăng số lượng dòng giỏ.
    private async void OnIncreaseQtyClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: CartLineItem item })
        {
            await _vm.ChangeQuantityAsync(item, +1);
            EmptyCartCard.IsVisible = _vm.IsEmpty;
            CartContentSection.IsVisible = _vm.HasItems;
        }
    }

    // Giảm số lượng dòng giỏ.
    private async void OnDecreaseQtyClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: CartLineItem item })
        {
            await _vm.ChangeQuantityAsync(item, -1);
            EmptyCartCard.IsVisible = _vm.IsEmpty;
            CartContentSection.IsVisible = _vm.HasItems;
        }
    }

    // Xóa dòng giỏ.
    private async void OnRemoveItemClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: CartLineItem item })
        {
            await _vm.RemoveItemAsync(item);
            EmptyCartCard.IsVisible = _vm.IsEmpty;
            CartContentSection.IsVisible = _vm.HasItems;
        }
    }

    // Bấm ảnh sản phẩm trong giỏ: mở ProductDetailPage của sản phẩm đó.
    private async void OnCartItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is int productId) await OpenProductDetailAsync(productId);
    }

    // Nhấn ảnh/tên sản phẩm gợi ý mua thêm (upsell): mở trang chi tiết sản phẩm ProductDetailPage.
    private async void OnUpsellItemProductTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is CartUpsellItemViewModel item)
            await OpenProductDetailAsync(item.ProductId);
    }

    // Nút trên sản phẩm gợi ý mua thêm: một biến thể còn hàng thì thêm thẳng vào giỏ, nhiều biến thể thì mở ProductDetailPage để chọn size, màu.
    private async void OnUpsellItemActionClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: CartUpsellItemViewModel item })
            return;

        if (item.CanQuickAdd)
        {
            await _vm.AddUpsellItemAsync(item);
            EmptyCartCard.IsVisible = _vm.IsEmpty;
            CartContentSection.IsVisible = _vm.HasItems;
        }
        else
        {
            await OpenProductDetailAsync(item.ProductId);
        }
    }

    // Mở ProductDetailPage của một sản phẩm.
    private async Task OpenProductDetailAsync(int productId)
    {
        if (productId <= 0) return;
        var detailPage = _services.GetRequiredService<ProductDetailPage>();
        await Navigation.PushAsync(detailPage);
        await detailPage.LoadAsync(productId);
    }

    // Mở trang thanh toán.
    private async void OnProceedCheckoutClicked(object? sender, EventArgs e)
    {
        if (!_vm.CanProceedToCheckout) return;
        var checkoutPage = _services.GetRequiredService<CheckoutPage>();
        await Navigation.PushAsync(checkoutPage);
    }

    // Chuyển sang tab Đơn hàng.
    private async void OnOpenOrdersClicked(object? sender, EventArgs e)
    {
        if (!await AppNavigation.OpenTabAsync(AppTab.Orders))
            await Navigation.PushAsync(_services.GetRequiredService<OrdersPage>());
    }
}
