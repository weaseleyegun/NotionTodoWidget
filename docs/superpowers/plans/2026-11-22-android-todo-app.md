# Android Todo 앱 (.NET MAUI) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 Notion 할 일 DB를 그대로 쓰는 Android 앱을 만들고, Windows WPF 위젯과 코드·데이터를 공유해 두 앱이 항상 같은 목록을 보게 한다.

**Architecture:** Notion DB가 유일한 데이터 원본이다(별도 서버 없음). 위젯의 `NotionClient`·`TodoItem`·`Strings`·`NotionIds`를 공유 라이브러리 `NotionTodo.Core`로 옮기고, 화면 로직(`TodoListViewModel`)도 Core에 두어 단위 테스트한다. Android 앱(.NET MAUI)은 Core를 참조하는 얇은 UI다. 위젯과 폰은 서로 직접 통신하지 않고, 둘 다 Notion API로 읽고 써서 동기화된다.

**Tech Stack:** C# / .NET 10 (`net10.0`), .NET MAUI (Android 전용 타깃), xUnit, Notion API `2022-06-28`, MAUI `SecureStorage`/`Preferences`.

**Spec:** 별도 문서 없음. 대화에서 확정된 요구사항: (1) 기술은 .NET MAUI로 위임받아 결정, (2) 범위는 "위젯과 동일 기능"(목록, 완료+실행 취소, 세부내용·마감일 편집, 추가, 삭제+실행 취소, 한/영, 다크 모드, 연결 상태), (3) 기존 Notion 페이지(DB)와 기존 Windows 위젯과 동기화. 참조: `README.md`의 기능표, `NotionClient.cs`.

## Global Constraints

- 사용자 대상 언어는 한국어/English. 모든 UI 문자열은 `Strings`를 거친다(하드코딩 금지).
- Notion DB 속성 계약은 변경 금지: 제목(title 타입이면 이름 무관), `Done`(checkbox, 필수), `Detail`(rich_text, 선택), `Due`(date, 선택). `Notion-Version: 2022-06-28`.
- 삭제 = Notion 휴지통 이동(`archived: true`), 복구 가능해야 한다.
- rich_text는 조각당 2000자 제한 → 나눠서 전송(기존 `RichText` 유지).
- 토큰은 폰에서 `SecureStorage`에만 저장한다. 로그·화면·예외 메시지에 토큰을 출력하지 않는다.
- Core 프로젝트는 UI/플랫폼 의존성(WPF, MAUI)을 가지지 않는다. 네임스페이스는 `NotionTodoWidget` 유지.
- 기존 WPF 위젯의 동작은 바뀌면 안 된다(리팩터링 후에도 빌드·실행 가능).
- 커밋 메시지 끝에 `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` 줄을 넣는다.

## Review Focus

테스트가 없으면 놓치기 쉬운, 실제 사용자가 만날 상황(가능성 높은 순):

1. **오프라인/비행기 모드로 앱 실행** → 앱이 죽지 않고, 마지막으로 저장된 목록을 보여주며 상태가 "연결 실패"가 된다. (Task 3)
2. **Notion 속도 제한(HTTP 429)** — 체크를 빠르게 연타하면 발생 → `Retry-After`만큼 기다렸다 재시도하고 그래도 실패하면 오류 표시, 항목은 복구. (Task 2)
3. **붙여넣은 토큰/URL의 앞뒤 공백·줄바꿈, 모바일 공유 URL**(`...?v=...&pvs=4`, `notion.site`) → 정상 인식. (Task 2, 4)
4. **위젯에서 이미 완료/삭제한 항목을 폰에서 편집** → Notion이 400/404를 돌려줌 → 오류 표시 후 목록 새로고침, 앱은 계속 동작. (Task 3)
5. **Detail이 2000자 초과 / 이모지·한글 포함** → 분할 전송 후 다시 읽었을 때 동일. (Task 2)

---

## 파일 구조

