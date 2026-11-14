# Notion Todo Widget

노션 할 일 데이터베이스를 Windows 바탕화면 위젯으로 보여주는 C# WPF 앱입니다.
A Windows desktop widget (C# / WPF) that shows a Notion to-do database.

[한국어](#한국어) · [English](#english)

---

## 한국어

### 기능
- 세로 카드 목록, 클릭하면 펼쳐져 **세부내용 편집** · **마감일 변경**
- 체크박스로 완료(노션에 즉시 반영), 완료/삭제 **실행 취소**
- 날짜 선택 칩 + 달력 팝업 (오늘/내일/다음 주/지우기)
- **라이트 / 다크 모드** (Windows 설정을 따르며, 투명도 슬라이더 옆 아이콘으로 전환)
- 배경 투명도 조절, 크기 조절, 위치 기억, 항상 위 고정, Windows 시작 시 자동 실행
- Notion 연결 상태 표시 (클릭하면 설정 창)
- 한국어 / English UI (Windows 언어에 따라 자동)

### 1. 노션 준비
1. <https://www.notion.so/profile/integrations> 에서 **새 통합(내부)** 을 만들고 *토큰*을 복사합니다.
2. 아래 속성을 가진 **데이터베이스**를 준비합니다 (속성 이름은 정확히 일치해야 합니다).

   | 속성 | 종류 | 필수 |
   |------|------|------|
   | Name (제목 속성이면 이름 무관) | 제목 | 필수 |
   | `Done` | 체크박스 | 필수 |
   | `Detail` | 텍스트 | 선택 (세부내용) |
   | `Due` | 날짜 | 선택 (마감일) |

3. 데이터베이스 페이지 우상단 **⋯ → 연결(Connections)** 에서 만든 통합을 추가합니다. (빠뜨리면 404 오류)

### 2. 실행
필요: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download) (다른 버전을 쓰려면 `.csproj` 의 `TargetFramework` 수정)

```powershell
git clone <이 저장소 주소>
cd NotionTodoWidget
dotnet run
```

처음 실행하면 **설정 창**이 열립니다. 토큰과 데이터베이스 주소(페이지 링크 그대로 붙여넣기 가능)를 입력하고 *연결 테스트* → *저장*.

> 설정은 `%AppData%\NotionTodoWidget\settings.json` 에 저장되며 저장소에는 올라가지 않습니다.
> 토큰은 평문으로 저장되므로 공용 PC에서는 주의하세요.
> (선택) `appsettings.example.json` 을 `appsettings.json` 으로 복사해 직접 입력해도 됩니다. 이 파일은 `.gitignore` 되어 있습니다.

### 3. 배포용 빌드
```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```
`publish\NotionTodoWidget.exe` 를 실행하면 됩니다. (실행 중인 위젯은 빌드 전에 종료: `taskkill /IM NotionTodoWidget.exe /F`)

### 사용 팁
- 헤더를 드래그해 이동, 우하단 ◢ 로 크기 조절, 헤더 우클릭 메뉴에서 자동 실행/설정.
- 위젯은 기본적으로 바탕화면 맨 뒤에 있습니다. 📌 로 고정하면 다른 창 위에 표시됩니다.
- 삭제는 노션 휴지통으로 이동하며 *실행 취소* 로 복구할 수 있습니다.

---

## English

### Features
- Vertical card list; click a card to expand, edit **details** and change the **due date**
- Checkbox completes the item in Notion, with **undo** for done/delete
- Date chip with calendar popup (Today / Tomorrow / Next week / Clear)
- **Light & dark mode** (follows Windows by default; toggle next to the opacity slider)
- Adjustable opacity, resizable, remembers position, keep-on-top, start with Windows
- Notion connection status pill (click it to open Settings)
- Korean / English UI (automatic)

### 1. Prepare Notion
1. Create an **internal integration** at <https://www.notion.so/profile/integrations> and copy its token.
2. Use a database with these properties (names must match exactly):

   | Property | Type | Required |
   |----------|------|----------|
   | Title (any name) | Title | yes |
   | `Done` | Checkbox | yes |
   | `Detail` | Text | optional |
   | `Due` | Date | optional |

3. Open the database page → **⋯ → Connections** and add your integration (otherwise the API returns 404).

### 2. Run
Requires Windows 10/11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download) (edit `TargetFramework` in the `.csproj` to use another version).

```powershell
git clone <this repository>
cd NotionTodoWidget
dotnet run
```

On first launch the **Settings** window opens: paste your token and the database link, press *Test connection*, then *Save*.

> Settings are stored in `%AppData%\NotionTodoWidget\settings.json` (never in the repository). The token is stored in plain text — be careful on shared PCs.
> Optionally copy `appsettings.example.json` to `appsettings.json` (git-ignored) and fill it in.

### 3. Publish
```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

### Tips
- Drag the header to move, drag ◢ to resize, right-click the header for autostart/settings.
- By default the widget sits behind other windows; pin it to keep it on top.
- Delete moves the page to Notion's trash; use *Undo* to restore it.

## License
[MIT](LICENSE)
