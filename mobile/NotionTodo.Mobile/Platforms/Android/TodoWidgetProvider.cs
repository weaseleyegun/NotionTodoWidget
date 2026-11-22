using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using NotionTodoWidget;

namespace NotionTodo.Mobile;

/// <summary>
/// 홈 화면 위젯. 목록은 <see cref="TodoWidgetService"/> 가 채웁니다.
///  - ☐ 누르면 Notion 에서 완료 처리, 제목 누르면 앱 열기, ⟳ 누르면 새로고침
///  - Notion 은 웹훅이 없어서 시스템 갱신 주기(30분) + 새로고침 버튼 + 앱 실행 시 갱신
/// </summary>
[BroadcastReceiver(Label = "Todo", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData("android.appwidget.provider", Resource = "@xml/todo_widget_info")]
public class TodoWidgetProvider : AppWidgetProvider
{
    public const string ActionAct = "com.notiontodo.mobile.WIDGET_ACT";
    public const string ActionRefresh = "com.notiontodo.mobile.WIDGET_REFRESH";
    public const string ExtraOp = "op";
    public const string ExtraId = "id";

    /// <summary>리소스 이름 → ID (생성된 Resource 클래스에 의존하지 않기 위함)</summary>
    internal static int Res(Context ctx, string type, string name)
        => ctx.Resources!.GetIdentifier(name, type, ctx.PackageName);

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context is null || appWidgetManager is null || appWidgetIds is null) return;

        foreach (var id in appWidgetIds)
            appWidgetManager.UpdateAppWidget(id, Build(context, id));
    }

    private static RemoteViews Build(Context context, int widgetId)
    {
        var rv = new RemoteViews(context.PackageName, Res(context, "layout", "todo_widget"));
        int listId = Res(context, "id", "widget_list");

        rv.SetTextViewText(Res(context, "id", "widget_title"), Strings.HeaderFallback);
        rv.SetTextViewText(Res(context, "id", "widget_empty"), Strings.EmptyText);

        // 목록 어댑터 (위젯마다 고유한 data 를 줘야 시스템이 intent 를 재사용하지 않음)
        var svc = new Intent(context, typeof(TodoWidgetService));
        svc.PutExtra(AppWidgetManager.ExtraAppwidgetId, widgetId);
        svc.SetData(Android.Net.Uri.Parse($"todowidget://{widgetId}"));
        rv.SetRemoteAdapter(listId, svc);
        rv.SetEmptyView(listId, Res(context, "id", "widget_empty"));

        // 목록 항목 클릭 템플릿 (항목마다 op/id 가 채워짐)
        var act = new Intent(context, typeof(TodoWidgetProvider)).SetAction(ActionAct);
        var template = PendingIntent.GetBroadcast(context, widgetId, act,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Mutable);
        rv.SetPendingIntentTemplate(listId, template);

        // 새로고침 버튼
        var refresh = new Intent(context, typeof(TodoWidgetProvider)).SetAction(ActionRefresh);
        var refreshPi = PendingIntent.GetBroadcast(context, widgetId + 100000, refresh,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        rv.SetOnClickPendingIntent(Res(context, "id", "widget_refresh"), refreshPi);

        // 제목 줄을 누르면 앱 열기
        var open = new Intent(context, typeof(MainActivity)).AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        var openPi = PendingIntent.GetActivity(context, widgetId + 200000, open,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        rv.SetOnClickPendingIntent(Res(context, "id", "widget_title"), openPi);

        return rv;
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        base.OnReceive(context, intent);
        if (context is null || intent is null) return;

        switch (intent.Action)
        {
            case ActionRefresh:
                NotifyDataChanged(context);
                break;

            case ActionAct:
                HandleAct(context, intent);
                break;
        }
    }

    private void HandleAct(Context context, Intent intent)
    {
        var op = intent.GetStringExtra(ExtraOp);
        var pageId = intent.GetStringExtra(ExtraId);

        if (op == "open")
        {
            var open = new Intent(context, typeof(MainActivity)).AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            context.StartActivity(open);
            return;
        }

        if (op != "done" || string.IsNullOrEmpty(pageId)) return;

        // 네트워크 작업이 끝날 때까지 프로세스가 유지되도록 GoAsync 사용
        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                var token = await MobileSettings.GetTokenAsync();
                if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(MobileSettings.DatabaseId))
                    await new NotionClient(token, MobileSettings.DatabaseId).SetDoneAsync(pageId, true);
            }
            catch
            {
                // 실패해도 아래에서 목록을 다시 불러오므로 항목이 그대로 남아 재시도할 수 있음
            }
            finally
            {
                NotifyDataChanged(context);
                pending?.Finish();
            }
        });
    }

    /// <summary>모든 위젯의 목록을 다시 불러오게 합니다. (앱에서도 호출 가능)</summary>
    public static void NotifyDataChanged(Context context)
    {
        var mgr = AppWidgetManager.GetInstance(context);
        if (mgr is null) return;

        var component = new ComponentName(context, Java.Lang.Class.FromType(typeof(TodoWidgetProvider)));
        var ids = mgr.GetAppWidgetIds(component);
        if (ids is null || ids.Length == 0) return;

        mgr.NotifyAppWidgetViewDataChanged(ids, Res(context, "id", "widget_list"));
    }
}
