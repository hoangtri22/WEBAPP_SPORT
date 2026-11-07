using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

// Trạng thái và thao tác của trang đăng nhập, đăng ký, vào bằng tư cách khách.
public sealed class AuthViewModel : INotifyPropertyChanged
{
    private readonly IAuthApi _api;
    private readonly ISessionManager _session;
    private readonly IDeviceIdentity _device;
    private readonly IGoogleSignIn? _google;
    private bool _isBusy;
    private bool _isRegister;
    private bool _restored;
    private bool _entered;
    private string _username = "";
    private string _email = "";
    private string _phone = "";
    private string _password = "";
    private string? _errorMessage;

    // Nhận IAuthApi, ISessionManager, IDeviceIdentity và IGoogleSignIn.
    public AuthViewModel(IAuthApi api, ISessionManager session, IDeviceIdentity device, IGoogleSignIn? google = null)
    {
        _api = api;
        _session = session;
        _device = device;
        _google = google;
        ShowLoginCommand = new AsyncCommand(() => SwitchAsync(false), () => IsIdle);
        ShowRegisterCommand = new AsyncCommand(() => SwitchAsync(true), () => IsIdle);
        SubmitCommand = new AsyncCommand(SubmitAsync, () => IsIdle);
        GuestCommand = new AsyncCommand(EnterAsGuestAsync, () => IsIdle);
        GoogleCommand = new AsyncCommand(SignInWithGoogleAsync, () => IsIdle && _google is not null);
        _session.Expired += OnSessionExpired;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Entered;
    public event Action? Left;
    public AsyncCommand ShowLoginCommand { get; }
    public AsyncCommand ShowRegisterCommand { get; }
    public AsyncCommand SubmitCommand { get; }
    public AsyncCommand GuestCommand { get; }
    public AsyncCommand GoogleCommand { get; }
    public bool IsSignedIn => _session.IsSignedIn;
    public bool IsBusy => _isBusy;
    public bool IsIdle => !_isBusy;
    public bool IsRegister => _isRegister;
    public bool IsLogin => !_isRegister;
    public bool HasError => !string.IsNullOrEmpty(_errorMessage);
    public string? ErrorMessage => _errorMessage;
    public string SubmitText => _isRegister ? "Tạo tài khoản" : "Đăng nhập";
    public string UsernameHint => _isRegister ? "Tên đăng nhập" : "Tên đăng nhập hoặc email";

    public string Username { get => _username; set { _username = value; Notify(); } }
    public string Email { get => _email; set { _email = value; Notify(); } }
    public string Phone { get => _phone; set { _phone = value; Notify(); } }
    public string Password { get => _password; set { _password = value; Notify(); } }

    // Mở app: nếu máy còn phiên thì vào thẳng Trang chủ, kể cả khi đang mất mạng; phiên bị API từ chối đã được SessionManager xóa.
    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (_restored) return;
        _restored = true;
        SetBusy(true);
        try
        {
            if (await _session.RestoreAsync(cancellationToken)) Enter();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { SetBusy(false); }
    }

    // Đăng xuất: SessionManager thu hồi phiên rồi xóa token trong máy, sau đó về màn hình ngoài cùng; khách chỉ quay về màn hình này.
    public async Task LeaveAsync(string? notice = null)
    {
        _entered = false;
        await _session.SignOutAsync();
        Username = Email = Phone = Password = "";
        if (notice is not null) ShowError(notice);
        Left?.Invoke();
    }

    // Phiên hết hạn hoặc bị thu hồi khi đang dùng app: về màn hình đăng nhập kèm lý do.
    private void OnSessionExpired()
    {
        if (!_entered) return;
        _entered = false;
        Password = "";
        ShowError("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        Left?.Invoke();
    }

    // Đánh dấu đã vào app và báo App mở HomePage.
    private void Enter()
    {
        _entered = true;
        Entered?.Invoke();
    }

    // Đổi giữa tab Đăng nhập và Đăng ký trên AuthPage, xóa lỗi cũ.
    private Task SwitchAsync(bool register)
    {
        _isRegister = register;
        _errorMessage = null;
        foreach (var name in new[] { nameof(IsRegister), nameof(IsLogin), nameof(SubmitText), nameof(UsernameHint), nameof(HasError), nameof(ErrorMessage) }) Notify(name);
        return Task.CompletedTask;
    }

    // Kiểm tra form theo cùng quy tắc web (AccountController.Validate), gọi AuthApi đăng nhập / đăng ký, lưu token qua SessionManager rồi báo App chuyển sang Trang chủ.
    private async Task SubmitAsync()
    {
        var error = Validate();
        if (error is not null) { ShowError(error); return; }
        SetBusy(true);
        try
        {
            var tokens = _isRegister
                ? await _api.RegisterAsync(new RegisterRequest(Username.Trim(), Email.Trim(), Phone.Trim(), Password), default)
                : await _api.LoginAsync(new LoginRequest(Username.Trim(), Password), default);
            await _session.StartAsync(tokens);
            Password = "";
            Enter();
        }
        catch (AuthException ex) { ShowError(ex.Message); }
        finally { SetBusy(false); }
    }

    // Đăng nhập Google: khách hủy thì ở lại màn hình này, không báo lỗi.
    private async Task SignInWithGoogleAsync()
    {
        SetBusy(true);
        try
        {
            var tokens = await _google!.SignInAsync(default);
            if (tokens is null) return;
            await _session.StartAsync(tokens);
            Password = "";
            Enter();
        }
        catch (AuthException ex) { ShowError(ex.Message); }
        finally { SetBusy(false); }
    }

    // Vào app với tư cách khách: gửi thông tin thiết bị (DeviceIdentity) qua AuthApi.RegisterGuestAsync(), chờ tối đa 5 giây; gửi lỗi vẫn cho vào app.
    private async Task EnterAsGuestAsync()
    {
        SetBusy(true);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _api.RegisterGuestAsync(_device.Current(), timeout.Token);
        }
        catch (AuthException) { }
        catch (OperationCanceledException) { }
        finally { SetBusy(false); }
        Enter();
    }

    // Kiểm tra form theo quy tắc AccountController.Validate của web, thêm định dạng tên đăng nhập của API.
    private string? Validate()
    {
        var username = Username.Trim();
        if (username.Length == 0) return "Vui lòng nhập tên đăng nhập.";
        if (_isRegister)
        {
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]{3,30}$")) return "Tên đăng nhập gồm 3-30 chữ cái, chữ số hoặc dấu gạch dưới.";
            if (!Regex.IsMatch(Email.Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$")) return "Email chưa đúng định dạng.";
            if (!Regex.IsMatch(Phone.Trim(), @"^0\d{9}$")) return "Số điện thoại cần gồm 10 chữ số.";
        }
        return Password.Length < 6 ? "Mật khẩu cần có ít nhất 6 ký tự." : null;
    }

    // Hiện thông báo lỗi.
    private void ShowError(string message)
    {
        _errorMessage = message;
        Notify(nameof(ErrorMessage));
        Notify(nameof(HasError));
    }

    // Bật tắt trạng thái đang xử lý và cập nhật các nút trên AuthPage.
    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        if (busy) _errorMessage = null;
        foreach (var name in new[] { nameof(IsBusy), nameof(IsIdle), nameof(HasError), nameof(ErrorMessage) }) Notify(name);
        ShowLoginCommand.NotifyCanExecuteChanged();
        ShowRegisterCommand.NotifyCanExecuteChanged();
        SubmitCommand.NotifyCanExecuteChanged();
        GuestCommand.NotifyCanExecuteChanged();
        GoogleCommand.NotifyCanExecuteChanged();
    }

    // Báo giao diện cập nhật thuộc tính.
    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
