namespace NotionTodoWidget;

/// <summary>Notion API 추상화 (화면 로직·테스트에서 사용). 구현: <see cref="NotionClient"/>.</summary>
public interface INotionApi
{
    Task<DatabaseInfo> GetDatabaseInfoAsync();
    Task<List<TodoItem>> GetOpenTodosAsync();
    Task SetDoneAsync(string pageId, bool done);
    Task SetDueAsync(string pageId, DateTime? due);
    Task SetDetailAsync(string pageId, string detail);
    Task SetArchivedAsync(string pageId, bool archived);
    Task AddAsync(string title, string? detail = null, DateTime? due = null);
}
