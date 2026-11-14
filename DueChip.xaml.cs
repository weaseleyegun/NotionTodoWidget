using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NotionTodoWidget;

/// <summary>마감일 선택 칩: 누르면 달력 팝업(월 이동, 빠른 선택, 지우기)이 열립니다.</summary>
public partial class DueChip : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(DateTime?), typeof(DueChip),
        new FrameworkPropertyMetadata(null, OnValueChanged));

    /// <summary>선택된 날짜 (없으면 null)</summary>
    public DateTime? Value
    {
        get => (DateTime?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>사용자가 날짜를 직접 고르거나 지웠을 때만 발생합니다(코드로 Value 를 바꿀 때는 발생하지 않음).</summary>
    public event EventHandler? DateChanged;

    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public DueChip()
    {
        InitializeComponent();

        foreach (var name in Strings.Weekdays)
        {
            var tb = new TextBlock
            {
                Text = name,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            WeekRow.Children.Add(tb);
        }

        AddQuick(Strings.Today, () => DateTime.Today);
        AddQuick(Strings.Tomorrow, () => DateTime.Today.AddDays(1));
        AddQuick(Strings.NextWeek, () => DateTime.Today.AddDays(7));
        AddQuick(Strings.ClearDue, () => null);

        Pop.MouseLeftButtonUp += (_, e) => e.Handled = true;   // 팝업 클릭이 카드 접기/펼치기로 번지지 않게
        UpdateText();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DueChip)d).UpdateText();

    private void UpdateText()
    {
        if (ChipText is null) return;

        if (Value is DateTime d)
        {
            ChipText.Text = d.ToString(Strings.DateChipFormat, Strings.Culture);
            ChipText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        }
        else
        {
            ChipText.Text = Strings.NoDue;
            ChipText.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
        }
    }

    private void AddQuick(string text, Func<DateTime?> pick)
    {
        var b = new Button
        {
            Content = text,
            Style = (Style)FindResource("ChipButton"),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 6, 0),
            FontSize = 12
        };
        b.Click += (_, _) => Select(pick());
        QuickRow.Children.Add(b);
    }

    private void Select(DateTime? date)
    {
        SetCurrentValue(ValueProperty, date?.Date);
        Pop.IsOpen = false;
        DateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Chip_Click(object sender, RoutedEventArgs e)
    {
        if (Pop.IsOpen)
        {
            Pop.IsOpen = false;
            return;
        }

        var basis = Value ?? DateTime.Today;
        _month = new DateTime(basis.Year, basis.Month, 1);
        BuildDays();
        Pop.IsOpen = true;
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        _month = _month.AddMonths(-1);
        BuildDays();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _month = _month.AddMonths(1);
        BuildDays();
    }

    private void BuildDays()
    {
        MonthText.Text = _month.ToString(Strings.MonthTitleFormat, Strings.Culture);
        DayGrid.Children.Clear();

        var start = _month.AddDays(-(int)_month.DayOfWeek);   // 일요일 시작, 6주(42칸)
        var selected = Value?.Date;
        var today = DateTime.Today;
        var dayStyle = (Style)FindResource("DayButton");

        for (int i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            bool inMonth = date.Month == _month.Month;

            var b = new Button
            {
                Content = date.Day.ToString(),
                Style = dayStyle,
                Tag = date,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 1)
            };

            if (selected == date)
            {
                b.SetResourceReference(Control.BackgroundProperty, "AccentBrush");
                b.Foreground = Brushes.White;
            }
            else
            {
                b.SetResourceReference(Control.ForegroundProperty, inMonth ? "TextBrush" : "MutedBrush");
                if (date == today) b.SetResourceReference(Control.BorderBrushProperty, "AccentBrush");
            }

            b.Click += Day_Click;
            DayGrid.Children.Add(b);
        }
    }

    private void Day_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DateTime date }) Select(date);
    }
}
