using System.Windows;

namespace NotionTodoWidget;

public partial class App : Application
{
    private void App_Startup(object sender, StartupEventArgs e)
    {
        // 언어 → 테마(브러시 리소스) → 창 순서로 준비합니다.
        Strings.Init(AppSettings.Current.Language);

        var state = WidgetState.Current;
        bool dark = state.Dark ?? ThemeManager.SystemPrefersDark();
        ThemeManager.Apply(dark, state.Opacity);

        new MainWindow().Show();
    }
}
