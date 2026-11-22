using NotionTodoWidget;

namespace NotionTodo.Mobile;

public partial class TodoPage : ContentPage
{
    public TodoPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await MobileSettings.IsConfiguredAsync())
        {
            await Shell.Current.GoToAsync("//SetupPage");
            return;
        }
        PlaceholderLabel.Text = Strings.HeaderFallback;   // Task 5 에서 실제 목록으로 교체
    }
}