| 경로 | 역할 |
|---|---|
| `src/NotionTodo.Core/NotionTodo.Core.csproj` (신규) | 공유 라이브러리 `net10.0` |
| `src/NotionTodo.Core/NotionClient.cs` (이동) | Notion API. `INotionApi` 구현 |
| `src/NotionTodo.Core/INotionApi.cs` (신규) | API 추상화(테스트·VM용) |
| `src/NotionTodo.Core/TodoItem.cs` (이동) | 항목 모델 + `TodoSnapshot` 변환 |
| `src/NotionTodo.Core/Strings.cs` (이동) | 한/영 문자열 |
| `src/NotionTodo.Core/NotionIds.cs` (이동, AppSettings.cs에서 분리) | URL/ID 파싱 |
| `src/NotionTodo.Core/TodoListViewModel.cs` (신규) | 목록 상태·동작·실행 취소·오프라인 캐시 |
| `src/NotionTodo.Core/ITodoCache.cs` (신규) | 마지막 목록 캐시 인터페이스 + `TodoSnapshot` |
| `tests/NotionTodo.Core.Tests/` (신규) | xUnit 테스트 |
| `mobile/NotionTodo.Mobile/` (신규) | MAUI Android 앱: `MauiProgram.cs`, `SetupPage`, `TodoPage`, `MobileSettings.cs`, `FileTodoCache.cs` |
| `NotionTodoWidget.csproj` (수정) | Core 참조, 하위 폴더 `Compile Remove` |
| `MainWindow.xaml`, `SettingsWindow.xaml` (수정) | `local:Strings` → `core:Strings` |
| `AppSettings.cs` (수정) | `NotionIds` 제거(Core로 이동) |
| `NotionTodoWidget.sln` (신규) | 모든 프로젝트 묶음 |

---

### Task 1: 공유 라이브러리 `NotionTodo.Core` 추출 (위젯은 그대로 동작)

**Files:**
- Create: `src/NotionTodo.Core/NotionTodo.Core.csproj`, `NotionTodoWidget.sln`
- Move (`git mv`): `NotionClient.cs`, `TodoItem.cs`, `Strings.cs` → `src/NotionTodo.Core/`
- Create: `src/NotionTodo.Core/NotionIds.cs` (`AppSettings.cs`의 `NotionIds` 클래스를 그대로 옮김)
- Modify: `AppSettings.cs`, `NotionTodoWidget.csproj`, `MainWindow.xaml`, `SettingsWindow.xaml`

**Interfaces:**
- Produces: 어셈블리 `NotionTodo.Core`, 네임스페이스 `NotionTodoWidget`에 `NotionClient`, `DatabaseInfo`, `TodoItem`, `Strings`, `NotionIds`(기존 시그니처 그대로).

- [ ] **Step 1: 환경 점검** — `dotnet workload install maui-android` 실행 후 `dotnet workload list`에 `maui-android`가 보여야 한다. JDK 17+와 Android SDK(`ANDROID_HOME` 또는 Visual Studio/Android Studio 설치본)가 필요하다. 설치된 SDK는 8.0.426, 11.0.100이다. MAUI 워크로드가 지원하는 TFM이 `net10.0-android`가 아니면 이 계획의 모든 `net10.0*`를 지원되는 값으로 일괄 치환한다(결정은 여기서 한 번만).
  기대 결과: `dotnet workload list`에 `maui-android` 표시.
- [ ] **Step 2: 베이스라인 확인** — Run: `dotnet build NotionTodoWidget.csproj` → 기대: 성공(실행 중이면 `taskkill /IM NotionTodoWidget.exe /F` 먼저).
- [ ] **Step 3: `NotionTodo.Core.csproj` 작성** — `net10.0`, `Nullable`/`ImplicitUsings` enable, `RootNamespace=NotionTodoWidget`, `System.Net.Http` 암시적 using 포함. `git mv`로 3개 파일 이동, `NotionIds`를 `NotionIds.cs`로 분리.
- [ ] **Step 4: 위젯 csproj 수정** — `<ProjectReference Include="src\NotionTodo.Core\NotionTodo.Core.csproj" />` 추가, 그리고 하위 프로젝트 소스가 위젯에 컴파일되지 않도록 `<Compile Remove="src\**;tests\**;mobile\**" />`, `<None Remove>`/`<Page Remove>`도 동일 경로에 적용.
- [ ] **Step 5: XAML 수정** — 두 XAML 루트에 `xmlns:core="clr-namespace:NotionTodoWidget;assembly=NotionTodo.Core"` 추가하고 `local:Strings` 참조(약 45곳)만 `core:Strings`로 치환. `local:DueChip` 등 위젯 자체 타입은 그대로 둔다.
- [ ] **Step 6: 솔루션 생성** — `dotnet new sln -n NotionTodoWidget` 후 `dotnet sln add NotionTodoWidget.csproj src/NotionTodo.Core/NotionTodo.Core.csproj`.
- [ ] **Step 7: 빌드·실행 확인** — Run: `dotnet build NotionTodoWidget.sln` → 기대: 오류 0. `dotnet run`으로 위젯을 띄워 목록이 보이고 체크 시 Notion에 반영되는지 확인.
- [ ] **Step 8: Commit** — `git add -A && git commit -m "refactor: extract NotionTodo.Core shared library"`

