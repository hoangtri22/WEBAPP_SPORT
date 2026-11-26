using System.Windows.Input;

namespace SportShop.Mobile.Controls;

// Khối trạng thái trống hoặc lỗi dùng chung: icon tròn, tiêu đề, mô tả và một nút hành động.
public sealed class EmptyState : VerticalStackLayout
{
    public static readonly BindableProperty IconProperty = Create<ImageSource?>(nameof(Icon), null);
    public static readonly BindableProperty TitleProperty = Create(nameof(Title), "");
    public static readonly BindableProperty MessageProperty = Create(nameof(Message), "");
    public static readonly BindableProperty ActionTextProperty = Create(nameof(ActionText), "");
    public static readonly BindableProperty ActionCommandProperty = Create<ICommand?>(nameof(ActionCommand), null);
    public static readonly BindableProperty IsErrorProperty = Create(nameof(IsError), false);

    private readonly Border _iconBox;
    private readonly Image _icon = new() { WidthRequest = 30, HeightRequest = 30 };
    private readonly Label _title = new() { FontFamily = "OpenSansSemibold", FontSize = 18, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label _message = new() { FontSize = 13, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Button _action = new() { HorizontalOptions = LayoutOptions.Center, Padding = new Thickness(24, 0), HeightRequest = 48, CornerRadius = 14 };

    public event EventHandler? ActionClicked;

    public ImageSource? Icon { get => (ImageSource?)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public string ActionText { get => (string)GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }
    public ICommand? ActionCommand { get => (ICommand?)GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }
    // IsError = true đổi nền icon sang màu cam nhạt để phân biệt lỗi với trạng thái trống.
    public bool IsError { get => (bool)GetValue(IsErrorProperty); set => SetValue(IsErrorProperty, value); }

    // Dựng icon, tiêu đề, mô tả, nút; nút gọi cả sự kiện ActionClicked và ActionCommand.
    public EmptyState()
    {
        Spacing = 10;
        Padding = new Thickness(24, 32);
        HorizontalOptions = LayoutOptions.Fill;
        _iconBox = new Border
        {
            StrokeThickness = 0, Padding = 18, HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 33 },
            Content = _icon,
        };
        _title.TextColor = Resource("Ink");
        _message.TextColor = Resource("Muted");
        _action.Clicked += (_, e) =>
        {
            ActionClicked?.Invoke(this, e);
            if (ActionCommand?.CanExecute(null) == true) ActionCommand.Execute(null);
        };
        Children.Add(_iconBox);
        Children.Add(_title);
        Children.Add(_message);
        Children.Add(_action);
        Refresh();
    }

    // Đồng bộ control con với các thuộc tính hiện tại.
    private void Refresh()
    {
        _icon.Source = Icon;
        _iconBox.IsVisible = Icon is not null;
        _iconBox.BackgroundColor = Resource(IsError ? "OrangeSoft" : "TealSoft");
        _title.Text = Title;
        _message.Text = Message;
        _message.IsVisible = !string.IsNullOrWhiteSpace(Message);
        _action.Text = ActionText;
        _action.IsVisible = !string.IsNullOrWhiteSpace(ActionText);
    }

    private static Color Resource(string key) => (Color)Application.Current!.Resources[key];

    private static BindableProperty Create<T>(string name, T defaultValue) =>
        BindableProperty.Create(name, typeof(T), typeof(EmptyState), defaultValue, propertyChanged: (b, _, _) => ((EmptyState)b).Refresh());
}
