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

    public void Update(CartDto cart) => SetCount(Math.Max(0, cart.TotalQuantity));
    public void Clear() => SetCount(0);

    private void SetCount(int count)
    {
        if (_count == count) return;
        _count = count;
        foreach (var property in new[] { nameof(Count), nameof(DisplayCount), nameof(HasItems) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
