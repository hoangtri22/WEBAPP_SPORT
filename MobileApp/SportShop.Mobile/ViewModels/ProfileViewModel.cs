using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

// Màn hình Tài khoản: đọc hồ sơ từ API, sửa rồi lưu theo cùng quy tắc của MeController.
public sealed class ProfileViewModel : INotifyPropertyChanged
{
    private readonly IAccountApi _api;
    private IReadOnlyCollection<string> _interests = [];
    private bool _isBusy;
    private bool _isLoaded;
    private string? _errorMessage;
    private string? _statusMessage;
    private string _fullName = "";
    private string _email = "";
    private string _phone = "";
    private string? _gender;
    private DateTime? _birth;
    private CustomerProfileResponse? _profile;

    // Nhận IAccountApi.
    public ProfileViewModel(IAccountApi api)
    {
        _api = api;
        LoadCommand = new AsyncCommand(LoadAsync, () => IsIdle);
        SaveCommand = new AsyncCommand(SaveAsync, () => IsIdle && IsLoaded);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public AsyncCommand LoadCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public IReadOnlyList<string> Genders { get; } = ["Nam", "Nữ", "Khác"];
    public DateTime Today => DateTime.Today;
    public bool IsBusy => _isBusy;
    public bool IsIdle => !_isBusy;
    // Chưa tải được hồ sơ thì không cho lưu, nếu không sở thích của khách sẽ bị ghi đè bằng danh sách rỗng.
    public bool IsLoaded => _isLoaded;
    public bool CanRetry => !_isLoaded && !_isBusy;
    public bool HasError => !string.IsNullOrEmpty(_errorMessage);
    public string? ErrorMessage => _errorMessage;
    public bool HasStatus => !string.IsNullOrEmpty(_statusMessage);
    public string? StatusMessage => _statusMessage;
    public string Username => _profile?.Username ?? "";
    public string Initials => _profile?.Initials ?? "";
    public string SummaryText => _profile is null ? "" : $"Hạng {_profile.Tier} · {_profile.Points:N0} điểm · {_profile.Orders} đơn hàng";

    public string FullName { get => _fullName; set { _fullName = value; Notify(); } }
    public string Email { get => _email; set { _email = value; Notify(); } }
    public string Phone { get => _phone; set { _phone = value; Notify(); } }
    public string? Gender { get => _gender; set { _gender = value; Notify(); } }
    public DateTime? Birth { get => _birth; set { _birth = value; Notify(); } }

    // Tải hồ sơ qua IAccountApi.
    public async Task LoadAsync()
    {
        SetBusy(true);
        try
        {
            var me = await _api.GetProfileAsync(default);
            if (me is null) ShowError("Chưa tải được hồ sơ. Vui lòng thử lại.");
            else Apply(me);
        }
        finally { SetBusy(false); }
    }

    // Kiểm tra rồi lưu hồ sơ.
    private async Task SaveAsync()
    {
        var error = Validate();
        if (error is not null) { ShowError(error); return; }
        SetBusy(true);
        try
        {
            Apply(await _api.UpdateProfileAsync(new UpdateProfileRequest(
                FullName.Trim(), Email.Trim(), Phone.Trim(), Birth?.Date, Gender!, [.. _interests]), default));
            _statusMessage = "Đã lưu hồ sơ.";
        }
        catch (AuthException ex) { ShowError(ex.Message); }
        finally { SetBusy(false); }
    }

    // Kiểm tra họ tên, email, số điện thoại, giới tính, ngày sinh; trả về thông báo lỗi nếu sai.
    private string? Validate()
    {
        if (FullName.Trim().Length == 0) return "Vui lòng nhập họ và tên.";
        if (!Regex.IsMatch(Email.Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$")) return "Email chưa đúng định dạng.";
        if (!Regex.IsMatch(Phone.Trim(), @"^0\d{9}$")) return "Số điện thoại cần gồm 10 chữ số.";
        if (Gender is null || !Genders.Contains(Gender)) return "Vui lòng chọn giới tính.";
        return Birth?.Date > DateTime.Today ? "Ngày sinh không hợp lệ." : null;
    }

    // Đổ dữ liệu hồ sơ vào các ô.
    private void Apply(CustomerMeResponse me)
    {
        _profile = me.Profile;
        _interests = me.Interests;
        _isLoaded = true;
        FullName = me.Profile.FullName;
        Email = me.Profile.Email;
        Phone = me.Profile.Phone;
        Gender = Genders.Contains(me.Profile.Gender) ? me.Profile.Gender : null;
        Birth = DateTime.TryParse(me.Profile.Birth, out var birth) ? birth : null;
        foreach (var name in new[] { nameof(IsLoaded), nameof(Username), nameof(Initials), nameof(SummaryText) }) Notify(name);
    }

    // Hiện thông báo lỗi.
    private void ShowError(string message)
    {
        _errorMessage = message;
        _statusMessage = null;
        NotifyMessages();
    }

    // Bật tắt trạng thái đang xử lý.
    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        if (busy) { _errorMessage = null; _statusMessage = null; }
        foreach (var name in new[] { nameof(IsBusy), nameof(IsIdle), nameof(CanRetry) }) Notify(name);
        NotifyMessages();
        LoadCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    // Báo giao diện cập nhật các thông báo.
    private void NotifyMessages()
    {
        foreach (var name in new[] { nameof(HasError), nameof(ErrorMessage), nameof(HasStatus), nameof(StatusMessage) }) Notify(name);
    }

    // Báo giao diện cập nhật thuộc tính.
    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

// Đổi mật khẩu: API thu hồi mọi phiên mobile nên làm xong app đưa khách về màn hình đăng nhập.
public sealed class ChangePasswordViewModel : INotifyPropertyChanged
{
    private readonly IAccountApi _api;
    private readonly AuthViewModel _auth;
    private bool _isBusy;
    private string? _errorMessage;
    private string _currentPassword = "";
    private string _newPassword = "";
    private string _confirmPassword = "";

    // Nhận IAccountApi và AuthViewModel.
    public ChangePasswordViewModel(IAccountApi api, AuthViewModel auth)
    {
        _api = api;
        _auth = auth;
        SubmitCommand = new AsyncCommand(SubmitAsync, () => IsIdle);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public AsyncCommand SubmitCommand { get; }
    public bool IsBusy => _isBusy;
    public bool IsIdle => !_isBusy;
    public bool HasError => !string.IsNullOrEmpty(_errorMessage);
    public string? ErrorMessage => _errorMessage;

    public string CurrentPassword { get => _currentPassword; set { _currentPassword = value; Notify(); } }
    public string NewPassword { get => _newPassword; set { _newPassword = value; Notify(); } }
    public string ConfirmPassword { get => _confirmPassword; set { _confirmPassword = value; Notify(); } }

    // Kiểm tra rồi đổi mật khẩu, xong thì rời phiên.
    private async Task SubmitAsync()
    {
        var error = Validate();
        if (error is not null) { ShowError(error); return; }
        SetBusy(true);
        try
        {
            await _api.ChangePasswordAsync(new ChangePasswordRequest(CurrentPassword, NewPassword), default);
            CurrentPassword = NewPassword = ConfirmPassword = "";
            await _auth.LeaveAsync("Đã đổi mật khẩu. Vui lòng đăng nhập lại bằng mật khẩu mới.");
        }
        catch (AuthException ex) { ShowError(ex.Message); }
        finally { SetBusy(false); }
    }

    // Kiểm tra mật khẩu hiện tại và mật khẩu mới, trả về thông báo lỗi nếu sai.
    private string? Validate()
    {
        if (CurrentPassword.Length == 0) return "Vui lòng nhập mật khẩu hiện tại.";
        if (NewPassword.Length < 6) return "Mật khẩu mới cần có ít nhất 6 ký tự.";
        if (NewPassword != ConfirmPassword) return "Mật khẩu xác nhận chưa khớp.";
        return NewPassword == CurrentPassword ? "Mật khẩu mới phải khác mật khẩu hiện tại." : null;
    }

    // Hiện thông báo lỗi.
    private void ShowError(string message)
    {
        _errorMessage = message;
        Notify(nameof(ErrorMessage));
        Notify(nameof(HasError));
    }

    // Bật tắt trạng thái đang xử lý.
    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        if (busy) _errorMessage = null;
        foreach (var name in new[] { nameof(IsBusy), nameof(IsIdle), nameof(HasError), nameof(ErrorMessage) }) Notify(name);
        SubmitCommand.NotifyCanExecuteChanged();
    }

    // Báo giao diện cập nhật thuộc tính.
    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
