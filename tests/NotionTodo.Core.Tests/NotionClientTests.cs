using System.Net;
using System.Text.Json;
using Xunit;

namespace NotionTodoWidget.Tests;

public class NotionClientTests
{
    private const string Db = "01234567-89ab-cdef-0123-456789abcdef";

    private static (NotionClient client, FakeHandler handler) Make(string token = "secret")
    {
        Strings.Init("en");
        var h = new FakeHandler();
        var http = new HttpClient(h) { BaseAddress = new Uri("https://api.notion.com/v1/") };
        return (new NotionClient(http, token, Db), h);
    }

    private static string Page(string id, string title, string? detail = null, string? due = null, string? emoji = null)
    {
        var props = new Dictionary<string, object>
        {
            ["Name"] = new { type = "title", title = new[] { new { plain_text = title } } },
            ["Detail"] = new { type = "rich_text", rich_text = detail is null ? Array.Empty<object>() : new object[] { new { plain_text = detail } } },
            ["Due"] = new { type = "date", date = due is null ? null : new { start = due } },
        };
        object? icon = emoji is null ? null : new { type = "emoji", emoji };
        return JsonSerializer.Serialize(new { id, url = "https://notion.so/" + id, created_time = "2026-11-01T00:00:00.000Z", icon, properties = props });
    }

    private static string Results(bool hasMore, string? cursor, params string[] pages)
        => "{\"results\":[" + string.Join(",", pages) + "],\"has_more\":" + (hasMore ? "true" : "false")
           + ",\"next_cursor\":" + (cursor is null ? "null" : "\"" + cursor + "\"") + "}";

    [Fact]
    public async Task GetOpenTodos_ParsesTitleDetailDueAndIcon()
    {
        var (c, h) = Make();
        h.Enqueue(HttpStatusCode.OK, Results(false, null, Page("p1", "장보기", "우유", "2026-12-01", "😀")));
        var items = await c.GetOpenTodosAsync();
        var t = Assert.Single(items);
        Assert.Equal("장보기", t.Title);
        Assert.Equal("우유", t.Detail);
        Assert.Equal(new DateTime(2026, 12, 1), t.Due);
        Assert.Equal("😀  ", t.IconPrefix);
    }

    [Fact]
    public async Task GetOpenTodos_FollowsPagination()
    {
        var (c, h) = Make();
        h.Enqueue(HttpStatusCode.OK, Results(true, "c1", Page("a", "A")));
        h.Enqueue(HttpStatusCode.OK, Results(false, null, Page("b", "B")));
        var items = await c.GetOpenTodosAsync();
        Assert.Equal(2, items.Count);
        Assert.Contains("\"start_cursor\":\"c1\"", h.Bodies[1]);
    }

    [Fact]
    public async Task GetOpenTodos_SortsByDueUndatedLast()
    {
        var (c, h) = Make();
        h.Enqueue(HttpStatusCode.OK, Results(false, null,
            Page("a", "none"), Page("b", "later", due: "2026-12-05"), Page("c", "soon", due: "2026-12-01")));
        var titles = (await c.GetOpenTodosAsync()).Select(t => t.Title).ToArray();
        Assert.Equal(new[] { "soon", "later", "none" }, titles);
    }

    [Fact]
    public async Task SetDone_SendsCheckboxPatch()
    {
        var (c, h) = Make();
        await c.SetDoneAsync("p1", true);
        Assert.Equal(HttpMethod.Patch, h.Requests[0].Method);
        Assert.EndsWith("pages/p1", h.Requests[0].RequestUri!.ToString());
        Assert.Contains("\"Done\":{\"checkbox\":true}", h.Bodies[0]);
    }

    [Fact]
    public async Task SetDetail_SplitsAt2000Chars()
    {
        var (c, h) = Make();
        await c.SetDetailAsync("p1", new string('가', 4500));
        using var doc = JsonDocument.Parse(h.Bodies[0]);
        var parts = doc.RootElement.GetProperty("properties").GetProperty("Detail").GetProperty("rich_text").EnumerateArray()
            .Select(p => p.GetProperty("text").GetProperty("content").GetString()!.Length).ToArray();
        Assert.Equal(new[] { 2000, 2000, 500 }, parts);
    }

    [Fact]
    public async Task SetDetail_EmptyStringSendsEmptyArray()
    {
        var (c, h) = Make();
        await c.SetDetailAsync("p1", "");
        Assert.Contains("\"rich_text\":[]", h.Bodies[0]);
    }

    [Fact]
    public async Task Add_UsesDiscoveredTitlePropertyName()
    {
        var (c, h) = Make();
        h.Enqueue(HttpStatusCode.OK, "{\"title\":[],\"properties\":{\"할 일\":{\"type\":\"title\"},\"Done\":{\"type\":\"checkbox\"}}}");
        await c.GetDatabaseInfoAsync();
        await c.AddAsync("새 일");
        using var doc = JsonDocument.Parse(h.Bodies[1]);
        Assert.True(doc.RootElement.GetProperty("properties").TryGetProperty("할 일", out _));
    }

    [Fact]
    public async Task Http429_RetriesAfterRetryAfterThenSucceeds()
    {
        var (c, h) = Make();
        h.Enqueue((HttpStatusCode)429, "{}", retryAfter: 0);
        h.Enqueue(HttpStatusCode.OK);
        await c.SetDoneAsync("p1", true);
        Assert.Equal(2, h.Requests.Count);
        Assert.Equal(h.Bodies[0], h.Bodies[1]);
    }

    [Fact]
    public async Task Http429_ThrowsAfterMaxRetries()
    {
        var (c, h) = Make();
        for (int i = 0; i < 4; i++) h.Enqueue((HttpStatusCode)429, "{\"message\":\"rate\"}", retryAfter: 0);
        await Assert.ThrowsAsync<HttpRequestException>(() => c.SetDoneAsync("p1", true));
        Assert.Equal(4, h.Requests.Count);
    }

    [Fact]
    public async Task Http401_And404_UseLocalizedMessages()
    {
        var (c, h) = Make();
        h.Enqueue(HttpStatusCode.Unauthorized);
        h.Enqueue(HttpStatusCode.NotFound);
        var e1 = await Assert.ThrowsAsync<HttpRequestException>(() => c.SetDoneAsync("p", true));
        var e2 = await Assert.ThrowsAsync<HttpRequestException>(() => c.SetDoneAsync("p", true));
        Assert.Equal(Strings.Http401, e1.Message);
        Assert.Equal(Strings.Http404, e2.Message);
    }

    [Fact]
    public async Task Token_IsTrimmed()
    {
        var (c, h) = Make("  secret\n");
        await c.SetDoneAsync("p1", true);
        Assert.Equal("Bearer secret", h.Requests[0].Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task SetDetail_DoesNotSplitSurrogatePairs()
    {
        var (c, h) = Make();
        var text = new string('a', 1999) + "😀" + "tail";   // 이모지가 2000자 경계에 걸림
        await c.SetDetailAsync("p1", text);
        using var doc = JsonDocument.Parse(h.Bodies[0]);
        var parts = doc.RootElement.GetProperty("properties").GetProperty("Detail").GetProperty("rich_text").EnumerateArray()
            .Select(p => p.GetProperty("text").GetProperty("content").GetString()!).ToArray();
        Assert.Equal(text, string.Concat(parts));
        Assert.All(parts, p => Assert.DoesNotContain('�', p));
    }
}
