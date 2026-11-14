using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace NotionTodoWidget;

/// <summary>토큰·데이터베이스·언어 등을 설정하고 연결을 테스트합니다.</summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        TokenBox.Password = settings.NotionToken;
        DbBox.Text = settings.DatabaseId;
        RefreshBox.Text = settings.RefreshSeconds.ToString();
        OpenInAppCheck.IsChecked = settings.OpenInNotionApp;
        try { AutoStartCheck.IsChecked = AutoStart.IsEnabled(); } catch (Exception) { }

        switch ((settings.Language ?? "auto").ToLowerInvariant())
        {
            case "ko": LangKo.IsChecked = true; break;
            case "en": LangEn.IsChecked = true; break;
            default: LangAuto.IsChecked = true; break;
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
        => ThemeManager.ApplyTitleBar(this, ThemeManager.IsDark);

    private void Integrations_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.notion.so/profile/integrations") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowResult(Strings.TestFail(ex.Message), "ErrorBrush");
        }
    }

    private bool TryRead(out string token, out string dbId)
    {
        token = TokenBox.Password.Trim();
        dbId = "";

        if (token.Length == 0)
        {
            ShowResult(Strings.NeedToken, "ErrorBrush");
            return false;
        }

        var parsed = NotionIds.ParseDatabaseId(DbBox.Text);
        if (parsed is null)
        {
            ShowResult(Strings.BadDbId, "ErrorBrush");
            return false;
        }

        dbId = parsed;
        return true;
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(out var token, out var dbId)) return;

        ShowResult(Strings.Testing, "SubTextBrush");
        TestBtn.IsEnabled = false;
        try
        {
            var info = await new NotionClient(token, dbId).GetDatabaseInfoAsync();
            var text = Strings.TestOk(info.Title);
            if (info.Problems.Count > 0) text += "\n" + string.Join("\n", info.Problems);
            ShowResult(text, info.Problems.Count > 0 ? "ErrorBrush" : "TagOkFg");
        }
        catch (Exception ex)
        {
            ShowResult(Strings.TestFail(ex.Message), "ErrorBrush");
        }
        finally
        {
            TestBtn.IsEnabled = true;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryRead(out var token, out var dbId)) return;

        int seconds = int.TryParse(RefreshBox.Text.Trim(), out var n) ? n : 60;

        _settings.NotionToken = token;
        _settings.DatabaseId = dbId;
        _settings.RefreshSeconds = Math.Max(15, seconds);
        _settings.OpenInNotionApp = OpenInAppCheck.IsChecked == true;
        _settings.Language = LangKo.IsChecked == true ? "ko" : LangEn.IsChecked == true ? "en" : "auto";

        try
        {
            AutoStart.Set(AutoStartCheck.IsChecked == true);
        }
        catch (Exception ex)
        {
            ShowResult(Strings.ErrAutoStart(ex.Message), "ErrorBrush");
            return;
        }

        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            ShowResult(Strings.TestFail(ex.Message), "ErrorBrush");
            return;
        }

        DialogResult = true;
    }

    private void ShowResult(string text, string brushKey)
    {
        ResultText.Text = text;
        ResultText.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
    }
}
