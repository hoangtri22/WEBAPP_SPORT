namespace SportShop.Mobile.Controls;

// Logo SPORTSHOP giống .logo của web khách (Views/Shared/_SiteHeader.cshtml): ô vuông teal có icon activity và chữ SPORT, SHOP.
public sealed class BrandLogo : HorizontalStackLayout
{
    public static readonly BindableProperty LightProperty =
        BindableProperty.Create(nameof(Light), typeof(bool), typeof(BrandLogo), false, propertyChanged: (b, _, _) => ((BrandLogo)b).Build());

    // Light = true dùng trên nền teal: ô vuông trắng, chữ trắng như .logo-white.
    public bool Light { get => (bool)GetValue(LightProperty); set => SetValue(LightProperty, value); }

    // Dựng logo với khoảng cách và mô tả cho trình đọc màn hình.
    public BrandLogo()
    {
        Spacing = 10;
        VerticalOptions = LayoutOptions.Center;
        SemanticProperties.SetDescription(this, "SPORTSHOP");
        Build();
    }

    // Dựng lại ô icon và chữ theo Light.
    private void Build()
    {
        Children.Clear();
        var teal = (Color)Application.Current!.Resources["Teal"];
        var ink = (Color)Application.Current.Resources["Ink"];
        Children.Add(new Border
        {
            WidthRequest = 34, HeightRequest = 34, StrokeThickness = 0, Padding = 7,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            BackgroundColor = Light ? Colors.White : teal,
            Content = new Image { Source = Light ? "ic_activity_teal.png" : "ic_activity_white.png" },
        });
        Children.Add(new Label
        {
            VerticalOptions = LayoutOptions.Center, FontFamily = "OpenSansSemibold", FontSize = 20, CharacterSpacing = -0.4,
            FormattedText = new FormattedString
            {
                Spans =
                {
                    new Span { Text = "SPORT", TextColor = Light ? Colors.White : ink },
                    new Span { Text = "SHOP", TextColor = Light ? Colors.White : teal },
                },
            },
        });
    }
}
