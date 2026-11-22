using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using NotionTodoWidget;
using AButton = Android.Widget.Button;
using AView = Android.Views.View;
using ARadioButton = Android.Widget.RadioButton;

namespace NotionTodo.Mobile;

/// <summary>
/// 홈 화면 위젯의 "+" 로 여는 작은 팝업. 제목(필수)과 마감일(없음/오늘/내일)만 받아 Notion 에 바로 추가합니다.
/// MAUI 가 아닌 네이티브 Activity 라서 앱 전체를 띄우지 않고 빠르게 열립니다.
/// </summary>
[Activity(Theme = "@style/QuickAddTheme", Exported = false, ExcludeFromRecents = true,
    LaunchMode = LaunchMode.SingleTask, TaskAffinity = "",
    WindowSoftInputMode = SoftInput.StateVisible | SoftInput.AdjustResize)]
public class QuickAddActivity : Activity
{
    private EditText _title = null!;
    private RadioGroup _due = null!;
    private TextView _error = null!;
    private AButton _add = null!;

    private T View<T>(string name) where T : AView
        => (T)FindViewById(Resources!.GetIdentifier(name, "id", PackageName))!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resources!.GetIdentifier("quick_add", "layout", PackageName));
        SetFinishOnTouchOutside(true);

        View<TextView>("quick_heading").Text = Strings.QuickAddTitle;
        _title = View<EditText>("quick_title");
        _title.Hint = Strings.QuickAddHint;
        _due = View<RadioGroup>("quick_due");
        View<ARadioButton>("quick_due_none").Text = Strings.NoDue;
        View<ARadioButton>("quick_due_today").Text = Strings.Today;
        View<ARadioButton>("quick_due_tomorrow").Text = Strings.Tomorrow;
        _error = View<TextView>("quick_error");

        var cancel = View<AButton>("quick_cancel");
        cancel.Text = Strings.BtnCancel;
        cancel.Click += (_, _) => Finish();

        _add = View<AButton>("quick_add");
        _add.Text = Strings.BtnAdd;
        _add.Click += (_, _) => Submit();

        // 키보드의 완료(체크) 키로도 추가
        _title.EditorAction += (_, e) =>
        {
            if (e.ActionId == ImeAction.Done) Submit();
        };
    }

    private DateTime? SelectedDue()
    {
        int checkedId = _due.CheckedRadioButtonId;
        if (checkedId == Resources!.GetIdentifier("quick_due_today", "id", PackageName)) return DateTime.Today;
        if (checkedId == Resources!.GetIdentifier("quick_due_tomorrow", "id", PackageName)) return DateTime.Today.AddDays(1);
        return null;
    }

    private void Submit()
    {
        var title = (_title.Text ?? "").Trim();
        if (title.Length == 0 || !_add.Enabled) return;   // 빈 제목 / 전송 중 중복 탭 무시

        var due = SelectedDue();
        _add.Enabled = false;
        _error.Visibility = ViewStates.Gone;

        _ = Task.Run(async () =>
        {
            try
            {
                var token = await MobileSettings.GetTokenAsync();
                var db = MobileSettings.DatabaseId;
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(db))
                    throw new InvalidOperationException(Strings.SetupNeeded);

                await new NotionClient(token, db).AddAsync(title, null, due);

                RunOnUiThread(() =>
                {
                    TodoWidgetProvider.NotifyDataChanged(this);
                    Toast.MakeText(this, Strings.QuickAddDone, ToastLength.Short)?.Show();
                    Finish();
                });
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                {
                    _error.Text = Strings.ErrAdd(ex.Message);   // 입력한 제목은 그대로 두어 다시 시도할 수 있게 함
                    _error.Visibility = ViewStates.Visible;
                    _add.Enabled = true;
                });
            }
        });
    }
}