---

### Task 2: NotionClient 테스트 가능화 + 속도 제한(429) 재시도 + 입력 정리

**Files:**
- Modify: `src/NotionTodo.Core/NotionClient.cs`, `src/NotionTodo.Core/NotionIds.cs`
- Create: `src/NotionTodo.Core/INotionApi.cs`, `tests/NotionTodo.Core.Tests/NotionTodo.Core.Tests.csproj`, `NotionClientTests.cs`, `NotionIdsTests.cs`, `FakeHandler.cs`

**Interfaces:**
- Produces:
  ```csharp
  public interface INotionApi {
    Task<DatabaseInfo> GetDatabaseInfoAsync();
    Task<List<TodoItem>> GetOpenTodosAsync();
    Task SetDoneAsync(string pageId, bool done);
    Task SetDueAsync(string pageId, DateTime? due);
    Task SetDetailAsync(string pageId, string detail);
    Task SetArchivedAsync(string pageId, bool archived);
    Task AddAsync(string title, string? detail = null, DateTime? due = null);
  }
  // NotionClient : INotionApi
  public NotionClient(string token, string databaseId)                 // 기존, 토큰/ID Trim 추가
  public NotionClient(HttpClient http, string token, string databaseId) // 신규, 테스트용 (BaseAddress는 호출 측이 설정)
  ```
  `FakeHandler : HttpMessageHandler` — 요청 목록 `List<HttpRequestMessage> Requests`와 응답 큐 `Queue<HttpResponseMessage>`를 가진다.

- [ ] **Step 1: 테스트 프로젝트 생성** — xUnit, `net10.0`, Core 참조, 솔루션에 추가.
- [ ] **Step 2: 실패하는 테스트 작성** (각각 `FakeHandler`로 응답을 주입)
  - `GetOpenTodos_ParsesTitleDetailDueAndIcon` — 응답 1건 `Name="장보기"`, `Detail="우유"`, `Due=2026-12-01`, 아이콘 😀 → `Title=="장보기"`, `Detail=="우유"`, `Due==new DateTime(2026,12,1)`, `IconPrefix=="😀  "`.
  - `GetOpenTodos_FollowsPagination` — 첫 응답 `has_more=true,next_cursor="c1"`, 둘째 응답 `has_more=false` → 항목 합계가 두 페이지 합이고 두 번째 요청 본문에 `"start_cursor":"c1"` 포함.
  - `GetOpenTodos_SortsByDueUndatedLast` — 마감일 없음/12-05/12-01 입력 → 12-01, 12-05, 없음 순.
  - `SetDone_SendsCheckboxPatch` — `PATCH pages/{id}` 본문에 `"Done":{"checkbox":true}`.
  - `SetDetail_SplitsAt2000Chars` — 4500자 → rich_text 조각 3개(2000/2000/500), 빈 문자열이면 빈 배열.
  - `Add_UsesDiscoveredTitlePropertyName` — `GetDatabaseInfoAsync`가 제목 속성명 `"할 일"`을 찾은 뒤 `AddAsync`하면 본문 properties 키가 `"할 일"`.
  - `Http429_RetriesAfterRetryAfterThenSucceeds` — 429(`Retry-After: 0`) 후 200 → 예외 없이 성공, 요청 2회.
  - `Http429_ThrowsAfterMaxRetries` — 429가 4번 연속 → `HttpRequestException`, 요청 총 4회(최초 1 + 재시도 3).
  - `Http401_And404_UseLocalizedMessages` — 예외 메시지가 `Strings.Http401`/`Strings.Http404`와 같다(`Strings.Init("en")`).
  - `Token_IsTrimmed` — 토큰 `"  secret\n"` → `Authorization: Bearer secret`.
  `NotionIdsTests`: `Parse_AcceptsMobileShareUrl`(`https://www.notion.so/ws/Todo-0123456789abcdef0123456789abcdef?v=abc&pvs=4`), `Parse_AcceptsNotionSiteUrl`, `Parse_AcceptsHyphenatedId`, `Parse_ReturnsNullForGarbage`, `Parse_TrimsWhitespaceAndNewlines`.
