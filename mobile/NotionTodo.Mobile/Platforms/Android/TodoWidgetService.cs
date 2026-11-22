using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using NotionTodoWidget;

namespace NotionTodo.Mobile;

[Service(Permission = "android.permission.BIND_REMOTEVIEWS", Exported = false)]
public class TodoWidgetService : RemoteViewsService
{
    public override IRemoteViewsFactory? OnGetViewFactory(Intent? intent)
        => new TodoFactory(ApplicationContext!);

    private sealed class TodoFactory : Java.Lang.Object, IRemoteViewsFactory
    {
        private readonly Context _context;
        private List<TodoItem> _items = new();

        public TodoFactory(Context context) => _context = context;

        public int Count => _items.Count;
        public bool HasStableIds => true;
        public RemoteViews? LoadingView => null;
        public int ViewTypeCount => 1;

        public long GetItemId(int position)
            => position < _items.Count ? _items[position].Id.GetHashCode() : position;

        public void OnCreate() { }
        public void OnDestroy() { }

        /// <summary>시스템이 백그라운드 스레드에서 호출하므로 여기서 동기적으로 Notion 을 조회해도 됩니다.</summary>
        public void OnDataSetChanged()
        {
            try
            {
                _items = Task.Run(LoadAsync).GetAwaiter().GetResult();
            }
            catch
            {
                _items = new List<TodoItem>();
            }
        }

        private static async Task<List<TodoItem>> LoadAsync()
        {
            var cache = new FileTodoCache();
            var token = await MobileSettings.GetTokenAsync();
            var db = MobileSettings.DatabaseId;

            if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(db))
            {
                try
                {
                    var fresh = await new NotionClient(token, db).GetOpenTodosAsync();
                    await cache.SaveAsync(fresh.Select(t =>
                        new TodoSnapshot(t.Id, t.Title, t.Url, t.CreatedText, t.IconPrefix, t.Detail, t.Due)).ToList());
                    return fresh;
                }
                catch
                {
                    // 오프라인 등: 마지막으로 받은 목록을 보여줌
                }
            }

            var snaps = await cache.LoadAsync();
            return snaps.Select(s =>
            {
                var t = new TodoItem
                {
                    Id = s.Id, Title = s.Title, Url = s.Url, CreatedText = s.CreatedText,
                    IconPrefix = s.IconPrefix, Detail = s.Detail
                };
                t.SetDue(s.Due);
                return t;
            }).ToList();
        }

        public RemoteViews? GetViewAt(int position)
        {
            if (position < 0 || position >= _items.Count) return null;
            var item = _items[position];

            var rv = new RemoteViews(_context.PackageName, TodoWidgetProvider.Res(_context, "layout", "todo_widget_item"));

            int titleId = TodoWidgetProvider.Res(_context, "id", "item_title");

            rv.SetTextViewText(titleId, item.IconPrefix + item.Title);

            // 마감 상태별 TextView 중 해당하는 것만 보이게 합니다. 색은 레이아웃의 리소스 참조라 테마가 바뀌면 같이 바뀝니다.
            foreach (var kind in new[] { "over", "today", "ok" })
                rv.SetViewVisibility(TodoWidgetProvider.Res(_context, "id", "item_due_" + kind), ViewStates.Gone);

            var visibleKind = item.DueKind switch { "over" => "over", "today" => "today", "ok" => "ok", _ => null };
            if (visibleKind is not null)
            {
                int dueId = TodoWidgetProvider.Res(_context, "id", "item_due_" + visibleKind);
                rv.SetViewVisibility(dueId, ViewStates.Visible);
                rv.SetTextViewText(dueId, item.DueText);
            }

            // ☐ → 완료, 제목 → 앱 열기
            var done = new Intent();
            done.PutExtra(TodoWidgetProvider.ExtraOp, "done");
            done.PutExtra(TodoWidgetProvider.ExtraId, item.Id);
            rv.SetOnClickFillInIntent(TodoWidgetProvider.Res(_context, "id", "item_check"), done);

            var open = new Intent();
            open.PutExtra(TodoWidgetProvider.ExtraOp, "open");
            open.PutExtra(TodoWidgetProvider.ExtraId, item.Id);
            rv.SetOnClickFillInIntent(TodoWidgetProvider.Res(_context, "id", "item_body"), open);

            return rv;
        }
    }
}
