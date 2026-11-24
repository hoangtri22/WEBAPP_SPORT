using System.ComponentModel;
using System.Globalization;
using SportShop.Contracts.Commerce;

namespace SportShop.Mobile.ViewModels;

// Tổng số lượng trong giỏ dùng chung cho các trang; chỉ thay đổi sau phản hồi thành công.
public sealed class CartBadgeState : INotifyPropertyChanged
{
    private int _count;

    public event PropertyChangedEventHandler? PropertyChanged;
    public int Count => _count;
    public bool HasItems => _count > 0;
    public string DisplayCount => _count > 99 ? "99+" : _count.ToString(CultureInfo.InvariantCulture);

    // Cập nhật theo CartDto.TotalQuantity của API.
    public void Update(CartDto cart) => SetCount(Math.Max(0, cart.TotalQuantity));
    // Đặt về 0 khi giỏ trống hoặc rời phiên.
    public void Clear() => SetCount(0);

    // Đổi số lượng và báo giao diện, chỉ khi giá trị khác.
    private void SetCount(int count)
    {
        if (_count == count) return;
        _count = count;
        foreach (var property in new[] { nameof(Count), nameof(DisplayCount), nameof(HasItems) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
