using NotionTodoWidget;

namespace NotionTodo.Mobile;

public partial class SetupPage : ContentPage
{
    private static readonly string[] LangCodes = { "auto", "ko", "en" };

    public SetupPage()
    {
        InitializeComponent();
        TitleLabel.Text = Strings.SettingsTitle;
        IntroLabel.Text = Strings.SetupIntro;
        IntegrationsButton.Text = Strings.OpenIntegrations;
        TokenLabel.Text = Strings.TokenLabel;
        DbLabel.Text = Strings.DbLabel;
        DbHint.Text = Strings.DbHint;
        LanguageLabel.Text = Strings.LanguageLabel;
        LanguageNote.Text = Strings.LanguageNote;
        TestButton.Text = Strings.BtnTest;
        SaveButton.Text = Strings.BtnSave;
        LanguagePicker.ItemsSource = new[] { Strings.LanguageAuto, "한국어", "English" };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        TokenEntry.Text = await MobileSettings.GetTokenAsync() ?? "";
        DbEntry.Text = MobileSettings.DatabaseId;
        LanguagePicker.SelectedIndex = Math.Max(0, Array.IndexOf(LangCodes, MobileSettings.Language));
    }

    private async void OnOpenIntegrations(object? sender, EventArgs e)
    {
        try { await Launcher.OpenAsync("https://www.notion.so/profile/integrations"); }
        catch (Exception ex) { ShowResult(Strings.ErrOpen(ex.Message), ok: false); }
    }

    /// <summary>입력값을 정리해 (토큰, DB ID) 로 돌려줍니다. 잘못되면 화면에 안내하고 null.</summary>
    private (string token, string dbId)? ReadInputs()
    {
        var token = (TokenEntry.Text ?? "").Trim();
        if (token.Length == 0) { ShowResult(Strings.NeedToken, ok: false); return null; }

        var dbId = NotionIds.ParseDatabaseId(DbEntry.Text);
        if (dbId is null) { ShowResult(Strings.BadDbId, ok: false); return null; }

        return (token, dbId);
    }

    private async void OnTest(object? sender, EventArgs e)
    {
        if (ReadInputs() is not var (token, dbId)) return;

        TestButton.IsEnabled = false;
        ShowResult(Strings.Testing, ok: true);
        try
        {
            var info = await new NotionClient(token, dbId).GetDatabaseInfoAsync();
            var text = Strings.TestOk(info.Title);
            if (info.Problems.Count > 0) text += "\n" + string.Join("\n", info.Problems);
            ShowResult(text, ok: true);
        }
        catch (Exception ex)
        {
            ShowResult(Strings.TestFail(ex.Message), ok: false);
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private async void OnSave(object? sender, EventArgs e)
    {
        if (ReadInputs() is not var (token, dbId)) return;

        try
        {
            await MobileSettings.SetTokenAsync(token);
            MobileSettings.DatabaseId = dbId;
            MobileSettings.Language = LangCodes[Math.Max(0, LanguagePicker.SelectedIndex)];
            Strings.Init(MobileSettings.Language);

            await Shell.Current.GoToAsync("//TodoPage");
        }
        catch (Exception ex)
        {
            // 키스토어 오류 등으로 저장이 실패해도 앱이 종료되지 않게 합니다.
            ShowResult(Strings.TestFail(ex.Message), ok: false);
        }
    }

    private void ShowResult(string text, bool ok)
    {
        ResultLabel.Text = text;
        ResultLabel.TextColor = ok ? Color.FromArgb("#2E9D5B") : Color.FromArgb("#D64545");
    }
}
