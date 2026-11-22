# Notion Todo Widget

노션 할 일 데이터베이스를 **Windows 바탕화면 위젯**으로 보여주는 C# WPF 앱입니다. 체크하면 노션에 바로 반영되고, 카드를 펼쳐 세부내용과 마감일을 수정할 수 있습니다.
A **Windows desktop widget** (C# / WPF) for a Notion to-do database. Check items off, edit details and due dates — everything syncs to Notion.

[한국어](#한국어) · [English](#english)

---

## 한국어

### 주요 기능

| 기능 | 설명 |
|------|------|
| 카드 목록 | 미완료 항목을 세로 카드로 표시. 마감일 임박 순, 마감일 없는 항목은 맨 아래 |
| 펼치기/접기 | 카드를 클릭하거나 ▾ 버튼 · 헤더의 *모두 펼치기* 버튼. 접힌 상태에서는 세부내용 한 줄 미리보기 |
| 완료 처리 | 체크박스를 누르면 노션의 `Done` 이 체크되고 목록에서 사라짐 |
| 실행 취소 | 완료/삭제 직후 6초 동안 하단에 *실행 취소* 표시 |
| 세부내용 편집 | 펼친 카드의 입력창에 작성 → 바깥 클릭 또는 `Ctrl+Enter` 로 저장, `Esc` 취소 |
| 마감일 | 카드의 날짜 칩 → 달력 팝업(월 이동 · 오늘/내일/다음 주 · 지우기). D-day 태그(N일 남음 / 오늘까지! / N일 지남) 색상 표시 |
| 새 할 일 추가 | 하단 입력창에 제목 + `Enter`. `▾` 로 세부내용과 마감일도 함께 입력 (`Ctrl+Enter` 로 추가) |
| 삭제 | 카드의 🗑 버튼 또는 우클릭 → 삭제. 노션 휴지통으로 이동(실행 취소로 복구 가능) |
| 노션에서 열기 | 카드의 ↗ = 해당 페이지, 헤더의 ↗ = 데이터베이스. 주소·제목은 API에서 가져옴(하드코딩 없음). 노션 앱 → 실패 시 브라우저 |
| 라이트/다크 | 투명도 슬라이더 옆 🌙/☀ 아이콘으로 전환. 처음에는 Windows 설정을 따름 |
| 투명도 | 슬라이더로 배경만 투명하게 (글자는 선명) |
| 창 동작 | 헤더 드래그로 이동, 우하단 ◢ 로 크기 조절, 위치·크기·테마 기억 |
| 고정 | 📌 으로 항상 위 / 바탕화면 맨 뒤(기본) 전환. Alt+Tab 목록에는 표시되지 않음 |
| 자동 실행 | 설정 창 또는 헤더 우클릭 메뉴에서 *Windows 시작 시 자동 실행* |
| 연결 상태 | 상단 알약: 🟢 연결됨(마지막 동기화 시각) · 🟡 동기화 중 · 🔴 실패 · ⚪ 설정 필요. 클릭하면 설정 창 |
| 자동 새로고침 | 기본 60초. 입력 중이거나 저장 중에는 건너뛰어 입력 내용을 보호 |
| 다국어 | 한국어 / English (Windows 언어에 따라 자동, 설정에서 변경) |

### 사용법

#### 1단계 — 노션 준비
1. <https://www.notion.so/profile/integrations> → **새 통합** 만들기(유형: 내부) → **토큰** 복사.
2. 아래 속성을 가진 **데이터베이스**를 준비합니다. 속성 이름과 종류가 같아야 합니다.

   | 속성 | 종류 | 필수 | 용도 |
   |------|------|:---:|------|
   | (제목 속성, 이름 무관) | 제목 | ✅ | 할 일 이름 |
   | `Done` | 체크박스 | ✅ | 완료 여부 |
   | `Detail` | 텍스트 | 선택 | 세부내용 |
   | `Due` | 날짜 | 선택 | 마감일 |

3. 데이터베이스 페이지 우상단 **⋯ → 연결(Connections)** 에서 만든 통합을 추가합니다. 빠뜨리면 404 오류가 납니다.

#### 2단계 — 실행
필요: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download) (다른 버전이면 `.csproj` 의 `TargetFramework` 수정)

```powershell
git clone https://github.com/weaseleyegun/NotionTodoWidget.git
cd NotionTodoWidget
dotnet run
```

#### 3단계 — 연결 설정
첫 실행 시 **설정 창**이 자동으로 열립니다.
1. 토큰 붙여넣기
2. 데이터베이스 페이지 **링크를 그대로** 붙여넣기 (32자리 ID만 넣어도 됨)
3. **연결 테스트** → ✓ 가 나오면 **저장**

설정은 `%AppData%\NotionTodoWidget\settings.json` 에 저장되며 저장소에는 올라가지 않습니다. (토큰은 평문 저장이므로 공용 PC에서는 주의)
파일로 설정하고 싶다면 `appsettings.example.json` 을 `appsettings.json` 으로 복사해 채우세요(`.gitignore` 됨).

#### 4단계 — 배포용 빌드 + 자동 실행
```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```
`publish\NotionTodoWidget.exe` 를 실행하고 설정에서 *Windows 시작 시 자동 실행* 을 켭니다. publish 폴더를 옮기면 체크를 껐다 켜서 경로를 갱신하세요.

### 단축키 요약
| 위치 | 키 | 동작 |
|------|----|------|
| 하단 입력창 | `Enter` | 할 일 추가 |
| 하단 입력창 | `Esc` | 입력 지우기 → 세부 패널 닫기 |
| 세부내용 입력 | `Ctrl+Enter` | 추가(새 항목) / 저장(기존 항목) |
| 세부내용 편집 | `Esc` | 편집 취소 |

### 문제 해결
- **404 / 데이터베이스를 찾을 수 없음** — DB 페이지에서 통합을 *연결* 했는지, 링크가 올바른지 확인
- **401 / 토큰 오류** — 설정에서 토큰 재입력
- **"필수 속성이 없습니다"** — `Done`(체크박스) 등 속성 이름/종류 확인
- **위젯이 안 보임** — 바탕화면 맨 뒤에 있어 다른 창에 가려진 것일 수 있습니다. 📌 으로 고정하거나 바탕화면 보기(`Win+D`)
- **빌드 전 종료** — `taskkill /IM NotionTodoWidget.exe /F`

### 구조
`MainWindow` (목록/UI) · `NotionClient` (API) · `DueChip` (달력 칩) · `SettingsWindow` · `ThemeManager` (라이트/다크 팔레트) · `Strings` (한/영) · `AppSettings`/`WidgetState` (설정/상태)

### Android 앱
같은 노션 데이터베이스를 쓰는 **Android 앱**(.NET MAUI)이 `mobile/` 에 있습니다. 위젯과 폰은 서로 직접 통신하지 않고 둘 다 노션 API 로 읽고 써서 동기화됩니다.

| 항목 | 설명 |
|------|------|
| 기능 | 위젯과 동일: 목록, 완료/삭제 + 실행 취소, 세부내용·마감일 편집, 새 할 일, 한/영, 라이트/다크, 연결 상태 |
| 동기화 | 앱으로 돌아올 때 즉시, 켜져 있는 동안 60초마다(입력 중에는 건너뜀), 아래로 당겨 새로고침 |
| 오프라인 | 마지막으로 받은 목록을 보여주고 상태가 🔴 로 바뀜 |
| 충돌 | 같은 항목을 위젯과 폰에서 동시에 수정하면 **마지막에 저장한 쪽**이 이김 |
| 토큰 | 폰의 보안 저장소(SecureStorage)에만 저장 |

빌드: .NET SDK + `dotnet workload install maui-android`, JDK 17+, Android SDK 필요.
```powershell
dotnet build mobile/NotionTodo.Mobile -f net11.0-android -t:InstallAndroidDependencies -p:AndroidSdkDirectory=<SDK경로> -p:JavaSdkDirectory=<JDK경로> -p:AcceptAndroidSDKLicenses=True   # 최초 1회
dotnet publish mobile/NotionTodo.Mobile -f net11.0-android -c Release -p:AndroidPackageFormat=apk -p:AndroidSdkDirectory=<SDK경로> -p:JavaSdkDirectory=<JDK경로>
adb install -r <생성된 *-Signed.apk>
```
첫 실행 시 설정 화면에서 토큰과 데이터베이스 링크를 입력하고 **연결 테스트 → 저장**. 공유 코드는 `src/NotionTodo.Core`(Notion 클라이언트·화면 로직), 테스트는 `dotnet test tests/NotionTodo.Core.Tests`.

---

## English

### Features

| Feature | Description |
|---------|-------------|
| Card list | Open items as vertical cards, sorted by due date (undated last) |
| Expand/collapse | Click a card, use ▾, or *Expand all* in the header; collapsed cards show a one-line details preview |
| Complete | Checkbox sets `Done` in Notion and removes the card |
| Undo | A 6-second *Undo* bar after completing or deleting |
| Edit details | Type in the expanded card; click outside or `Ctrl+Enter` to save, `Esc` to cancel |
| Due date | Date chip → calendar popup (month navigation, Today / Tomorrow / Next week, Clear). Color-coded countdown tag |
| Add task | Type a title + `Enter`; `▾` adds details and a due date (`Ctrl+Enter` to add) |
| Delete | 🗑 button on a card, or right-click → Delete (moves to Notion's trash; undo restores it) |
| Open in Notion | ↗ on a card opens the page; ↗ in the header opens the database (URL/title come from the API) |
| Light / dark | 🌙/☀ icon next to the opacity slider; follows Windows at first |
| Opacity | Slider makes only the background translucent |
| Window | Drag header to move, ◢ to resize; position, size and theme are remembered |
| Pin | 📌 toggles always-on-top vs. behind other windows (default); hidden from Alt+Tab |
| Autostart | *Start with Windows* in Settings or the header context menu |
| Status pill | 🟢 connected (last sync) · 🟡 syncing · 🔴 failed · ⚪ setup needed; click to open Settings |
| Auto refresh | Every 60 s by default; skipped while you type or save |
| Languages | Korean / English (automatic, changeable in Settings) |

### Quick start
1. **Notion** — create an internal integration at <https://www.notion.so/profile/integrations>, copy the token. Use a database with: a title property, `Done` (checkbox, required), `Detail` (text, optional), `Due` (date, optional). On the database page choose **⋯ → Connections** and add the integration.
2. **Run** — requires Windows 10/11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download):
   ```powershell
   git clone https://github.com/weaseleyegun/NotionTodoWidget.git
   cd NotionTodoWidget
   dotnet run
   ```
3. **Settings** (opens automatically on first run) — paste the token and the database link, *Test connection*, *Save*. Settings live in `%AppData%\NotionTodoWidget\settings.json` (plain text; never in the repo). Optionally copy `appsettings.example.json` to `appsettings.json` (git-ignored).
4. **Publish + autostart** — `dotnet publish -c Release -r win-x64 --self-contained false -o publish`, run `publish\NotionTodoWidget.exe`, enable *Start with Windows*.

### Troubleshooting
- **404 / database not found** — connect the integration to the database page; check the link.
- **401** — re-enter the token.
- **Missing required property** — check that `Done` exists and is a checkbox.
- **Widget not visible** — it sits behind other windows by default; pin it or press `Win+D`.
- **Before rebuilding** — `taskkill /IM NotionTodoWidget.exe /F`.

### Android app
An **Android app** (.NET MAUI) for the same Notion database lives in `mobile/`. The widget and the phone never talk to each other — both read and write through the Notion API, which keeps them in sync.

- **Features:** same as the widget — list, complete/delete with undo, edit details and due dates, add tasks, Korean/English, light/dark, connection status.
- **Sync:** immediately when the app returns to the foreground, every 60 s while open (skipped while typing), and pull-to-refresh.
- **Offline:** shows the last list received and the status turns 🔴.
- **Conflicts:** if the widget and phone edit the same item, the **last save wins**.
- **Token:** stored only in the phone's SecureStorage.

Build needs the .NET SDK, `dotnet workload install maui-android`, JDK 17+ and the Android SDK:
```powershell
dotnet build mobile/NotionTodo.Mobile -f net11.0-android -t:InstallAndroidDependencies -p:AndroidSdkDirectory=<sdk> -p:JavaSdkDirectory=<jdk> -p:AcceptAndroidSDKLicenses=True   # once
dotnet publish mobile/NotionTodo.Mobile -f net11.0-android -c Release -p:AndroidPackageFormat=apk -p:AndroidSdkDirectory=<sdk> -p:JavaSdkDirectory=<jdk>
adb install -r <generated *-Signed.apk>
```
Shared code is in `src/NotionTodo.Core` (Notion client, view-model); tests: `dotnet test tests/NotionTodo.Core.Tests`.

## License
[MIT](LICENSE)
