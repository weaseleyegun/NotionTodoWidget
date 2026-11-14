using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace NotionTodoWidget;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<TodoItem> _items = new();
    private readonly DispatcherTimer _timer = new();
    private readonly DispatcherTimer _undoTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    private readonly AppSettings _settings = AppSettings.Current;
    private readonly WidgetState _state = WidgetState.Current;

    private NotionClient? _client;
    private bool _ready;
    private bool _allExpanded;
    private int _pending;                 // 진행 중인 쓰기 작업 수 (자동 새로고침과 충돌 방지)
    private bool _dbInfoLoaded;
    private bool _firstLoadDone;          // 첫 불러오기 전에는 '할 일 없음' 안내를 숨김
    private string? _dbUrl;
    private string? _schemaWarning;       // DB 속성 점검 결과 (있으면 계속 표시)
    private bool _dark;
    private Func<Task>? _undo;

    private enum Conn { NoToken, Syncing, Connected, Failed }

    public MainWindow()
    {
        InitializeComponent();
        TodoList.ItemsSource = _items;
        _items.CollectionChanged += (_, _) =>
        {
            CountText.Text = _items.Count.ToString();
            UpdateEmptyState();
        };
        _undoTimer.Tick += (_, _) => HideUndo();

        _timer.Tick += async (_, _) => await RefreshAsync(auto: true);

        // 저장된 투명도·테마 복원
        _dark = ThemeManager.IsDark;
        OpacitySlider.Value = _state.Opacity;
        UpdateThemeButton();
        ApplyPin();
        _ready = true;

        Closing += (_, _) => SaveState();

        ConfigureClient();
    }

    /// <summary>현재 설정으로 Notion 클라이언트를 (다시) 만듭니다.</summary>
    private void ConfigureClient()
    {
        _timer.Stop();
        _client = null;
        _dbInfoLoaded = false;
        _firstLoadDone = false;
        _dbUrl = null;
        _schemaWarning = null;

        if (!_settings.IsConfigured)
        {
            var detail = Strings.SetupNeeded;
            if (!string.IsNullOrEmpty(_settings.LoadError)) detail += "\n" + _settings.LoadError;
            ShowStatus(detail);
            SetConn(Conn.NoToken, Strings.TipClickSettings);
            UpdateEmptyState();
            return;
        }

        _client = new NotionClient(_settings.NotionToken, _settings.DatabaseId);
        _timer.Interval = TimeSpan.FromSeconds(Math.Max(15, _settings.RefreshSeconds));
    }

    // =====================================================================
    //  창 동작 (바탕화면 위젯처럼)
    // =====================================================================

    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

    private static readonly IntPtr HWND_BOTTOM = new(1);
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
    private const int GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x00000080;

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        // Alt+Tab 목록에서 숨김
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;

        // 저장된 크기 복원
        if (_state.Width is double w) Width = Math.Clamp(w, MinWidth, area.Width);
        Height = Math.Clamp(_state.Height ?? Height, MinHeight, area.Height - 20);

        // 저장된 위치가 화면 안이면 복원, 아니면 오른쪽 위
        double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
        double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
        if (_state.Left is double l && _state.Top is double t &&
            l > vl - 50 && l < vl + vw - 50 && t >= vt && t < vt + vh - 50)
        {
            Left = l;
            Top = t;
        }
        else
        {
            Left = area.Right - Width - 20;
            Top = area.Top + 20;
        }

        SendToBottom();
        UpdateEmptyState();

        if (_client is null)
        {
            await OpenSettingsAsync();   // 처음 실행: 설정 창을 바로 열어 줍니다.
            return;
        }
        await RefreshAsync();
        _timer.Start();
    }

    private void Window_Activated(object? sender, EventArgs e) => SendToBottom();

    private void SendToBottom()
    {
        if (_state.Pinned) return;   // 고정 상태에서는 맨 뒤로 보내지 않음
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    // ---------- 크기 조절 ----------

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        Width = Math.Clamp(Width + e.HorizontalChange, MinWidth, area.Width);
        Height = Math.Clamp(Height + e.VerticalChange, MinHeight, area.Height);
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e) => SaveState();

    // ---------- 항상 위 고정 ----------

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        _state.Pinned = !_state.Pinned;
        ApplyPin();
        SaveState();
    }

    private void ApplyPin()
    {
        Topmost = _state.Pinned;
        PinButton.Content = _state.Pinned ? "\uE840" : "\uE718";
        PinButton.ToolTip = _state.Pinned ? Strings.TipUnpin : Strings.TipPin;
        PinButton.SetResourceReference(Control.ForegroundProperty, _state.Pinned ? "AccentBrush" : "IconBrush");
        if (!_state.Pinned) SendToBottom();
    }

    // ---------- Windows 시작 시 자동 실행 ----------

    private void AutoStart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AutoStart.Set(AutoStartMenu.IsChecked);
        }
        catch (Exception ex)
        {
            ShowStatus(Strings.ErrAutoStart(ex.Message));
        }
        AutoStartMenu.IsChecked = AutoStart.IsEnabled();
    }

    protected override void OnContextMenuOpening(ContextMenuEventArgs e)
    {
        AutoStartMenu.IsChecked = AutoStart.IsEnabled();
        base.OnContextMenuOpening(e);
    }

    // =====================================================================
    //  투명도
    // =====================================================================

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _state.Opacity = e.NewValue;
        ThemeManager.Apply(_dark, e.NewValue);   // 배경만 투명해지고 글자는 그대로
    }

    private void OpacitySlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => SaveState();

    // =====================================================================
    //  라이트 / 다크 테마
    // =====================================================================

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        _dark = !_dark;
        _state.Dark = _dark;
        ThemeManager.Apply(_dark, _state.Opacity);
        UpdateThemeButton();
        SaveState();
    }

    private void UpdateThemeButton()
    {
        // 라이트일 때는 달(다크로 전환), 다크일 때는 해(라이트로 전환)
        ThemeBtn.Content = _dark ? "\uE706" : "\uE708";
        ThemeBtn.ToolTip = _dark ? Strings.TipToLight : Strings.TipToDark;
    }

    private void SaveState()
    {
        _state.Left = Left;
        _state.Top = Top;
        _state.Width = Width;
        _state.Height = Height;
        _state.Save();
    }

    // =====================================================================
    //  연결 상태
    // =====================================================================

    private void SetConn(Conn conn, string? detail = null)
    {
        string color, label;
        switch (conn)
        {
            case Conn.Connected:
                color = "#4ADE80";
                label = Strings.ConnOk(DateTime.Now.ToString("HH:mm"));
                break;
            case Conn.Syncing:
                color = "#FBBF24";
                label = Strings.ConnSyncing;
                break;
            case Conn.Failed:
                color = "#F87171";
                label = Strings.ConnFailed;
                break;
            default:
                color = "#94A3B8";
                label = Strings.ConnNeedSetup;
                break;
        }

        StatusDot.Foreground = ThemeManager.MakeBrush(color);
        StatusLabel.Text = label;
        StatusPill.ToolTip = string.IsNullOrEmpty(detail)
            ? Strings.TipClickSettings
            : detail + "\n\n" + Strings.TipClickSettings;
    }

    // =====================================================================
    //  설정 창
    // =====================================================================

    private async void Settings_Click(object sender, RoutedEventArgs e) => await OpenSettingsAsync();

    private async void StatusPill_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => await OpenSettingsAsync();

    private async Task OpenSettingsAsync()
    {
        var win = new SettingsWindow(_settings) { Owner = this };
        if (win.ShowDialog() != true) return;

        // 설정이 바뀌었으니 연결을 새로 만들고 다시 불러옵니다.
        _items.Clear();
        ConfigureClient();
        if (_client is null) return;

        HideStatus();
        await RefreshAsync();
        _timer.Start();
    }

    // =====================================================================
    //  데이터 불러오기
    // =====================================================================

    private bool IsUserTyping() => IsActive && Keyboard.FocusedElement is TextBox;

    private async Task RefreshAsync(bool auto = false)
    {
        if (_client is null) return;

        // 자동 새로고침은 쓰기 작업 중이거나 입력 중이면 건너뜁니다 (입력 내용 보호)
        if (auto && (_pending > 0 || IsUserTyping())) return;

        if (!auto) SetConn(Conn.Syncing);
        try
        {
            var fresh = await _client.GetOpenTodosAsync();

            bool same = fresh.Count == _items.Count &&
                        fresh.Zip(_items, (a, b) =>
                            a.Id == b.Id && a.Title == b.Title && a.Detail == b.Detail &&
                            a.Due == b.Due && a.IconPrefix == b.IconPrefix).All(x => x);

            if (!same)
            {
                var expanded = _items.Where(i => i.IsExpanded).Select(i => i.Id).ToHashSet();
                _items.Clear();
                foreach (var item in fresh)
                {
                    item.IsExpanded = expanded.Contains(item.Id);
                    _items.Add(item);
                }
            }

            _firstLoadDone = true;
            UpdateEmptyState();
            SetConn(Conn.Connected);

            if (!_dbInfoLoaded) await LoadDatabaseInfoAsync();
            if (_schemaWarning is null) HideStatus(); else ShowStatus(_schemaWarning);
        }
        catch (Exception ex)
        {
            SetConn(Conn.Failed, ex.Message);
            ShowStatus(Strings.ErrLoad(ex.Message));
        }
    }

    /// <summary>DB 제목과 URL 을 노션에서 가져옵니다 (하드코딩 없음).</summary>
    private async Task LoadDatabaseInfoAsync()
    {
        if (_client is null) return;
        try
        {
            var info = await _client.GetDatabaseInfoAsync();
            _dbUrl = info.Url;
            if (!string.IsNullOrWhiteSpace(info.Title)) HeaderTitle.Text = info.Title;
            _schemaWarning = info.Problems.Count > 0 ? string.Join("\n", info.Problems) : null;
            _dbInfoLoaded = true;
        }
        catch (Exception)
        {
            // 다음 새로고침 때 다시 시도합니다.
        }
    }

    private void UpdateEmptyState()
    {
        EmptyText.Visibility = _items.Count == 0 && _client is not null && _firstLoadDone
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // =====================================================================
    //  노션 열기
    // =====================================================================

    private async void OpenDatabase_Click(object sender, RoutedEventArgs e)
    {
        if (!_dbInfoLoaded) await LoadDatabaseInfoAsync();
        var url = _dbUrl ?? "https://www.notion.so/" + _settings.DatabaseId.Replace("-", "");
        OpenUrl(url);
    }

    private void OpenNotion_Click(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is { } item && !string.IsNullOrEmpty(item.Url))
            OpenUrl(item.Url);
    }

    /// <summary>설정에 따라 노션 앱(notion://)으로 열고, 실패하면 브라우저로 엽니다.</summary>
    private void OpenUrl(string url)
    {
        try
        {
            if (_settings.OpenInNotionApp && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Process.Start(new ProcessStartInfo("notion://" + url.Substring("https://".Length)) { UseShellExecute = true });
                    return;
                }
                catch (Exception)
                {
                    // 노션 앱이 없으면 아래에서 브라우저로 엽니다.
                }
            }
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowStatus(Strings.ErrOpen(ex.Message));
        }
    }

    // =====================================================================
    //  카드 이벤트
    // =====================================================================

    private static TodoItem? GetItem(object sender)
    {
        if (sender is FrameworkElement { DataContext: TodoItem a }) return a;
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement { DataContext: TodoItem b } } }) return b;
        return null;
    }

    private static bool IsInside<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null)
        {
            if (d is T) return true;
            DependencyObject? parent = d is Visual || d is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(d)
                : null;
            d = parent ?? LogicalTreeHelper.GetParent(d);   // 팝업 내용은 시각 트리가 끊겨 있어 논리 트리로 이어감
        }
        return false;
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // 편집창 안을 클릭한 경우에는 접지 않습니다.
        if (e.OriginalSource is DependencyObject src && (IsInside<TextBox>(src) || IsInside<DueChip>(src))) return;
        if (GetItem(sender) is { } item) item.IsExpanded = !item.IsExpanded;
    }

    private void Expand_Click(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is { } item) item.IsExpanded = !item.IsExpanded;
    }

    private void CardToggle_Click(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is { } item) item.IsExpanded = !item.IsExpanded;
    }

    private void ExpandAll_Click(object sender, RoutedEventArgs e)
    {
        _allExpanded = !_allExpanded;
        foreach (var item in _items) item.IsExpanded = _allExpanded;
        ExpandAllBtn.Content = _allExpanded ? "\uE70E" : "\uE70D";
        ExpandAllBtn.ToolTip = _allExpanded ? Strings.TipCollapseAll : Strings.TipExpandAll;
    }

    // ---------- 완료 (실행 취소 가능) ----------

    private async void Done_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: TodoItem item } || _client is null) return;

        int index = _items.IndexOf(item);
        _items.Remove(item);   // 먼저 화면에서 치워서 반응을 빠르게

        _pending++;
        try
        {
            await _client.SetDoneAsync(item.Id, true);
            ShowUndo(Strings.UndoDone(Shorten(item.Title)), async () =>
            {
                await _client.SetDoneAsync(item.Id, false);
                await RefreshAsync();
            });
        }
        catch (Exception ex)
        {
            ShowStatus(Strings.ErrDone(ex.Message));
            _items.Insert(Math.Min(index, _items.Count), item);   // 실패하면 되돌림
        }
        finally
        {
            _pending--;
        }
    }

    // ---------- 삭제 (노션 휴지통, 실행 취소 가능) ----------

    private async void CardDelete_Click(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item || _client is null) return;

        int index = _items.IndexOf(item);
        _items.Remove(item);

        _pending++;
        try
        {
            await _client.SetArchivedAsync(item.Id, true);
            ShowUndo(Strings.UndoDeleted(Shorten(item.Title)), async () =>
            {
                await _client.SetArchivedAsync(item.Id, false);
                await RefreshAsync();
            });
        }
        catch (Exception ex)
        {
            ShowStatus(Strings.ErrDelete(ex.Message));
            _items.Insert(Math.Min(index, _items.Count), item);
        }
        finally
        {
            _pending--;
        }
    }

    // ---------- 세부내용 편집 ----------

    private async void DetailEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: TodoItem item } box || _client is null) return;

        var text = box.Text.TrimEnd();
        if (text == item.Detail) return;

        var old = item.Detail;
        item.Detail = text;   // 화면에는 바로 반영

        _pending++;
        try
        {
            await _client.SetDetailAsync(item.Id, text);
        }
        catch (Exception ex)
        {
            item.Detail = old;
            ShowStatus(Strings.ErrDetail(ex.Message));
        }
        finally
        {
            _pending--;
        }
    }

    private void DetailEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: TodoItem item } box) return;

        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            AddBox.Focus();            // 포커스를 옮기면 LostFocus 에서 저장됩니다.
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            box.Text = item.Detail;    // 편집 취소
            AddBox.Focus();
        }
    }

    // ---------- 마감일 편집 (펼친 카드의 칩) ----------

    private async void CardDue_Changed(object? sender, EventArgs e)
    {
        if (sender is not DueChip { DataContext: TodoItem item } chip || _client is null) return;

        var old = item.Due;
        var next = chip.Value;
        if (old == next) return;

        item.SetDue(next);   // 화면에는 바로 반영

        _pending++;
        try
        {
            await _client.SetDueAsync(item.Id, next);
        }
        catch (Exception ex)
        {
            item.SetDue(old);
            ShowStatus(Strings.ErrDue(ex.Message));
        }
        finally
        {
            _pending--;
        }
    }

    // =====================================================================
    //  새 할 일 추가 (제목 + 세부내용 + 마감일)
    // =====================================================================

    private void ToggleDetail_Click(object sender, RoutedEventArgs e)
    {
        bool show = NewDetailPanel.Visibility != Visibility.Visible;
        NewDetailPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ToggleDetailBtn.Content = show ? "\uE70E" : "\uE70D";
        if (show) NewDetailBox.Focus();
    }

    private async void AddBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await AddNewAsync();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (AddBox.Text.Length > 0) AddBox.Clear();
            else if (NewDetailPanel.Visibility == Visibility.Visible) ToggleDetail_Click(sender, e);
        }
    }

    private async void NewDetailBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            await AddNewAsync();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            AddBox.Focus();
        }
    }

    private async Task AddNewAsync()
    {
        if (_client is null) return;

        var title = AddBox.Text.Trim();
        if (title.Length == 0)
        {
            AddBox.Focus();
            return;
        }

        var detail = NewDetailBox.Text.Trim();
        DateTime? due = NewDueChip.Value;

        _pending++;
        try
        {
            AddBox.IsEnabled = false;
            NewDetailBox.IsEnabled = false;

            if (!_dbInfoLoaded) await LoadDatabaseInfoAsync();   // 제목 속성 이름 확인
            await _client.AddAsync(title, detail, due);

            AddBox.Clear();
            NewDetailBox.Clear();
            NewDueChip.Value = null;
            HideStatus();
        }
        catch (Exception ex)
        {
            ShowStatus(Strings.ErrAdd(ex.Message));
            return;
        }
        finally
        {
            _pending--;
            AddBox.IsEnabled = true;
            NewDetailBox.IsEnabled = true;
            AddBox.Focus();
        }

        await RefreshAsync();
        if (due is null)   // 마감일 없는 항목은 맨 아래에 추가됨
            Dispatcher.BeginInvoke(new Action(() => ListScroll.ScrollToEnd()), DispatcherPriority.Loaded);
    }

    // =====================================================================
    //  실행 취소 / 메시지
    // =====================================================================

    private void ShowUndo(string text, Func<Task> undo)
    {
        _undo = undo;
        UndoText.Text = text;
        UndoBar.Visibility = Visibility.Visible;
        _undoTimer.Stop();
        _undoTimer.Start();
    }

    private void HideUndo()
    {
        _undoTimer.Stop();
        UndoBar.Visibility = Visibility.Collapsed;
        _undo = null;
    }

    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        var undo = _undo;
        HideUndo();
        if (undo is null) return;

        _pending++;
        try
        {
            await undo();
        }
        catch (Exception ex)
        {
            ShowStatus(Strings.ErrUndo(ex.Message));
        }
        finally
        {
            _pending--;
        }
    }

    private static string Shorten(string s, int max = 16)
        => s.Length <= max ? s : s.Substring(0, max) + "…";

    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusText.Visibility = Visibility.Visible;
    }

    private void HideStatus() => StatusText.Visibility = Visibility.Collapsed;
}
