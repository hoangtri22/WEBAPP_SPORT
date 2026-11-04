using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

public sealed class AuthViewModel : INotifyPropertyChanged
{
    private readonly IAuthApi _api;
    private readonly ISessionStore _session;
    private readonly IDeviceIdentity _device;
    private bool _isBusy;
    private bool _isRegister;
    private bool _restored;
    private string _username = "";
    private string _email = "";
    private string _phone = "";
    private string _password = "";
    private string? _errorMessage;

    public AuthViewModel(IAuthApi api, ISessionStore session, IDeviceIdentity device)
    {
        _api = api;
        _session = session;
        _device = device;
        ShowLoginCommand = new AsyncCommand(() => SwitchAsync(false), () => IsIdle);
        ShowRegisterCommand = new AsyncCommand(() => SwitchAsync(true), () => IsIdle);
        SubmitCommand = new AsyncCommand(SubmitAsync, () => IsIdle);
        GuestCommand = new AsyncCommand(EnterAsGuestAsync, () => IsIdle);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Entered;
    public event Action? Left;
    public AsyncCommand ShowLoginCommand { get; }
    public AsyncCommand ShowRegisterCommand { get; }
    public AsyncCommand SubmitCommand { get; }
    public AsyncCommand GuestCommand { get; }
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

    // Mở app: nếu máy còn refresh token (SessionStore) thì gọi AuthApi.RefreshAsync() để vào thẳng Trang chủ; token bị API từ chối thì xóa.
    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (_restored) return;
        _restored = true;
        var refreshToken = await _session.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken)) return;
        SetBusy(true);
        try
        {
            await _session.SaveAsync(await _api.RefreshAsync(refreshToken, cancellationToken));
            Entered?.Invoke();
        }
        catch (AuthException ex) { if (ex.Rejected) _session.Clear(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { SetBusy(false); }
    }

    // Đăng xuất: thu hồi phiên qua AuthApi.LogoutAsync() (lỗi mạng vẫn xóa token trong máy) rồi về màn hình ngoài cùng; khách chỉ quay về màn hình này.
    public async Task LeaveAsync()
    {
        var accessToken = _session.IsSignedIn ? await _session.GetAccessTokenAsync() : null;
        if (!string.IsNullOrEmpty(accessToken))
            try { await _api.LogoutAsync(accessToken, default); }
            catch (AuthException) { }
        _session.Clear();
        Username = Email = Phone = Password = "";
        Left?.Invoke();
    }

    private Task SwitchAsync(bool register)
    {
        _isRegister = register;
        _errorMessage = null;
        foreach (var name in new[] { nameof(IsRegister), nameof(IsLogin), nameof(SubmitText), nameof(UsernameHint), nameof(HasError), nameof(ErrorMessage) }) Notify(name);
        return Task.CompletedTask;
    }

    // Kiểm tra form theo cùng quy tắc web (AccountController.Validate), gọi AuthApi đăng nhập / đăng ký, lưu token vào SessionStore rồi báo App chuyển sang Trang chủ.
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
            await _session.SaveAsync(tokens);
            Password = "";
            Entered?.Invoke();
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
        Entered?.Invoke();
    }

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

    private void ShowError(string message)
    {
        _errorMessage = message;
        Notify(nameof(ErrorMessage));
        Notify(nameof(HasError));
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        if (busy) _errorMessage = null;
        foreach (var name in new[] { nameof(IsBusy), nameof(IsIdle), nameof(HasError), nameof(ErrorMessage) }) Notify(name);
        ShowLoginCommand.NotifyCanExecuteChanged();
        ShowRegisterCommand.NotifyCanExecuteChanged();
        SubmitCommand.NotifyCanExecuteChanged();
        GuestCommand.NotifyCanExecuteChanged();
    }

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
