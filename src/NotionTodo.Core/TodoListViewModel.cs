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
                Items.Clear();
                foreach (var i in fresh) Items.Add(i);
                State = ConnState.Connected;
                StatusText = Strings.ConnOk(DateTime.Now.ToString("HH:mm"));
                if (_errorIsFromLoad) ErrorMessage = null;   // 동작 실패 오류는 새로고침으로 지우지 않습니다
                await SaveCacheAsync();
            }
            catch (Exception ex)
            {
                State = ConnState.Failed;
                StatusText = Strings.ConnFailed;
                ErrorMessage = Strings.ErrLoad(ex.Message);
                _errorIsFromLoad = true;
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
        try
        {
            await _api.SetDoneAsync(item.Id, true);
            OfferUndo(Strings.UndoDone(item.Title), () => _api.SetDoneAsync(item.Id, false));
            await SaveCacheAsync();
        }
        catch (Exception ex)
        {
            if (index >= 0) Items.Insert(Math.Min(index, Items.Count), item);
            ErrorMessage = Strings.ErrDone(ex.Message);
            ClearUndo();
        }
    }

    public async Task DeleteAsync(TodoItem item)
    {
        if (_api is null) return;
        ErrorMessage = null;
        int index = Items.IndexOf(item);
        if (index >= 0) Items.RemoveAt(index);
        try
        {
            await _api.SetArchivedAsync(item.Id, true);
            OfferUndo(Strings.UndoDeleted(item.Title), () => _api.SetArchivedAsync(item.Id, false));
            await SaveCacheAsync();
        }
        catch (Exception ex)
        {
            if (index >= 0) Items.Insert(Math.Min(index, Items.Count), item);
            ErrorMessage = Strings.ErrDelete(ex.Message);
            ClearUndo();
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

    public async Task AddAsync(string title, string? detail, DateTime? due)
    {
        if (_api is null || string.IsNullOrWhiteSpace(title)) return;
        ErrorMessage = null;
        try
        {
            await _api.AddAsync(title.Trim(), detail, due);
        }
        catch (Exception ex)
        {
            ErrorMessage = Strings.ErrAdd(ex.Message);
            return;
        }
        await LoadAsync();
    }

    public async Task SaveDetailAsync(TodoItem item, string detail)
    {
        if (_api is null) return;
        ErrorMessage = null;
        item.Detail = detail;
        try
        {
            await _api.SetDetailAsync(item.Id, detail);
            await SaveCacheAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = Strings.ErrDetail(ex.Message);
            await LoadAsync();
        }
    }

    public async Task SetDueAsync(TodoItem item, DateTime? due)
    {
        if (_api is null) return;
        ErrorMessage = null;
        item.SetDue(due);
        try
        {
            await _api.SetDueAsync(item.Id, due);
            await SaveCacheAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = Strings.ErrDue(ex.Message);
            await LoadAsync();
        }
    }

    // ---------- 내부 ----------

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
