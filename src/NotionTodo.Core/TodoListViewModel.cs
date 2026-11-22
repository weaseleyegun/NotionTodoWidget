using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NotionTodoWidget;

public enum ConnState { NeedSetup, Syncing, Connected, Failed }

/// <summary>
/// 목록 화면의 상태와 동작. UI 프레임워크에 의존하지 않습니다.
/// 낙관적 갱신(UI 먼저 → API → 실패 시 롤백)이며, API 오류는 예외로 던지지 않고 <see cref="ErrorMessage"/> 로 알립니다.
/// </summary>
public sealed class TodoListViewModel : INotifyPropertyChanged
{
    private readonly INotionApi? _api;
    private readonly ITodoCache _cache;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private Func<Task>? _undo;
    private readonly HashSet<string> _pending = new();   // 서버 반영 중인 완료/삭제 항목 (새로고침에 되살아나지 않게)
    private bool _adding;

    public TodoListViewModel(INotionApi? api, ITodoCache cache)
    {
        _api = api;
        _cache = cache;
        State = api is null ? ConnState.NeedSetup : ConnState.Syncing;
        StatusText = api is null ? Strings.ConnNeedSetup : Strings.ConnChecking;
    }

    public ObservableCollection<TodoItem> Items { get; } = new();

    private ConnState _state;
    public ConnState State { get => _state; private set => Set(ref _state, value); }

    private string _statusText = "";
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

    private string? _error;
    private bool _errorIsFromLoad;
    public string? ErrorMessage { get => _error; private set { _errorIsFromLoad = false; Set(ref _error, value); } }

    private string? _undoText;
    public string? UndoText { get => _undoText; private set => Set(ref _undoText, value); }

    // ---------- 조회 ----------