- [ ] **Step 3: 실패 확인** — Run: `dotnet test tests/NotionTodo.Core.Tests` → 기대: 컴파일 실패(`INotionApi`, 새 생성자 없음).
- [ ] **Step 4: 구현** — `INotionApi` 추가, `NotionClient`가 구현하고 `HttpClient` 주입 생성자 추가(기존 생성자는 이를 호출). 모든 요청을 `SendWithRetryAsync(Func<HttpRequestMessage>)` 한 곳으로 통과시켜 429면 `Retry-After`(없으면 1초)만큼 `Task.Delay` 후 최대 3회 재시도. 요청은 매번 새로 만든다(본문 재사용 불가).
- [ ] **Step 5: 통과 확인** — Run: `dotnet test tests/NotionTodo.Core.Tests` → 기대: 전부 PASS. `dotnet build NotionTodoWidget.sln` 성공.
- [ ] **Step 6: Commit** — `git commit -am "feat: injectable HttpClient, 429 retry, input trimming + tests"` (신규 파일은 `git add` 포함)

---

### Task 3: `TodoListViewModel` — 화면 로직·실행 취소·오프라인 캐시

**Files:**
- Create: `src/NotionTodo.Core/TodoListViewModel.cs`, `src/NotionTodo.Core/ITodoCache.cs`, `tests/NotionTodo.Core.Tests/TodoListViewModelTests.cs`, `FakeApi.cs`, `FakeCache.cs`
- Modify: `src/NotionTodo.Core/TodoItem.cs` (스냅샷 변환)

**Interfaces:**
- Consumes: `INotionApi` (Task 2).
- Produces:
  ```csharp
  public sealed record TodoSnapshot(string Id, string Title, string Url, string CreatedText,
                                    string IconPrefix, string Detail, DateTime? Due);
  public interface ITodoCache {
    Task<IReadOnlyList<TodoSnapshot>> LoadAsync();
    Task SaveAsync(IReadOnlyList<TodoSnapshot> items);
  }
  // TodoItem: public TodoSnapshot ToSnapshot(); public static TodoItem FromSnapshot(TodoSnapshot s);
  public enum ConnState { NeedSetup, Syncing, Connected, Failed }
  public sealed class TodoListViewModel : INotifyPropertyChanged {
    public TodoListViewModel(INotionApi? api, ITodoCache cache);   // api == null → NeedSetup
    public ObservableCollection<TodoItem> Items { get; }
    public ConnState State { get; }  public string StatusText { get; }
    public string? ErrorMessage { get; }
    public string? UndoText { get; }          // null이면 실행 취소 막대 숨김
    public Task LoadAsync();                  // 캐시 먼저 표시 → 서버 조회
    public Task CompleteAsync(TodoItem item);
    public Task DeleteAsync(TodoItem item);
    public Task UndoAsync();
    public Task AddAsync(string title, string? detail, DateTime? due);
    public Task SaveDetailAsync(TodoItem item, string detail);
    public Task SetDueAsync(TodoItem item, DateTime? due);
    public void DismissUndo();                // 6초 타이머 만료 시 UI가 호출
  }
  ```

