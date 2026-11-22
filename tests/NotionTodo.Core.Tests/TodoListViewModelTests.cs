using Xunit;

namespace NotionTodoWidget.Tests;

public class TodoListViewModelTests
{
    private static TodoItem Item(string id, string title) => new() { Id = id, Title = title };

    private static (TodoListViewModel vm, FakeApi api, FakeCache cache) Make(params TodoItem[] server)
    {
        Strings.Init("en");
        var api = new FakeApi { Server = server.ToList() };
        var cache = new FakeCache();
        return (new TodoListViewModel(api, cache), api, cache);
    }

    [Fact]
    public async Task Load_ShowsCachedItemsFirstThenReplacesWithServer()
    {
        var (vm, api, cache) = Make(Item("1", "a"), Item("2", "b"), Item("3", "c"));
        cache.Stored = new() { Item("1", "a").ToSnapshot(), Item("2", "b").ToSnapshot() };
        int seenDuringFetch = -1;
        api.BeforeGetOpen = () => seenDuringFetch = vm.Items.Count;

        await vm.LoadAsync();

        Assert.Equal(2, seenDuringFetch);
        Assert.Equal(3, vm.Items.Count);
        Assert.Equal(ConnState.Connected, vm.State);
        Assert.Equal(3, cache.Stored.Count);
    }

    [Fact]
    public async Task Load_Offline_KeepsCachedItemsAndMarksFailed()
    {
        var (vm, api, cache) = Make();
        cache.Stored = new() { Item("1", "a").ToSnapshot(), Item("2", "b").ToSnapshot() };
        var ex = new HttpRequestException("no network");
        api.Failures[nameof(INotionApi.GetOpenTodosAsync)] = ex;

        await vm.LoadAsync();

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(ConnState.Failed, vm.State);
        Assert.Equal(Strings.ErrLoad(ex.Message), vm.ErrorMessage);
    }

    [Fact]
    public async Task Load_WithoutApi_IsNeedSetup()
    {
        Strings.Init("en");
        var vm = new TodoListViewModel(null, new FakeCache());
        await vm.LoadAsync();
        Assert.Equal(ConnState.NeedSetup, vm.State);
    }

    [Fact]
    public async Task Complete_RemovesItemCallsApiAndOffersUndo()
    {
        var (vm, api, _) = Make(Item("1", "a"), Item("2", "b"));
        await vm.LoadAsync();
        var target = vm.Items[0];

        await vm.CompleteAsync(target);

        Assert.Single(vm.Items);
        Assert.Contains("SetDoneAsync:1:True", api.Calls);
        Assert.Equal(Strings.UndoDone("a"), vm.UndoText);
    }

    [Fact]
    public async Task Complete_Failure_RestoresItemAtSameIndexAndSetsError()
    {
        var (vm, api, _) = Make(Item("1", "a"), Item("2", "b"), Item("3", "c"));
        await vm.LoadAsync();
        var target = vm.Items[1];
        var ex = new HttpRequestException("boom");
        api.Failures["SetDoneAsync:2:True"] = ex;

        await vm.CompleteAsync(target);

        Assert.Equal(3, vm.Items.Count);
        Assert.Same(target, vm.Items[1]);
        Assert.Equal(Strings.ErrDone(ex.Message), vm.ErrorMessage);
        Assert.Null(vm.UndoText);
    }

    [Fact]
    public async Task Undo_AfterComplete_CallsSetDoneFalseAndReloads()
    {
        var (vm, api, _) = Make(Item("1", "a"));
        await vm.LoadAsync();
        await vm.CompleteAsync(vm.Items[0]);
        int loadsBefore = api.Calls.Count(c => c == nameof(INotionApi.GetOpenTodosAsync));

        await vm.UndoAsync();

        Assert.Contains("SetDoneAsync:1:False", api.Calls);
        Assert.Equal(loadsBefore + 1, api.Calls.Count(c => c == nameof(INotionApi.GetOpenTodosAsync)));
        Assert.Null(vm.UndoText);
    }

    [Fact]
    public async Task Delete_CallsSetArchivedTrue_AndOffersUndo()
    {
        var (vm, api, _) = Make(Item("1", "a"));
        await vm.LoadAsync();
        await vm.DeleteAsync(vm.Items[0]);
        Assert.Contains("SetArchivedAsync:1:True", api.Calls);
        Assert.Equal(Strings.UndoDeleted("a"), vm.UndoText);
    }

    [Fact]
    public async Task Undo_AfterDelete_CallsSetArchivedFalse()
    {
        var (vm, api, _) = Make(Item("1", "a"));
        await vm.LoadAsync();
        await vm.DeleteAsync(vm.Items[0]);
        await vm.UndoAsync();
        Assert.Contains("SetArchivedAsync:1:False", api.Calls);
    }

    [Fact]
    public async Task Add_CallsApiThenReloads_AndRejectsBlankTitle()
    {
        var (vm, api, _) = Make();
        await vm.LoadAsync();
        await vm.AddAsync("   ", null, null);
        Assert.DoesNotContain(nameof(INotionApi.AddAsync), api.Calls);

        int loads = api.Calls.Count(c => c == nameof(INotionApi.GetOpenTodosAsync));
        await vm.AddAsync("milk", null, null);
        Assert.Contains(nameof(INotionApi.AddAsync), api.Calls);
        Assert.Equal(loads + 1, api.Calls.Count(c => c == nameof(INotionApi.GetOpenTodosAsync)));
    }

