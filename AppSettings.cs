using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NotionTodoWidget;

/// <summary>
/// 연결 설정. 우선순위:
///  1) %AppData%\NotionTodoWidget\settings.json  (설정 창에서 저장 — 저장소에 올라가지 않음)
///  2) 실행 파일 폴더 / 상위 폴더의 appsettings.json  (개발·휴대용 설정)
/// </summary>
public class AppSettings
{
    public string NotionToken { get; set; } = "";
    public string DatabaseId { get; set; } = "";
    public int RefreshSeconds { get; set; } = 60;

    /// <summary>true 면 notion:// 로 노션 데스크톱 앱에서 열고, 실패하면 브라우저로 엽니다.</summary>
    public bool OpenInNotionApp { get; set; } = true;

    /// <summary>"auto" | "ko" | "en"</summary>
    public string Language { get; set; } = "auto";

    /// <summary>실제로 읽은 설정 파일 경로 (문제 진단용)</summary>
    [JsonIgnore] public string LoadedFrom { get; private set; } = "";

    /// <summary>JSON 파싱 오류가 있었다면 그 내용</summary>
    [JsonIgnore] public string LoadError { get; private set; } = "";

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(NotionToken) && !string.IsNullOrWhiteSpace(DatabaseId);

    public static string UserFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NotionTodoWidget", "settings.json");

    private static AppSettings? _current;

    /// <summary>앱 전체에서 공유하는 설정 인스턴스</summary>
    public static AppSettings Current => _current ??= Load();

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static AppSettings Load()
    {
        AppSettings? firstFound = null;
        string error = "";

        foreach (var path in Candidates())
        {
            try
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), ReadOptions);
                if (s is null) continue;

                s.LoadedFrom = path;
                firstFound ??= s;
                if (s.IsConfigured) return s;
            }
            catch (Exception ex)
            {
                error = $"{path}: {ex.Message}";
            }
        }

        if (firstFound is not null) return firstFound;
        return new AppSettings { LoadedFrom = "(settings file not found)", LoadError = error };
    }

    /// <summary>사용자 설정 파일(%AppData%)에 저장합니다.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UserFilePath)!);
        File.WriteAllText(UserFilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        LoadedFrom = UserFilePath;
    }

    private static IEnumerable<string> Candidates()
    {
        if (File.Exists(UserFilePath)) yield return UserFilePath;

        var dirs = new List<string>();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
            dirs.Add(dir.FullName);
        dirs.Add(Directory.GetCurrentDirectory());

        foreach (var d in dirs.Distinct())
        {
            var p = Path.Combine(d, "appsettings.json");
            if (File.Exists(p)) yield return p;
        }
    }
}