- [ ] **Step 1: 실패하는 테스트 작성** (`FakeApi`는 호출 기록과 던질 예외를 설정 가능)
  - `Load_ShowsCachedItemsFirstThenReplacesWithServer` — 캐시 2건, 서버 3건 → 서버 응답 후 `Items.Count==3`, `State==Connected`, 캐시에 3건 저장됨.
  - `Load_Offline_KeepsCachedItemsAndMarksFailed` — 서버가 `HttpRequestException` → `Items`는 캐시 그대로, `State==Failed`, `ErrorMessage==Strings.ErrLoad(...)`, 예외 전파 없음. *(Review Focus 1)*
  - `Load_WithoutApi_IsNeedSetup` — `api==null` → `State==NeedSetup`, 서버 호출 없음.
  - `Complete_RemovesItemCallsApiAndOffersUndo` — `Items`에서 제거, `SetDoneAsync(id,true)` 호출, `UndoText==Strings.UndoDone(title)`.
  - `Complete_Failure_RestoresItemAtSameIndexAndSetsError` — API 예외 → 항목이 원래 위치에 복귀, `ErrorMessage==Strings.ErrDone(msg)`, `UndoText==null`.
  - `Undo_AfterComplete_CallsSetDoneFalseAndReloads` / `Undo_AfterDelete_CallsSetArchivedFalse`.
  - `Delete_CallsSetArchivedTrue_AndOffersUndo`.
  - `Add_CallsApiThenReloads_AndRejectsBlankTitle` — 공백 제목이면 API 호출 없음.
  - `SaveDetail_OnArchivedPage_ShowsErrorAndReloads` — API가 `HttpRequestException("404")` → `ErrorMessage==Strings.ErrDetail(...)`, 이후 `GetOpenTodosAsync`가 1회 추가 호출됨. *(Review Focus 4)*
  - `SetDue_UpdatesItemDueTextImmediately` — 호출 직후 `item.Due`와 `item.DueKind` 갱신.
- [ ] **Step 2: 실패 확인** — Run: `dotnet test tests/NotionTodo.Core.Tests --filter TodoListViewModelTests` → 기대: 컴파일 실패.
- [ ] **Step 3: 구현** — 위 시그니처대로. 낙관적 갱신(UI 먼저 반영 → API 호출 → 실패 시 롤백+오류), 모든 API 오류는 `ErrorMessage`로만 전달하고 메서드 밖으로 던지지 않는다. `LoadAsync` 동시 호출은 직렬화(`SemaphoreSlim`)해 자동 새로고침과 사용자 동작이 겹치지 않게 한다.
- [ ] **Step 4: 통과 확인** — Run: `dotnet test tests/NotionTodo.Core.Tests` → 기대: 전부 PASS.
- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: TodoListViewModel with undo and offline cache"`

---

### Task 4: MAUI Android 앱 골격 + 설정 화면

**Files:**
- Create: `mobile/NotionTodo.Mobile/` (MAUI 프로젝트, `net10.0-android` 단일 타깃, Core 참조), `MobileSettings.cs`, `FileTodoCache.cs`, `SetupPage.xaml(.cs)`, `AppShell.xaml`
- Modify: `NotionTodoWidget.sln`

**Interfaces:**
- Consumes: `NotionClient(string token, string databaseId)`, `NotionIds.ParseDatabaseId`, `Strings.Init`, `ITodoCache`.
- Produces:
  ```csharp
  public static class MobileSettings {
    public static Task<string?> GetTokenAsync();              // SecureStorage
    public static Task SetTokenAsync(string token);
    public static string DatabaseId { get; set; }             // Preferences
    public static string Language { get; set; }               // "auto"|"ko"|"en"
    public static int RefreshSeconds { get; set; }            // 기본 60
    public static Task<bool> IsConfiguredAsync();
  }
  public sealed class FileTodoCache : ITodoCache { }          // FileSystem.CacheDirectory/todos.json
  ```

