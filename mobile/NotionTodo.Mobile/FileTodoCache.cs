using System.Text.Json;
using NotionTodoWidget;

namespace NotionTodo.Mobile;

/// <summary>마지막 목록을 JSON 파일로 보관(오프라인 표시용). 파일 없음/손상은 빈 목록.</summary>
public sealed class FileTodoCache : ITodoCache
{
    private static string PathOf => System.IO.Path.Combine(FileSystem.CacheDirectory, "todos.json");

    public async Task<IReadOnlyList<TodoSnapshot>> LoadAsync()
    {
        try
        {
            if (!File.Exists(PathOf)) return Array.Empty<TodoSnapshot>();
            await using var fs = File.OpenRead(PathOf);
            return await JsonSerializer.DeserializeAsync<List<TodoSnapshot>>(fs) ?? new List<TodoSnapshot>();
        }
        catch
        {
            return Array.Empty<TodoSnapshot>();
        }
    }

    public async Task SaveAsync(IReadOnlyList<TodoSnapshot> items)
    {
        var tmp = PathOf + ".tmp";
        await using (var fs = File.Create(tmp))
            await JsonSerializer.SerializeAsync(fs, items);
        File.Move(tmp, PathOf, overwrite: true);
    }
}
