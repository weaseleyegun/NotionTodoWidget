using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NotionTodoWidget;

public class TodoItem : INotifyPropertyChanged
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Url { get; init; } = "";
    public string CreatedText { get; init; } = "";

    /// <summary>페이지 아이콘(이모지) + 공백. 아이콘이 없으면 빈 문자열.</summary>
    public string IconPrefix { get; init; } = "";

    public DateTime? Due { get; private set; }

    /// <summary>"3일 남음" / "오늘까지!" / "6일 지남"</summary>
    public string DueText { get; private set; } = "";

    /// <summary>"none" | "ok" | "today" | "over" — 태그 색상 결정</summary>
    public string DueKind { get; private set; } = "none";

    /// <summary>마감일을 바꾸고 관련 표시(DueText/DueKind)를 함께 갱신합니다.</summary>
    public void SetDue(DateTime? due)
    {
        Due = due?.Date;
        if (Due is DateTime d)
        {
            int diff = (d - DateTime.Today).Days;
            DueText = Strings.DueDays(diff);
            DueKind = diff > 0 ? "ok" : diff == 0 ? "today" : "over";
        }
        else
        {
            DueText = "";
            DueKind = "none";
        }
        OnPropertyChanged(nameof(Due));
        OnPropertyChanged(nameof(DueText));
        OnPropertyChanged(nameof(DueKind));
    }

    private string _detail = "";
    public string Detail
    {
        get => _detail;
        set
        {
            if (_detail == value) return;
            _detail = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasDetail));
            OnPropertyChanged(nameof(DetailPreview));
        }
    }

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    /// <summary>접힌 카드에 보이는 한 줄 미리보기 (비어 있으면 안내 문구)</summary>
    public string DetailPreview => HasDetail ? Detail : Strings.DetailPlaceholder;

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
