using System;
using System.Globalization;

namespace NotionTodoWidget;

/// <summary>
/// UI 문자열 (한국어 / English). 시작할 때 <see cref="Init"/> 으로 언어를 정하고,
/// XAML 에서는 {x:Static local:Strings.이름} 으로 사용합니다.
/// </summary>
public static class Strings
{
    private static bool _ko = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko";

    public static void Init(string? language)
    {
        _ko = (language ?? "auto").Trim().ToLowerInvariant() switch
        {
            "ko" => true,
            "en" => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko"
        };
    }

    private static string T(string ko, string en) => _ko ? ko : en;

    public static CultureInfo Culture => new(_ko ? "ko-KR" : "en-US");

    // ---------- 헤더 / 공통 툴팁 ----------
    public static string HeaderFallback => "Todo List";
    public static string TipOpenDb => T("노션에서 Todo List 열기", "Open the database in Notion");
    public static string TipExpandAll => T("모두 펼치기", "Expand all");
    public static string TipCollapseAll => T("모두 접기", "Collapse all");
    public static string TipPin => T("항상 위에 고정", "Keep on top");
    public static string TipUnpin => T("고정 해제 (바탕화면에 두기)", "Unpin (send to desktop)");
    public static string TipRefresh => T("새로고침", "Refresh");
    public static string TipClose => T("종료", "Close");
    public static string TipOpacity => T("배경 투명도", "Background opacity");
    public static string TipToDark => T("다크 모드로 전환", "Switch to dark mode");
    public static string TipToLight => T("라이트 모드로 전환", "Switch to light mode");
    public static string TipResize => T("드래그하여 크기 조절", "Drag to resize");

    // ---------- 메뉴 ----------
    public static string MenuAutoStart => T("Windows 시작 시 자동 실행", "Start with Windows");
    public static string MenuExpandAll => T("모두 펼치기 / 접기", "Expand / collapse all");
    public static string MenuRefresh => T("새로고침", "Refresh");
    public static string MenuSettings => T("설정…", "Settings…");
    public static string MenuClose => T("종료", "Exit");
    public static string MenuToggle => T("펼치기 / 접기", "Expand / collapse");
    public static string MenuOpenInNotion => T("노션에서 열기", "Open in Notion");
    public static string MenuDelete => T("삭제 (노션 휴지통으로)", "Delete (move to Notion trash)");

    // ---------- 카드 ----------
    public static string TipOpenItem => T("노션에서 이 항목 열기", "Open this item in Notion");
    public static string TipDone => T("완료", "Mark as done");
    public static string TipExpand => T("펼치기 / 접기", "Expand / collapse");
    public static string DetailPlaceholder => T("세부내용 추가…", "Add details…");
    public static string EditorPlaceholder => T("세부내용을 입력하세요", "Write details here");
    public static string EditorTip => T("Ctrl+Enter 또는 바깥을 클릭하면 저장 · Esc 취소", "Ctrl+Enter or click outside to save · Esc to cancel");
    public static string DueLabel => T("마감일", "Due date");

    // ---------- 목록 / 추가 ----------
    public static string EmptyText => T("할 일이 없어요\n아래에서 새 할 일을 추가해 보세요", "Nothing to do\nAdd a new task below");
    public static string AddPlaceholder => T("+ 새 할 일 입력 후 Enter", "+ New task — press Enter");
    public static string NewDetailPlaceholder => T("세부내용 (선택) · Ctrl+Enter 로 추가", "Details (optional) · Ctrl+Enter to add");
    public static string TipToggleDetail => T("세부내용 · 마감일 입력", "Details & due date");

    // ---------- 실행 취소 ----------
    public static string UndoAction => T("실행 취소", "Undo");
    public static string UndoDone(string title) => string.Format(T("완료: {0}", "Done: {0}"), title);
    public static string UndoDeleted(string title) => string.Format(T("삭제: {0}", "Deleted: {0}"), title);

    // ---------- 연결 상태 ----------
    public static string ConnChecking => T("Notion 확인 중…", "Checking Notion…");
    public static string ConnOk(string time) => string.Format(T("Notion 연결됨 · {0} 동기화", "Notion connected · synced {0}"), time);
    public static string ConnSyncing => T("Notion 동기화 중…", "Syncing with Notion…");
    public static string ConnFailed => T("Notion 연결 실패", "Notion connection failed");
    public static string ConnNeedSetup => T("Notion 설정 필요", "Notion setup needed");
    public static string TipClickSettings => T("클릭하면 설정 창이 열립니다", "Click to open settings");
    public static string SetupNeeded => T("Notion 토큰과 데이터베이스를 설정해 주세요. 위의 상태 표시를 클릭하면 설정 창이 열립니다.",
                                          "Set your Notion token and database to get started. Click the status pill above to open Settings.");

