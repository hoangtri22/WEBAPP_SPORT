using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Trang Chỉnh sửa thông tin giống Views/Customer/Profile.cshtml: hồ sơ, sở thích thể thao và mức hoàn thiện; dùng ProfileViewModel.
public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;

    // Nhận ProfileViewModel.
    public ProfilePage(ProfileViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Tải hồ sơ khi mở trang lần đầu.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_vm.IsLoaded) await _vm.LoadAsync();
    }

    // Bấm chip sở thích: đổi trạng thái chọn qua ProfileViewModel.ToggleInterest(), lưu khi bấm Lưu thay đổi.
    private void OnInterestTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is InterestChoice choice) _vm.ToggleInterest(choice);
    }
}
