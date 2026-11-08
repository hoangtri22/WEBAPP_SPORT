using SportShop.Mobile.Services;
using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

public partial class InfoPagesPage : ContentPage
{
    private readonly InfoPagesViewModel _vm;
    private string? _requestedSlug;
    public InfoPagesPage(InfoPagesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Nơi mở trang gọi rõ LoadAsync() hoặc LoadAsync("chon-size"); không tự tải lặp ở OnAppearing.
    public Task LoadAsync(string? slug = null)
    {
        _requestedSlug = slug;
        return _vm.LoadAsync(slug);
    }

    private async void OnPageClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: ContentPageSummary page }) await LoadAsync(page.Slug);
    }

    private async void OnAllClicked(object? sender, EventArgs e) => await LoadAsync();
    private async void OnRetryClicked(object? sender, EventArgs e) => await LoadAsync(_requestedSlug);
}
