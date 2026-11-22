namespace NotionTodoWidget;

/// <summary>오프라인 표시용 항목 스냅샷 (캐시 직렬화 단위)</summary>
public sealed record TodoSnapshot(string Id, string Title, string Url, string CreatedText,
                                  string IconPrefix, string Detail, DateTime? Due);

/// <summary>마지막으로 받은 목록을 보관합니다. 구현체는 파일이 없거나 손상돼도 예외 대신 빈 목록을 돌려줘야 합니다.</summary>
public interface ITodoCache
{
    Task<IReadOnlyList<TodoSnapshot>> LoadAsync();
    Task SaveAsync(IReadOnlyList<TodoSnapshot> items);
}
