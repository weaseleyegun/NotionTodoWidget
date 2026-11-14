using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace NotionTodoWidget;

/// <summary>라이트/다크 팔레트를 애플리케이션 리소스(브러시)로 적용합니다.</summary>
public static class ThemeManager
{
    // 두 팔레트는 반드시 같은 키를 가져야 합니다.
    private static readonly Dictionary<string, string> Light = new()
    {
        ["PanelBrush"] = "#F7F6F3",
        ["SurfaceBrush"] = "#F7F6F3",
        ["PanelBorderBrush"] = "#E3E2DE",
        ["CardBrush"] = "#FFFFFF",
        ["CardHoverBrush"] = "#FBFBFA",
        ["CardBorderBrush"] = "#E4E3DF",
        ["TextBrush"] = "#37352F",
        ["SubTextBrush"] = "#787774",
        ["MutedBrush"] = "#A5A4A0",
        ["PillBrush"] = "#EFEEEC",
        ["TitlePillBrush"] = "#E3E2E0",
        ["IconBrush"] = "#787774",
        ["HoverOverlayBrush"] = "#14000000",
        ["InputBrush"] = "#FFFFFF",
        ["EditorBrush"] = "#FAFAF9",
        ["ThumbBrush"] = "#C9C8C4",
        ["CheckBorderBrush"] = "#C7C6C2",
        ["CheckBgBrush"] = "#FFFFFF",
        ["AccentBrush"] = "#2383E2",
        ["UndoBgBrush"] = "#37352F",
        ["UndoTextBrush"] = "#FFFFFF",
        ["UndoActionBrush"] = "#7CB9FF",
        ["TagBg"] = "#EFEEEC",
        ["TagFg"] = "#5F5E5B",
        ["TagOkBg"] = "#DBEDDB",
        ["TagOkFg"] = "#1C6B3A",
        ["TagTodayBg"] = "#FADEC9",
        ["TagTodayFg"] = "#B45309",
        ["TagOverBg"] = "#FFE2DD",
        ["TagOverFg"] = "#C4261D",
        ["ErrorBrush"] = "#B4541E",
        ["PopupBrush"] = "#FFFFFF",
        ["PopupBorderBrush"] = "#E4E3DF",
    };

    private static readonly Dictionary<string, string> Dark = new()
    {
        ["PanelBrush"] = "#1F1F1F",
        ["SurfaceBrush"] = "#1F1F1F",
        ["PanelBorderBrush"] = "#363636",
        ["CardBrush"] = "#2B2B2B",
        ["CardHoverBrush"] = "#323232",
        ["CardBorderBrush"] = "#3A3A3A",
        ["TextBrush"] = "#EDEDEC",
        ["SubTextBrush"] = "#A3A3A0",
        ["MutedBrush"] = "#737373",
        ["PillBrush"] = "#2F2F2F",
        ["TitlePillBrush"] = "#383838",
        ["IconBrush"] = "#A3A3A0",
        ["HoverOverlayBrush"] = "#22FFFFFF",
        ["InputBrush"] = "#2B2B2B",
        ["EditorBrush"] = "#242424",
        ["ThumbBrush"] = "#4D4D4D",
        ["CheckBorderBrush"] = "#5C5C5C",
        ["CheckBgBrush"] = "#2B2B2B",
        ["AccentBrush"] = "#2F8FEA",
        ["UndoBgBrush"] = "#EDEDEC",
        ["UndoTextBrush"] = "#1F1F1F",
        ["UndoActionBrush"] = "#1B6FCB",
        ["TagBg"] = "#383838",
        ["TagFg"] = "#C8C8C6",
        ["TagOkBg"] = "#1F3A2B",
        ["TagOkFg"] = "#7FD9A0",
        ["TagTodayBg"] = "#4A3420",
        ["TagTodayFg"] = "#F0A96B",
        ["TagOverBg"] = "#4D2A2A",
        ["TagOverFg"] = "#FF8F85",
        ["ErrorBrush"] = "#F2A072",
        ["PopupBrush"] = "#2B2B2B",
        ["PopupBorderBrush"] = "#454545",
    };

    /// <summary>Windows 앱 테마가 다크인지 확인합니다.</summary>
    public static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>현재 적용된 테마가 다크인지</summary>
    public static bool IsDark { get; private set; }

    /// <summary>팔레트를 적용합니다. opacity 는 패널/카드 배경에만 곱해집니다(글자는 그대로).</summary>
    public static void Apply(bool dark, double opacity)
    {
        opacity = Math.Clamp(opacity, 0.3, 1.0);
        IsDark = dark;
        var palette = dark ? Dark : Light;
        var resources = Application.Current.Resources;

        foreach (var (key, hex) in palette)
        {
            double o = key switch
            {
                "PanelBrush" => opacity,
                "CardBrush" or "CardHoverBrush" => Math.Min(1.0, opacity + 0.04),
                _ => 1.0
            };
            resources[key] = MakeBrush(hex, o);
        }
    }

    public static SolidColorBrush MakeBrush(string hex, double opacity = 1.0)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }

    // ---------- 창 제목표시줄 다크 모드 (설정 창용) ----------

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void ApplyTitleBar(Window window, bool dark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            int value = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref value, sizeof(int));   // DWMWA_USE_IMMERSIVE_DARK_MODE
        }
        catch (Exception)
        {
            // 지원하지 않는 Windows 버전이면 무시
        }
    }
}
