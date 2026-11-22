using System.Globalization;
using NotionTodoWidget;

namespace NotionTodo.Mobile;

public sealed class InvertBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is bool b && b);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>null/빈 문자열이면 false, 아니면 true (IsVisible 용)</summary>
public sealed class NotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && s.Length > 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>DueKind("none"|"ok"|"today"|"over") → 칩 배경색</summary>
public sealed class DueKindColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value as string) switch
        {
            "ok" => Color.FromArgb("#2E9D5B"),
            "today" => Color.FromArgb("#E8A317"),
            "over" => Color.FromArgb("#D64545"),
            _ => Colors.Transparent
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>연결 상태 → 상태 알약 색 (🟢 연결됨 · 🟡 동기화 중 · 🔴 실패 · ⚪ 설정 필요)</summary>
public sealed class StateColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ConnState s
            ? s switch
            {
                ConnState.Connected => Color.FromArgb("#2E9D5B"),
                ConnState.Syncing => Color.FromArgb("#E8A317"),
                ConnState.Failed => Color.FromArgb("#D64545"),
                _ => Color.FromArgb("#888888")
            }
            : Colors.Gray;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
