namespace NotionTodo.Mobile;

/// <summary>
/// 폰 설정 저장소. 토큰은 SecureStorage(Android Keystore)에만 두고, 나머지는 Preferences.
/// 토큰은 어떤 로그/메시지에도 출력하지 않습니다.
/// </summary>
public static class MobileSettings
{
    private const string TokenKey = "notion_token";

    public static async Task<string?> GetTokenAsync()
    {
        try { return await SecureStorage.Default.GetAsync(TokenKey); }
        catch { return null; }   // 키스토어 손상 등: 미설정으로 취급 → 설정 화면에서 재입력
    }

    public static Task SetTokenAsync(string token)
    {
        token = token.Trim();
        if (token.Length == 0)
        {
            SecureStorage.Default.Remove(TokenKey);
            return Task.CompletedTask;
        }
        return SecureStorage.Default.SetAsync(TokenKey, token);
    }

    public static string DatabaseId
    {
        get => Preferences.Default.Get("database_id", "");
        set => Preferences.Default.Set("database_id", value);
    }

    /// <summary>"auto" | "ko" | "en"</summary>
    public static string Language
    {
        get => Preferences.Default.Get("language", "auto");
        set => Preferences.Default.Set("language", value);
    }

    public static int RefreshSeconds
    {
        get => Preferences.Default.Get("refresh_seconds", 60);
        set => Preferences.Default.Set("refresh_seconds", Math.Max(15, value));
    }

    public static async Task<bool> IsConfiguredAsync()
        => !string.IsNullOrWhiteSpace(await GetTokenAsync()) && !string.IsNullOrWhiteSpace(DatabaseId);
}