- [ ] **Step 1: 프로젝트 생성** — `dotnet new maui -n NotionTodo.Mobile -o mobile/NotionTodo.Mobile` 후 `<TargetFrameworks>`를 `net10.0-android` 하나로 줄이고 Core 프로젝트 참조 추가, 템플릿의 샘플 `MainPage`와 불필요한 리소스 제거. `ApplicationTitle`은 "Todo".
- [ ] **Step 2: `MobileSettings`, `FileTodoCache` 구현** — 위 시그니처. 캐시 파일이 없거나 손상되면 빈 목록 반환(예외 금지).
- [ ] **Step 3: `SetupPage` 구현** — 토큰/DB URL 입력, `Strings.SetupIntro` 안내, "연결 테스트" 버튼이 `GetDatabaseInfoAsync()`를 호출해 `Strings.TestOk(title)`/`TestFail(msg)`와 속성 누락 경고(`Problems`)를 표시, 저장 시 입력값 Trim 후 `NotionIds.ParseDatabaseId`로 정규화. 형식 오류면 `Strings.BadDbId`. 토큰 입력칸은 `IsPassword=true`.
- [ ] **Step 4: 시작 흐름** — `MauiProgram`에서 `Strings.Init(MobileSettings.Language)`; 설정이 없으면 `SetupPage`, 있으면 목록 페이지(Task 5)로 이동하도록 `AppShell` 구성(목록 페이지는 임시 빈 `ContentPage`로 둔다).
- [ ] **Step 5: 빌드 확인** — Run: `dotnet build mobile/NotionTodo.Mobile -f net10.0-android` → 기대: 오류 0.
- [ ] **Step 6: 실기기/에뮬레이터 수동 확인** — `dotnet build -t:Run -f net10.0-android mobile/NotionTodo.Mobile`. 잘못된 토큰 → 401 한국어 메시지, 올바른 토큰+DB URL → "✓ 연결되었습니다 — {DB 제목}", 앱 재시작 후 설정 유지(토큰은 재입력 불필요).
- [ ] **Step 7: Commit** — `git add -A && git commit -m "feat(mobile): MAUI Android scaffold and setup page"`

---

### Task 5: 목록 화면 (위젯과 동일 기능)

**Files:**
- Create: `mobile/NotionTodo.Mobile/TodoPage.xaml(.cs)`, `Converters.cs`, `Resources/Styles/Colors.xaml`(라이트/다크)
- Modify: `AppShell.xaml`, `MauiProgram.cs`

**Interfaces:**
- Consumes: `TodoListViewModel`(Task 3 전체), `FileTodoCache`, `MobileSettings`, `NotionClient`.
- Produces: 없음(최종 UI).

- [ ] **Step 1: 화면 구성** — `CollectionView`(항목 = 카드): 체크박스(→`CompleteAsync`), 이모지+제목, 접힘 시 `DetailPreview` 한 줄, 탭하면 펼침(`IsExpanded`) → 세부내용 `Editor`(포커스 해제 시 `SaveDetailAsync`), 마감일 칩(`DueText`, `DueKind`별 색: none/ok/today/over), 탭하면 `DatePicker` + "오늘/내일/다음 주/지우기" 버튼(`Strings`), 카드 스와이프 또는 🗑 버튼 → `DeleteAsync`, ↗ → `Launcher.OpenAsync(item.Url)`.
- [ ] **Step 2: 상단/하단** — 상단 상태 알약(`State`/`StatusText`, 탭하면 `SetupPage`), `RefreshView` 당겨서 새로고침, 하단 새 할 일 입력창(Enter=`AddAsync`) + 세부내용·마감일 펼침. 목록이 비면 `Strings.EmptyText`. `UndoText`가 있으면 하단 스낵바 형태 막대 + `Strings.UndoAction` 버튼, 6초 후 `DismissUndo()`.
- [ ] **Step 3: 자동 새로고침** — 앱 포그라운드 복귀(`Window.Resumed`) 시 즉시 `LoadAsync()`, 포그라운드에서 `RefreshSeconds`마다 타이머로 `LoadAsync()`. 사용자가 입력/편집 중(Editor·입력창 포커스)이면 타이머 새로고침은 건너뛴다. 백그라운드에서는 타이머 중지.
- [ ] **Step 4: 다크 모드·언어** — `AppThemeBinding`으로 라이트/다크 색상 분리(시스템 설정 따름), 모든 텍스트는 `Strings` 바인딩.
- [ ] **Step 5: 키보드/안전 영역** — 입력창이 키보드에 가려지지 않도록 `Android:WindowSoftInputMode=adjustResize`(`MainActivity` 속성) 설정.
- [ ] **Step 6: 수동 검증 체크리스트(기기에서)** — 각 항목이 Notion 웹에서도 반영되는지 확인:
  1. 목록이 Notion의 미완료 항목과 일치(마감 임박순) 2. 체크 → 사라지고 Notion `Done` 체크됨, 실행 취소로 복구 3. 세부내용 편집·저장(한글/이모지, 2000자 초과 붙여넣기) 4. 마감일 설정/지우기, D-day 색상 5. 새 할 일 추가(제목만 / 세부내용+마감일) 6. 삭제 → Notion 휴지통에 있음, 실행 취소로 복구 7. 비행기 모드로 앱 재실행 → 마지막 목록 표시 + 🔴 상태, 앱 종료 안 됨 8. 시스템 다크 모드 전환 9. 시스템 언어 English에서 영어 UI.
