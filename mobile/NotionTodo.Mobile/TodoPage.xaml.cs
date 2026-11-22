using System.ComponentModel;
using NotionTodoWidget;

namespace NotionTodo.Mobile;

public partial class TodoPage : ContentPage
{
    private TodoListViewModel? _vm;
    private IDispatcherTimer? _timer;
    private CancellationTokenSource? _undoCts;
    private Window? _window;

    /// <summary>입력/편집 중이면 자동 새로고침을 건너뜁니다(입력 내용 보호).</summary>
    private bool _editing;

    // 날짜 선택 대상: 기존 항목 또는 "새 할 일"
    private TodoItem? _pickItem;
    private bool _pickForNew;
    private DateTime? _newDue;

    public TodoPage()
    {
        InitializeComponent();
        AddButton.Text = Strings.BtnSave;
        UpdateNewDueLabel();
    }

    // ---------- 수명 주기 ----------

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!await MobileSettings.IsConfiguredAsync())
        {
            await Shell.Current.GoToAsync("//SetupPage");
            return;
        }

        var token = await MobileSettings.GetTokenAsync();
        _vm = new TodoListViewModel(new NotionClient(token!, MobileSettings.DatabaseId), new FileTodoCache());
        _vm.PropertyChanged += OnVmPropertyChanged;
        BindingContext = _vm;

        if (Window is { } w)
        {
            _window = w;
            w.Resumed += OnWindowResumed;
            w.Stopped += OnWindowStopped;
        }

        StartTimer();
        await _vm.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTimer();
        if (_window is { } w)
        {
            w.Resumed -= OnWindowResumed;
            w.Stopped -= OnWindowStopped;
            _window = null;
        }
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private async void OnWindowResumed(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        StartTimer();
        await _vm.LoadAsync();   // 앱으로 돌아오면 즉시 동기화
    }

    private void OnWindowStopped(object? sender, EventArgs e) => StopTimer();

    private void StartTimer()
    {
        StopTimer();
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(MobileSettings.RefreshSeconds);
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer = null;
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (_vm is null || _editing) return;
        await _vm.LoadAsync();
    }

    // 실행 취소 막대는 6초 뒤 자동으로 사라집니다.
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TodoListViewModel.UndoText) && _vm?.UndoText is not null)
            _ = AutoDismissUndoAsync();
    }

    private async Task AutoDismissUndoAsync()
    {
        _undoCts?.Cancel();
        var cts = _undoCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(6), cts.Token);
            _vm?.DismissUndo();
        }
        catch (TaskCanceledException) { /* 새 실행 취소가 생겨 타이머가 교체됨 */ }
    }

    // ---------- 헤더 ----------

    private async void OnStatusTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//SetupPage");

    private async void OnOpenDb(object? sender, EventArgs e)
    {
        try { await Launcher.OpenAsync("https://www.notion.so/" + MobileSettings.DatabaseId.Replace("-", "")); }
        catch (Exception ex) { await ShowAlert(Strings.ErrOpen(ex.Message)); }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        if (_vm is not null) await _vm.LoadAsync();
        Refresh.IsRefreshing = false;
    }

    // ---------- 카드 ----------

    private static TodoItem? ItemOf(object? sender) => (sender as BindableObject)?.BindingContext as TodoItem;

    private async void OnCompleteTapped(object? sender, TappedEventArgs e)
    {
        if (ItemOf(sender) is { } item && _vm is not null) await _vm.CompleteAsync(item);
    }

    private void OnToggleTapped(object? sender, TappedEventArgs e)
    {
        if (ItemOf(sender) is { } item) item.IsExpanded = !item.IsExpanded;
    }

    private async void OnOpenItem(object? sender, EventArgs e)
    {
        if (ItemOf(sender) is not { } item || string.IsNullOrEmpty(item.Url)) return;
        try { await Launcher.OpenAsync(item.Url); }
        catch (Exception ex) { await ShowAlert(Strings.ErrOpen(ex.Message)); }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (ItemOf(sender) is { } item && _vm is not null) await _vm.DeleteAsync(item);
    }

    private void OnEditFocused(object? sender, FocusEventArgs e) => _editing = true;

    private void OnEditUnfocused(object? sender, FocusEventArgs e) => _editing = false;

    private async void OnDetailUnfocused(object? sender, FocusEventArgs e)
    {
        _editing = false;
        if (sender is Editor editor && ItemOf(editor) is { } item && _vm is not null)
        {
            var text = editor.Text ?? "";
            if (text != item.Detail) await _vm.SaveDetailAsync(item, text);
        }
    }

    private Task SetDue(object? sender, int daysFromToday)
        => ItemOf(sender) is { } item && _vm is not null
            ? _vm.SetDueAsync(item, DateTime.Today.AddDays(daysFromToday))
            : Task.CompletedTask;

    private async void OnDueToday(object? sender, EventArgs e) => await SetDue(sender, 0);
    private async void OnDueTomorrow(object? sender, EventArgs e) => await SetDue(sender, 1);
    private async void OnDueNextWeek(object? sender, EventArgs e) => await SetDue(sender, 7);

    private async void OnDueClear(object? sender, EventArgs e)
    {
        if (ItemOf(sender) is { } item && _vm is not null) await _vm.SetDueAsync(item, null);
    }

    private void OnDuePick(object? sender, EventArgs e)
    {
        if (ItemOf(sender) is not { } item) return;
        OpenPicker(forNew: false, item, item.Due);
    }

    // ---------- 날짜 선택 ----------

    private void OpenPicker(bool forNew, TodoItem? item, DateTime? current)
    {
        _pickItem = null;                         // Date 를 바꿀 때 DateSelected 가 오동작하지 않도록
        _pickForNew = false;
        Picker.Date = current ?? DateTime.Today;
        _pickForNew = forNew;
        _pickItem = item;
        Picker.Focus();
    }

    private async void OnDatePicked(object? sender, DateChangedEventArgs e)
    {
        if (e.NewDate is not DateTime picked)
            return;

        if (_pickForNew)
        {
            _newDue = picked.Date;
            UpdateNewDueLabel();
        }
        else if (_pickItem is { } item && _vm is not null)
        {
            await _vm.SetDueAsync(item, picked.Date);
        }
        _pickItem = null;
        _pickForNew = false;
    }

    // ---------- 새 할 일 ----------

    private void OnAddToggle(object? sender, EventArgs e)
    {
        AddPanel.IsVisible = !AddPanel.IsVisible;
        AddToggle.Text = AddPanel.IsVisible ? "▴" : "▾";
    }

    private void OnNewToday(object? sender, EventArgs e) { _newDue = DateTime.Today; UpdateNewDueLabel(); }
    private void OnNewTomorrow(object? sender, EventArgs e) { _newDue = DateTime.Today.AddDays(1); UpdateNewDueLabel(); }
    private void OnNewClear(object? sender, EventArgs e) { _newDue = null; UpdateNewDueLabel(); }
    private void OnNewPick(object? sender, EventArgs e) => OpenPicker(forNew: true, null, _newDue);

    private void UpdateNewDueLabel()
        => NewDueLabel.Text = _newDue is DateTime d
            ? d.ToString(Strings.DateChipFormat, Strings.Culture)
            : Strings.NoDue;

    private async void OnAddCompleted(object? sender, EventArgs e) => await AddNewAsync();

    private async void OnAddClicked(object? sender, EventArgs e) => await AddNewAsync();

    private async Task AddNewAsync()
    {
        if (_vm is null) return;
        var title = AddEntry.Text ?? "";
        if (string.IsNullOrWhiteSpace(title)) return;

        await _vm.AddAsync(title, AddDetail.Text, _newDue);

        if (_vm.ErrorMessage is null)   // 실패하면 입력을 남겨 다시 시도할 수 있게 합니다.
        {
            AddEntry.Text = "";
            AddDetail.Text = "";
            _newDue = null;
            UpdateNewDueLabel();
            AddPanel.IsVisible = false;
            AddToggle.Text = "▾";
        }
    }

    private async void OnUndoClicked(object? sender, EventArgs e)
    {
        if (_vm is not null) await _vm.UndoAsync();
    }

    private Task ShowAlert(string message) => DisplayAlertAsync(Strings.HeaderFallback, message, "OK");
}
