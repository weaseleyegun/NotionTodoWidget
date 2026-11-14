using System;
using System.IO;
using System.Text.Json;

namespace NotionTodoWidget;

/// <summary>투명도·위치·크기·고정 여부·테마를 %AppData%\NotionTodoWidget\state.json 에 저장합니다.</summary>
public class WidgetState
{
    public double Opacity { get; set; } = 0.92;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }

    /// <summary>true 면 다른 창 위에 항상 표시, false 면 바탕화면 맨 뒤</summary>
    public bool Pinned { get; set; }

    /// <summary>true = 다크, false = 라이트, null = Windows 설정을 따름</summary>
    public bool? Dark { get; set; }

    private static WidgetState? _current;

    /// <summary>앱 전체에서 공유하는 상태 인스턴스</summary>
    public static WidgetState Current => _current ??= Load();

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NotionTodoWidget", "state.json");

    public static WidgetState Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<WidgetState>(File.ReadAllText(FilePath));
                if (s is not null)
                {
                    s.Opacity = Math.Clamp(s.Opacity, 0.3, 1.0);
                    return s;
                }
            }
        }
        catch (Exception)
        {
            // 상태 파일이 깨졌으면 기본값 사용
        }
        return new WidgetState();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch (Exception)
        {
            // 저장 실패는 무시 (위젯 동작에는 영향 없음)
        }
    }
}