    // ---------- 오류 메시지 ----------
    public static string ErrLoad(string m) => string.Format(T("불러오기 실패: {0}", "Failed to load: {0}"), m);
    public static string ErrDone(string m) => string.Format(T("완료 처리 실패: {0}", "Failed to complete: {0}"), m);
    public static string ErrDelete(string m) => string.Format(T("삭제 실패: {0}", "Failed to delete: {0}"), m);
    public static string ErrDetail(string m) => string.Format(T("세부내용 저장 실패: {0}", "Failed to save details: {0}"), m);
    public static string ErrDue(string m) => string.Format(T("마감일 저장 실패: {0}", "Failed to save due date: {0}"), m);
    public static string ErrAdd(string m) => string.Format(T("추가 실패: {0}", "Failed to add: {0}"), m);
    public static string ErrUndo(string m) => string.Format(T("실행 취소 실패: {0}", "Failed to undo: {0}"), m);
    public static string ErrOpen(string m) => string.Format(T("열기 실패: {0}", "Failed to open: {0}"), m);
    public static string ErrAutoStart(string m) => string.Format(T("자동 실행 설정 실패: {0}", "Failed to change startup setting: {0}"), m);
    public static string Http401 => T("토큰이 올바르지 않습니다. 설정에서 토큰을 확인해 주세요.", "Invalid token. Check the token in Settings.");
    public static string Http404 => T("데이터베이스를 찾을 수 없습니다. DB 주소와, 노션에서 해당 DB에 통합이 '연결'되어 있는지 확인해 주세요.",
                                      "Database not found. Check the database URL and make sure the integration is connected to it in Notion.");

    // ---------- DB 속성 점검 ----------
    public static string TypeCheckbox => T("체크박스", "Checkbox");
    public static string TypeText => T("텍스트", "Text");
    public static string TypeDate => T("날짜", "Date");
    public static string SchemaMissingRequired(string name, string type)
        => string.Format(T("필수 속성 '{0}' ({1}) 이(가) 없습니다.", "Required property '{0}' ({1}) is missing."), name, type);
    public static string SchemaMissingOptional(string name, string type)
        => string.Format(T("'{0}' ({1}) 속성이 없어 해당 기능은 사용할 수 없습니다.", "Property '{0}' ({1}) is missing, so that feature is unavailable."), name, type);

    // ---------- 마감일 ----------
    public static string DueDays(int diff)
    {
        if (diff > 0) return string.Format(T("{0}일 남음", "{0}d left"), diff);
        if (diff == 0) return T("오늘까지!", "Due today!");
        return string.Format(T("{0}일 지남", "{0}d overdue"), -diff);
    }

    public static string NoDue => T("마감일 없음", "No due date");
    public static string Today => T("오늘", "Today");
    public static string Tomorrow => T("내일", "Tomorrow");
    public static string NextWeek => T("다음 주", "Next week");
    public static string ClearDue => T("지우기", "Clear");
    public static string TipClearDue => T("마감일 지우기", "Clear due date");
    public static string DateChipFormat => T("M월 d일 (ddd)", "MMM d (ddd)");
    public static string MonthTitleFormat => T("yyyy년 M월", "MMMM yyyy");
    public static string[] Weekdays => _ko
        ? new[] { "일", "월", "화", "수", "목", "금", "토" }
        : new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };

    // ---------- 설정 창 ----------
    public static string SettingsTitle => T("Todo 위젯 설정", "Todo Widget Settings");
    public static string SetupIntro => T(
        "1) 아래 링크에서 '새 통합'을 만들고 토큰을 복사하세요.\n2) 노션에서 사용할 데이터베이스 페이지의 ⋯ → 연결 에 그 통합을 추가하세요.\n3) 데이터베이스 페이지의 링크를 아래에 붙여넣으세요.",
        "1) Create a new integration at the link below and copy its token.\n2) In Notion, open your database page, then ⋯ → Connections and add that integration.\n3) Paste the database page link below.");
    public static string OpenIntegrations => T("노션 통합 만들기 (notion.so/profile/integrations)", "Create a Notion integration (notion.so/profile/integrations)");
    public static string TokenLabel => T("Notion 통합 토큰", "Notion integration token");
    public static string DbLabel => T("데이터베이스 URL 또는 ID", "Database URL or ID");
    public static string DbHint => T("필요한 속성: Name(제목), Done(체크박스) · 선택: Detail(텍스트), Due(날짜)",
                                     "Required: Name (title), Done (checkbox) · Optional: Detail (text), Due (date)");
    public static string OpenInApp => T("링크를 노션 앱으로 열기", "Open links in the Notion app");
    public static string RefreshLabel => T("자동 새로고침 간격 (초)", "Auto-refresh interval (seconds)");
    public static string LanguageLabel => T("언어", "Language");
    public static string LanguageAuto => T("자동", "Auto");
    public static string LanguageNote => T("언어는 다시 시작해야 적용됩니다.", "Language changes apply after restart.");
    public static string BtnTest => T("연결 테스트", "Test connection");
    public static string BtnSave => T("저장", "Save");
    public static string BtnCancel => T("취소", "Cancel");
    public static string Testing => T("확인 중…", "Checking…");
    public static string NeedToken => T("토큰을 입력해 주세요.", "Please enter your token.");
    public static string BadDbId => T("데이터베이스 URL/ID 형식을 확인해 주세요.", "Please check the database URL/ID format.");
    public static string TestOk(string title) => string.Format(T("✓ 연결되었습니다 — {0}", "✓ Connected — {0}"), string.IsNullOrWhiteSpace(title) ? "(untitled)" : title);
    public static string TestFail(string m) => "✗ " + m;
}
