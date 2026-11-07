using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SportShop.Contracts.Customer;
using SportShop.Mobile.Services;

namespace SportShop.Mobile.ViewModels;

// Quên mật khẩu hai bước: gửi OTP về email, rồi nhập mã và mật khẩu mới.
public sealed class ForgotPasswordViewModel : INotifyPropertyChanged
{
    private readonly IPasswordResetApi _api;
    private bool _isBusy;
    private bool _codeSent;
    private string? _errorMessage;
    private string _email = "";
    private string _code = "";
    private string _newPassword = "";
    private string _confirmPassword = "";

    // Nhận IPasswordResetApi.
    public ForgotPasswordViewModel(IPasswordResetApi api)
    {
        _api = api;
        SendCodeCommand = new AsyncCommand(SendCodeAsync, () => IsIdle);
        ResetCommand = new AsyncCommand(ResetAsync, () => IsIdle);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Completed;
    public AsyncCommand SendCodeCommand { get; }
    public AsyncCommand ResetCommand { get; }
    public bool IsBusy => _isBusy;
    public bool IsIdle => !_isBusy;
    public bool CodeSent => _codeSent;
    public bool NeedsEmail => !_codeSent;
    public bool HasError => !string.IsNullOrEmpty(_errorMessage);
    public string? ErrorMessage => _errorMessage;
    // API không cho biết email có tồn tại hay không, để người khác không dò được tài khoản.
    public string SentText => $"Nếu {Email.Trim()} thuộc một tài khoản, mã 6 số đã được gửi tới email này. Mã có hiệu lực 10 phút.";

    public string Email { get => _email; set { _email = value; Notify(); } }
    public string Code { get => _code; set { _code = value; Notify(); } }
    public string NewPassword { get => _newPassword; set { _newPassword = value; Notify(); } }
    public string ConfirmPassword { get => _confirmPassword; set { _confirmPassword = value; Notify(); } }

    // Gửi mã về email.
    private async Task SendCodeAsync()
    {
        if (!Regex.IsMatch(Email.Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$")) { ShowError("Email chưa đúng định dạng."); return; }
        SetBusy(true);
        try
        {
            await _api.SendCodeAsync(Email.Trim(), default);
            _codeSent = true;
            foreach (var name in new[] { nameof(CodeSent), nameof(NeedsEmail), nameof(SentText) }) Notify(name);
        }
        catch (AuthException ex) { ShowError(ex.Message); }
        finally { SetBusy(false); }
    }

    // Đặt lại mật khẩu bằng mã.
    private async Task ResetAsync()
    {
        var error = Validate();
        if (error is not null) { ShowError(error); return; }
        SetBusy(true);
        try
        {
            await _api.ResetAsync(new ResetPasswordRequest(Email.Trim(), Code.Trim(), NewPassword), default);
            Code = NewPassword = ConfirmPassword = "";
            Completed?.Invoke();
        }
        catch (AuthException ex) { ShowError(ex.Message); }
        finally { SetBusy(false); }
    }

    // Kiểm tra mã 6 số và mật khẩu mới, trả về thông báo lỗi nếu sai.
    private string? Validate()
    {
        if (!Regex.IsMatch(Code.Trim(), @"^\d{6}$")) return "Mã xác nhận gồm 6 chữ số.";
        if (NewPassword.Length < 6) return "Mật khẩu mới cần có ít nhất 6 ký tự.";
        return NewPassword == ConfirmPassword ? null : "Mật khẩu xác nhận chưa khớp.";
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
        SendCodeCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
    }

    // Báo giao diện cập nhật thuộc tính.
    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
