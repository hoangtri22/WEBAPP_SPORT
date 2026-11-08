using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class PromotionsPage : ContentPage
{
    private readonly PromotionsViewModel _vm;
    public PromotionsPage(PromotionsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_vm.HasLoaded) await _vm.LoadAsync();
    }

    private async void OnReloadClicked(object? sender, EventArgs e) => await _vm.LoadAsync();

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: PromotionCard card }) return;
        try
        {
            await Clipboard.Default.SetTextAsync(card.Code);
            await DisplayAlertAsync("Đã sao chép", $"Mã {card.Code} đã được sao chép.", "Đóng");
        }
        catch (Exception) { await DisplayAlertAsync("Mã khuyến mãi", card.Code, "Đóng"); }
    }
}
