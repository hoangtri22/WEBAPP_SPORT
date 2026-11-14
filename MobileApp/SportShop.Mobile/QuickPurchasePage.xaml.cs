using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Modal mua nhanh chỉ đóng thành công sau khi API đã xác nhận thêm vào giỏ.
public partial class QuickPurchasePage : ContentPage
{
    private readonly QuickPurchaseViewModel _vm;
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _started;
    private bool _closing;

    public Task<bool> Completion => _completion.Task;

    public QuickPurchasePage(ICatalogApi catalog, ICommerceApi commerce, int productId, bool buyNow)
    {
        InitializeComponent();
        BindingContext = _vm = new QuickPurchaseViewModel(catalog, commerce, productId, buyNow);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;
        await _vm.LoadAsync(_lifetime.Token);
    }

    protected override void OnDisappearing()
    {
        _lifetime.Cancel();
        // Cũng hoàn tất khi điều hướng gốc bị thay bởi hết phiên hoặc trang bị đóng từ bên ngoài.
        if (!_closing) _completion.TrySetResult(false);
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_vm.IsSubmitting) _ = CloseAsync(false);
        return true;
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await CloseAsync(false);
    private async void OnRetryClicked(object? sender, EventArgs e) => await _vm.LoadAsync(_lifetime.Token);
    private void OnDecreaseClicked(object? sender, EventArgs e) => _vm.Quantity--;
    private void OnIncreaseClicked(object? sender, EventArgs e) => _vm.Quantity++;

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        if (_closing) return;
        if (await _vm.SubmitAsync(_lifetime.Token)) await CloseAsync(true);
    }

    private async Task CloseAsync(bool added)
    {
        if (_closing || (!added && _vm.IsSubmitting)) return;
        _closing = true;
        try
        {
            if (Navigation.ModalStack.LastOrDefault() == this) await Navigation.PopModalAsync();
            _completion.TrySetResult(added);
        }
        finally { _lifetime.Cancel(); }
    }
}
