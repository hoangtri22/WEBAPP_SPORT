using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Danh sách khảo sát giống Views/Customer/Surveys.cshtml; mở SurveyFormPage để trả lời khảo sát đang diễn ra.
public partial class SurveysPage : ContentPage
{
    private readonly SurveysViewModel _vm;
    private readonly IServiceProvider _services;
    private bool _opening;

    // Nhận SurveysViewModel và bộ dịch vụ để mở SurveyFormPage.
    public SurveysPage(SurveysViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _services = services;
    }

    // Tải lại mỗi lần hiện để khảo sát vừa nộp chuyển sang Đã hoàn thành.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e) => await _vm.LoadAsync();

    // Mở form trả lời của khảo sát đã chọn.
    private async void OnAnswerClicked(object? sender, EventArgs e)
    {
        if (_opening || sender is not Button { CommandParameter: SurveyCard { CanAnswer: true } card }) return;
        _opening = true;
        try
        {
            var page = _services.GetRequiredService<SurveyFormPage>();
            await Navigation.PushAsync(page);
            await page.LoadAsync(card.Id);
        }
        finally { _opening = false; }
    }
}
