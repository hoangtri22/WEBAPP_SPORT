using SportShop.Mobile.ViewModels;

namespace SportShop.Mobile;

// Form trả lời khảo sát từng câu có thanh tiến độ như Views/Customer/Surveys.cshtml; nộp xong hiện lời cảm ơn; dùng SurveyFormViewModel.
public partial class SurveyFormPage : ContentPage
{
    private readonly SurveyFormViewModel _vm;

    // Nhận SurveyFormViewModel.
    public SurveyFormPage(SurveyFormViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Tải câu hỏi của khảo sát; không mở được thì hiện lỗi thay cho form.
    public async Task LoadAsync(int surveyId)
    {
        await _vm.LoadAsync(surveyId);
        LoadError.IsVisible = !_vm.HasLoaded && _vm.HasError;
    }

    // Chọn phương án qua SurveyQuestionItem.Choose().
    private void OnOptionTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is SurveyOptionItem option) _vm.Current?.Choose(option);
    }

    private void OnBackClicked(object? sender, EventArgs e) => _vm.Back();
    private async void OnNextClicked(object? sender, EventArgs e) => await _vm.NextAsync();
    private async void OnBackToListClicked(object? sender, EventArgs e) => await Navigation.PopAsync();
}
