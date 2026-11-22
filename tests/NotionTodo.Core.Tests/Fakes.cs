namespace NotionTodoWidget.Tests;

public sealed class FakeApi : INotionApi
{
    public List<TodoItem> Server { get; set; } = new();
    public List<string> Calls { get; } = new();
    public Dictionary<string, Exception> Failures { get; } = new();
    public Action? BeforeGetOpen { get; set; }

    private void Hit(string name)
    {
        Calls.Add(name);
        if (Failures.TryGetValue(name, out var ex)) throw ex;
    }

    public Task<DatabaseInfo> GetDatabaseInfoAsync() { Hit(nameof(GetDatabaseInfoAsync)); return Task.FromResult(new DatabaseInfo("t", "u", new List<string>())); }
    public Task<List<TodoItem>> GetOpenTodosAsync()
    {
        BeforeGetOpen?.Invoke();
        Hit(nameof(GetOpenTodosAsync));
        return Task.FromResult(Server.Select(i => TodoItem.FromSnapshot(i.ToSnapshot())).ToList());
    }
    public Task SetDoneAsync(string pageId, bool done) { Hit(nameof(SetDoneAsync) + ":" + pageId + ":" + done); return Task.CompletedTask; }
    public Task SetDueAsync(string pageId, DateTime? due) { Hit(nameof(SetDueAsync)); return Task.CompletedTask; }
    public Task SetDetailAsync(string pageId, string detail) { Hit(nameof(SetDetailAsync)); return Task.CompletedTask; }
    public Task SetArchivedAsync(string pageId, bool archived) { Hit(nameof(SetArchivedAsync) + ":" + pageId + ":" + archived); return Task.CompletedTask; }
    public Task AddAsync(string title, string? detail = null, DateTime? due = null) { Hit(nameof(AddAsync)); return Task.CompletedTask; }
}

public sealed class FakeCache : ITodoCache
{
    public List<TodoSnapshot> Stored { get; set; } = new();
    public Task<IReadOnlyList<TodoSnapshot>> LoadAsync() => Task.FromResult<IReadOnlyList<TodoSnapshot>>(Stored.ToList());
    public Task SaveAsync(IReadOnlyList<TodoSnapshot> items) { Stored = items.ToList(); return Task.CompletedTask; }
}
