using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Tab Tài khoản: thẻ hạng, số đơn, điểm và menu như dropdown tài khoản của web; khách chưa đăng nhập thấy lời mời đăng nhập.
public partial class AccountPage : ContentPage
{
    private readonly ProfileViewModel _vm;
    private readonly AuthViewModel _auth;
    private readonly IServiceProvider _services;
    private bool _navigating;

    // Nhận ProfileViewModel để đọc hồ sơ qua IAccountApi, AuthViewModel để biết trạng thái đăng nhập.
    public AccountPage(ProfileViewModel vm, AuthViewModel auth, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _auth = auth;
        _services = services;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ProfileViewModel.HasError) or nameof(ProfileViewModel.IsLoaded)) UpdateState();
        };
    }

    // Mỗi lần mở tab: tải lại hồ sơ để số đơn và điểm mới nhất (đơn vừa đặt, hạng vừa đổi).
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateState();
        if (_auth.IsSignedIn && _vm.IsIdle) await _vm.LoadAsync();
    }

    // Ẩn hiện phần thành viên hoặc khách theo trạng thái đăng nhập.
    private void UpdateState()
    {
        var signedIn = _auth.IsSignedIn;
        SignedInHeader.IsVisible = SignedInStats.IsVisible = signedIn && _vm.IsLoaded;
        MemberMenu.IsVisible = LogoutButton.IsVisible = signedIn;
        GuestHeader.IsVisible = !signedIn;
        ProfileError.IsVisible = signedIn && _vm.HasError && !_vm.IsLoaded;
    }

    // Rời phiên khách để về AuthPage đăng nhập hoặc đăng ký.
    private async void OnSignInClicked(object? sender, EventArgs e) => await _auth.LeaveAsync();

    private async void OnProfileTapped(object? sender, TappedEventArgs e) => await PushAsync<ProfilePage>();
    private async void OnChangePasswordTapped(object? sender, TappedEventArgs e) => await PushAsync<ChangePasswordPage>();
    private async void OnFeedbackTapped(object? sender, TappedEventArgs e) => await PushAsync<FeedbackPage>();
    private async void OnSurveysTapped(object? sender, TappedEventArgs e) => await PushAsync<SurveysPage>();
    private async void OnPromotionsTapped(object? sender, TappedEventArgs e) => await PushAsync<PromotionsPage>();

    // Chuyển sang tab Đơn hàng.
    private async void OnOrdersTapped(object? sender, TappedEventArgs e) => await AppNavigation.OpenTabAsync(AppTab.Orders);

    // Mở danh sách trang thông tin qua InfoPagesPage.LoadAsync().
    private async void OnInfoTapped(object? sender, TappedEventArgs e)
    {
        if (_navigating) return;
        _navigating = true;
        try
        {
            var page = _services.GetRequiredService<InfoPagesPage>();
            await Navigation.PushAsync(page);
            await page.LoadAsync();
        }
        finally { _navigating = false; }
    }

    // Hỏi xác nhận rồi đăng xuất qua AuthViewModel.LeaveAsync().
    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        if (await DisplayAlertAsync("Đăng xuất", "Bạn muốn đăng xuất khỏi tài khoản này?", "Đăng xuất", "Hủy"))
            await _auth.LeaveAsync();
    }

    // Mở một trang lấy từ DI; _navigating chặn chạm hai lần mở trùng trang.
    private async Task PushAsync<TPage>() where TPage : Page
    {
        if (_navigating) return;
        _navigating = true;
        try { await Navigation.PushAsync(_services.GetRequiredService<TPage>()); }
        finally { _navigating = false; }
    }
}