- [ ] **Step 7: Commit** — `git add -A && git commit -m "feat(mobile): todo list page with feature parity to widget"`

---

### Task 6: 위젯↔폰 동기화 검증 + 배포 + 문서

**Files:**
- Modify: `README.md`
- Create: `docs/android-install.md`(없으면 README 섹션으로 대체)

**Interfaces:**
- Consumes: 완성된 앱 전체.

- [ ] **Step 1: 양방향 동기화 수동 검증** — Windows 위젯과 폰을 같은 DB로 설정한 뒤: (a) 폰에서 추가 → 위젯 새로고침 주기(60초) 안에 표시 (b) 위젯에서 완료 → 폰 포그라운드 복귀 시 사라짐 (c) 양쪽에서 같은 항목의 세부내용을 수정 → 마지막 저장이 이김(정상 동작, README에 명시) (d) 위젯에서 삭제한 항목을 폰에서 편집 시도 → 오류 메시지 후 목록 갱신, 앱 유지.
- [ ] **Step 2: Release APK 빌드** — Run: `dotnet publish mobile/NotionTodo.Mobile -f net10.0-android -c Release -p:AndroidPackageFormat=apk` → 기대: `*-Signed.apk` 생성. (개인 사용 서명 키 생성은 `keytool -genkey ...` 후 `-p:AndroidKeyStore=true` 옵션; 키 파일과 비밀번호는 저장소에 올리지 않고 `.gitignore`에 `*.keystore` 추가.)
- [ ] **Step 3: 설치 확인** — `adb install -r <apk>` 후 처음부터(설정→연결 테스트→목록) 동작 확인.
- [ ] **Step 4: README 갱신** — 한/영 양쪽에 "Android 앱" 섹션(요구사항, 빌드/설치, 설정, 위젯과의 동기화 방식, 충돌 시 마지막 저장 우선), 구조 섹션에 `NotionTodo.Core`/`mobile` 추가.
- [ ] **Step 5: 전체 테스트·빌드 최종 확인** — Run: `dotnet test tests/NotionTodo.Core.Tests && dotnet build NotionTodoWidget.sln` → 기대: 전부 PASS, 오류 0.
- [ ] **Step 6: Commit** — `git add -A && git commit -m "docs: Android app usage and sync notes"`

---

## Self-Review

- **Spec coverage:** 목록/완료+취소/세부내용·마감일 편집/추가/삭제+취소/한영/다크/연결 상태 → Task 3(로직)·5(UI). Notion 연동 → Task 2·4. 위젯 연동 → Task 1(코드 공유)·6(양방향 검증). 홈 화면 위젯은 범위 밖(사용자가 "위젯과 동일 기능" 선택).
- **Type consistency:** `INotionApi` 7개 메서드는 Task 2 정의 = Task 3 사용 = `NotionClient` 기존 시그니처와 일치. `TodoSnapshot`/`ITodoCache`는 Task 3 정의 = Task 4 `FileTodoCache` 구현. `ConnState`·`UndoText`·`DismissUndo`는 Task 3 정의 = Task 5 사용.
- **Review Focus 매핑:** 1→Task 3 `Load_Offline_*`, 2→Task 2 `Http429_*`, 3→Task 2 `Token_IsTrimmed`/`NotionIdsTests`, 4→Task 3 `SaveDetail_OnArchivedPage_*`, 5→Task 2 `SetDetail_SplitsAt2000Chars`.
- **알려진 위험:** (1) MAUI 워크로드가 설치된 .NET SDK(8.0.426, 11.0.100)와 맞는 TFM을 지원하는지는 Task 1 Step 1에서 확정. (2) 위젯 csproj가 하위 폴더 `.cs`를 자동 포함하므로 Task 1 Step 4의 `Compile Remove`가 필수. (3) Notion은 웹훅이 없어 폰 동기화는 폴링(복귀 시 + 주기)이다.
