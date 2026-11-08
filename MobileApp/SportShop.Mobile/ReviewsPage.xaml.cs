using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class ReviewsPage : ContentPage
{
    private readonly ReviewsViewModel _vm;
    private readonly AuthViewModel _auth;
    public ReviewsPage(ReviewsViewModel vm, AuthViewModel auth)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _auth = auth;
    }

    public Task LoadAsync(int productId, string productName) => _vm.LoadAsync(productId, productName);
    private async void OnRefreshClicked(object? sender, EventArgs e) => await _vm.RefreshAsync();
    private async void OnMoreClicked(object? sender, EventArgs e) => await _vm.LoadMoreAsync();
    private async void OnMoreMineClicked(object? sender, EventArgs e) => await _vm.LoadMoreMineAsync();
    private async void OnSendClicked(object? sender, EventArgs e) => await _vm.SendAsync();

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        if (await DisplayAlertAsync("Đăng nhập", "Mở màn hình đăng nhập để đánh giá sản phẩm đã nhận?", "Đăng nhập", "Ở lại"))
            await _auth.LeaveAsync();
    }
}