    [Fact]
    public async Task SaveDetail_OnArchivedPage_ShowsErrorAndReloads()
    {
        var (vm, api, _) = Make(Item("1", "a"));
        await vm.LoadAsync();
        var ex = new HttpRequestException("404");
        api.Failures[nameof(INotionApi.SetDetailAsync)] = ex;
        int loads = api.Calls.Count(c => c == nameof(INotionApi.GetOpenTodosAsync));

        await vm.SaveDetailAsync(vm.Items[0], "x");

        Assert.Equal(Strings.ErrDetail(ex.Message), vm.ErrorMessage);
        Assert.Equal(loads + 1, api.Calls.Count(c => c == nameof(INotionApi.GetOpenTodosAsync)));
    }

    [Fact]
    public async Task SetDue_UpdatesItemDueTextImmediately()
    {
        var (vm, _, _) = Make(Item("1", "a"));
        await vm.LoadAsync();
        var item = vm.Items[0];
        var due = DateTime.Today.AddDays(3);

        await vm.SetDueAsync(item, due);

        Assert.Equal(due, item.Due);
        Assert.Equal("ok", item.DueKind);
    }

    [Fact]
    public async Task SaveDetail_Offline_RestoresOldDetailAndKeepsActionError()
    {
        var (vm, api, _) = Make(new TodoItem { Id = "1", Title = "a" });
        await vm.LoadAsync();
        var item = vm.Items[0];
        item.Detail = "old";
        var ex = new HttpRequestException("offline");
        api.Failures[nameof(INotionApi.SetDetailAsync)] = ex;
        api.Failures[nameof(INotionApi.GetOpenTodosAsync)] = ex;

        await vm.SaveDetailAsync(item, "typed");

        Assert.Equal("old", item.Detail);
        Assert.Equal(Strings.ErrDetail(ex.Message), vm.ErrorMessage);
    }

    [Fact]
    public async Task SetDue_Failure_RestoresOldDue()
    {
        var (vm, api, _) = Make(Item("1", "a"));
        await vm.LoadAsync();
        var item = vm.Items[0];
        var oldDue = DateTime.Today.AddDays(2);
        item.SetDue(oldDue);
        api.Failures[nameof(INotionApi.SetDueAsync)] = new HttpRequestException("x");
        api.Failures[nameof(INotionApi.GetOpenTodosAsync)] = new HttpRequestException("x");

        await vm.SetDueAsync(item, DateTime.Today.AddDays(9));

        Assert.Equal(oldDue, item.Due);
    }

    [Fact]
    public async Task Complete_Failure_ReloadsSoGhostItemDisappears()
    {
        var (vm, api, _) = Make(Item("a", "A"), Item("b", "B"));
        await vm.LoadAsync();
        var target = vm.Items[0];
        api.Server = new() { Item("b", "B") };          // 위젯에서 이미 삭제됨
        var ex = new HttpRequestException("archived");
        api.Failures["SetDoneAsync:a:True"] = ex;

        await vm.CompleteAsync(target);

        Assert.Equal(new[] { "b" }, vm.Items.Select(i => i.Id).ToArray());
        Assert.Equal(Strings.ErrDone(ex.Message), vm.ErrorMessage);
    }

    [Fact]
    public async Task Load_DuringPendingComplete_DoesNotResurrectItem()
    {
        var (vm, api, _) = Make(Item("x", "X"), Item("y", "Y"));
        await vm.LoadAsync();
        var gate = new TaskCompletionSource();
        api.SetDoneGate = gate.Task;

        var completing = vm.CompleteAsync(vm.Items[0]);   // PATCH 가 느린 상태
        await vm.LoadAsync();                              // 서버는 아직 X 를 돌려줌
        Assert.DoesNotContain(vm.Items, i => i.Id == "x");

        gate.SetResult();
        await completing;
        Assert.Equal(new[] { "y" }, vm.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public async Task Load_KeepsInstancesAndExpandedState()
    {
        var (vm, _, _) = Make(Item("1", "a"), Item("2", "b"));
        await vm.LoadAsync();
        var first = vm.Items[0];
        first.IsExpanded = true;

        await vm.LoadAsync();

        Assert.Same(first, vm.Items[0]);
        Assert.True(first.IsExpanded);
    }

    [Fact]
    public async Task Load_ExpandedItemKeepsLocalDetail_CollapsedGetsServerDetail()
    {
        var (vm, api, _) = Make(new TodoItem { Id = "1", Title = "a" }, new TodoItem { Id = "2", Title = "b" });
        await vm.LoadAsync();
        vm.Items[0].IsExpanded = true;
        vm.Items[0].Detail = "typed locally";
        api.Server = new() { new TodoItem { Id = "1", Title = "a", Detail = "server1" }, new TodoItem { Id = "2", Title = "b", Detail = "server2" } };

        await vm.LoadAsync();

        Assert.Equal("typed locally", vm.Items[0].Detail);
        Assert.Equal("server2", vm.Items[1].Detail);
    }

    [Fact]
    public async Task Add_ConcurrentCalls_OnlyOneApiCall()
    {
        var (vm, api, _) = Make();
        await vm.LoadAsync();
        var gate = new TaskCompletionSource();
        api.AddGate = gate.Task;

        var first = vm.AddAsync("milk", null, null);
        var second = await vm.AddAsync("milk", null, null);   // 진행 중이라 무시
        gate.SetResult();
        var firstOk = await first;

        Assert.False(second);
        Assert.True(firstOk);
        Assert.Equal(1, api.Calls.Count(c => c == nameof(INotionApi.AddAsync)));
    }
}
