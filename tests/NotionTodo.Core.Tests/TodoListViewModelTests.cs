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
}
