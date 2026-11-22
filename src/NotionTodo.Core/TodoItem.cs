using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NotionTodoWidget;

public class TodoItem : INotifyPropertyChanged
{
    public string Id { get; init; } = "";
    private string _title = "";
    public string Title { get => _title; set { if (_title == value) return; _title = value; OnPropertyChanged(); } }

    private string _url = "";
    public string Url { get => _url; set { if (_url == value) return; _url = value; OnPropertyChanged(); } }

    private string _createdText = "";
    public string CreatedText { get => _createdText; set { if (_createdText == value) return; _createdText = value; OnPropertyChanged(); } }

    private string _iconPrefix = "";
    /// <summary>페이지 아이콘(이모지) + 공백. 아이콘이 없으면 빈 문자열.</summary>
    public string IconPrefix { get => _iconPrefix; set { if (_iconPrefix == value) return; _iconPrefix = value; OnPropertyChanged(); } }

    /// <summary>
    /// 서버에서 새로 받은 같은 항목(같은 Id)의 값으로 이 인스턴스를 갱신합니다.
    /// 펼쳐진 카드는 편집 중일 수 있으므로 세부내용(Detail)은 덮어쓰지 않습니다.
    /// </summary>
    public void UpdateFrom(TodoItem fresh)
    {
        Title = fresh.Title;
        Url = fresh.Url;
        CreatedText = fresh.CreatedText;
        IconPrefix = fresh.IconPrefix;
        if (!IsExpanded) Detail = fresh.Detail;
        SetDue(fresh.Due);
    }

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

    public TodoSnapshot ToSnapshot()
        => new(Id, Title, Url, CreatedText, IconPrefix, Detail, Due);

    public static TodoItem FromSnapshot(TodoSnapshot s)
    {
        var item = new TodoItem
        {
            Id = s.Id, Title = s.Title, Url = s.Url,
            CreatedText = s.CreatedText, IconPrefix = s.IconPrefix, Detail = s.Detail
        };
        item.SetDue(s.Due);
        return item;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