    public async Task LoadAsync()
    {
        if (_api is null)
        {
            State = ConnState.NeedSetup;
            StatusText = Strings.ConnNeedSetup;
            return;
        }

        await _loadLock.WaitAsync();
        try
        {
            if (Items.Count == 0)
            {
                foreach (var s in await _cache.LoadAsync()) Items.Add(TodoItem.FromSnapshot(s));
            }

            State = ConnState.Syncing;
            StatusText = Strings.ConnSyncing;
            try
            {
                var fresh = await _api.GetOpenTodosAsync();
                MergeInto(fresh);
                State = ConnState.Connected;
                StatusText = Strings.ConnOk(DateTime.Now.ToString("HH:mm"));
                if (_errorIsFromLoad) ErrorMessage = null;   // 동작 실패 오류는 새로고침으로 지우지 않습니다
                await SaveCacheAsync();
            }
            catch (Exception ex)
            {
                State = ConnState.Failed;
                StatusText = Strings.ConnFailed;
                if (ErrorMessage is null || _errorIsFromLoad)   // 직전 동작 오류를 덮어쓰지 않습니다
                {
                    ErrorMessage = Strings.ErrLoad(ex.Message);
                    _errorIsFromLoad = true;
                }
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    // ---------- 변경 ----------

    public async Task CompleteAsync(TodoItem item)
    {
        if (_api is null) return;
        ErrorMessage = null;
        int index = Items.IndexOf(item);
        if (index >= 0) Items.RemoveAt(index);
        _pending.Add(item.Id);
        try
        {
            await _api.SetDoneAsync(item.Id, true);
            _pending.Remove(item.Id);
            OfferUndo(Strings.UndoDone(item.Title), () => _api.SetDoneAsync(item.Id, false));
            await SaveCacheAsync();
        }
        catch (Exception ex)
        {
            _pending.Remove(item.Id);
            Restore(item, index);
            ErrorMessage = Strings.ErrDone(ex.Message);
            ClearUndo();
            await LoadAsync();   // 위젯에서 이미 지워진 항목이면 유령 항목이 남지 않게 목록을 맞춥니다
        }
    }

    public async Task DeleteAsync(TodoItem item)
    {
        if (_api is null) return;
        ErrorMessage = null;
        int index = Items.IndexOf(item);
        if (index >= 0) Items.RemoveAt(index);
        _pending.Add(item.Id);
        try
        {
            await _api.SetArchivedAsync(item.Id, true);
            _pending.Remove(item.Id);
            OfferUndo(Strings.UndoDeleted(item.Title), () => _api.SetArchivedAsync(item.Id, false));
            await SaveCacheAsync();
        }
        catch (Exception ex)
        {
            _pending.Remove(item.Id);
            Restore(item, index);
            ErrorMessage = Strings.ErrDelete(ex.Message);
            ClearUndo();
            await LoadAsync();
        }
    }

    public async Task UndoAsync()
    {
        var undo = _undo;
        ClearUndo();
        if (undo is null) return;
        try
        {
            await undo();
        }
        catch (Exception ex)
        {
            ErrorMessage = Strings.ErrUndo(ex.Message);
        }
        await LoadAsync();
    }

    public void DismissUndo() => ClearUndo();

    /// <summary>서버에 추가됐으면 true. 진행 중이거나 제목이 비었으면 아무것도 보내지 않고 false.</summary>
    public async Task<bool> AddAsync(string title, string? detail, DateTime? due)
    {
        if (_api is null || _adding || string.IsNullOrWhiteSpace(title)) return false;
        _adding = true;
        ErrorMessage = null;
        try
        {
            await _api.AddAsync(title.Trim(), detail, due);
        }
        catch (Exception ex)
        {
            ErrorMessage = Strings.ErrAdd(ex.Message);
            return false;
        }
        finally
        {
            _adding = false;
        }
        await LoadAsync();
        return true;
    }

    public async Task SaveDetailAsync(TodoItem item, string detail)
    {
        if (_api is null) return;
        ErrorMessage = null;
        var old = item.Detail;
        item.Detail = detail;
        try
        {
            await _api.SetDetailAsync(item.Id, detail);
            await SaveCacheAsync();
        }
        catch (Exception ex)
        {
            item.Detail = old;   // 저장되지 않은 값을 저장된 것처럼 보여주지 않습니다
            ErrorMessage = Strings.ErrDetail(ex.Message);
            await LoadAsync();
        }
    }

    public async Task SetDueAsync(TodoItem item, DateTime? due)
    {
        if (_api is null) return;
        ErrorMessage = null;
        var old = item.Due;
        item.SetDue(due);
        try
        {
            await _api.SetDueAsync(item.Id, due);
            await SaveCacheAsync();
        }
        catch (Exception ex)
        {
            item.SetDue(old);
            ErrorMessage = Strings.ErrDue(ex.Message);
            await LoadAsync();
        }
    }

    // ---------- 내부 ----------

    /// <summary>롤백: 같은 Id 항목이 이미 목록에 있으면(새로고침이 먼저 넣은 경우) 중복으로 넣지 않습니다.</summary>
    private void Restore(TodoItem item, int index)
    {
        if (index < 0 || Items.Any(i => i.Id == item.Id)) return;
        Items.Insert(Math.Min(index, Items.Count), item);
    }

    /// <summary>
    /// 서버 목록을 Id 기준으로 병합합니다. 기존 인스턴스(펼침 상태 등)를 유지하고 차이만 넣고 빼며,
    /// 서버 반영 중인 완료/삭제 항목은 제외합니다.
    /// </summary>
    private void MergeInto(IReadOnlyList<TodoItem> fresh)
    {
        var existing = new Dictionary<string, TodoItem>();
        foreach (var i in Items) existing[i.Id] = i;

        var target = new List<TodoItem>();
        foreach (var f in fresh)
        {
            if (_pending.Contains(f.Id)) continue;
            if (existing.TryGetValue(f.Id, out var e)) { e.UpdateFrom(f); target.Add(e); }
            else target.Add(f);
        }

        for (int i = Items.Count - 1; i >= 0; i--)
            if (!target.Contains(Items[i])) Items.RemoveAt(i);

        for (int i = 0; i < target.Count; i++)
        {
            if (i < Items.Count && ReferenceEquals(Items[i], target[i])) continue;
            int at = Items.IndexOf(target[i]);
            if (at >= 0) Items.Move(at, i);
            else Items.Insert(i, target[i]);
        }
    }

    private void OfferUndo(string text, Func<Task> undo)
    {
        _undo = undo;
        UndoText = text;
    }

    private void ClearUndo()
    {
        _undo = null;
        UndoText = null;
    }

    /// <summary>캐시 저장 실패는 화면 동작에 영향을 주지 않습니다.</summary>
    private async Task SaveCacheAsync()
    {
        try { await _cache.SaveAsync(Items.Select(i => i.ToSnapshot()).ToList()); }
        catch { /* best effort */ }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
