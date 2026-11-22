using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NotionTodoWidget;

/// <summary>DB 제목과 URL (노션 API 에서 조회 — 하드코딩 없음)</summary>
public sealed record DatabaseInfo(string Title, string Url, IReadOnlyList<string> Problems);

/// <summary>Notion API 호출 담당. DB 속성: Name(title), Done(checkbox), Detail(rich_text), Due(date)</summary>
public sealed class NotionClient : INotionApi
{
    private const int MaxRetries = 3;

    private readonly HttpClient _http;
    private readonly string _databaseId;
    private string _titleProp = "Name";

    public NotionClient(string token, string databaseId)
        : this(new HttpClient { BaseAddress = new Uri("https://api.notion.com/v1/") }, token, databaseId)
    {
    }

    /// <summary>테스트용: HttpClient 를 주입합니다. 토큰·DB ID 앞뒤 공백/줄바꿈은 제거합니다.</summary>
    public NotionClient(HttpClient http, string token, string databaseId)
    {
        _http = http;
        _http.BaseAddress ??= new Uri("https://api.notion.com/v1/");
        _databaseId = databaseId.Trim();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        _http.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28");
    }

    /// <summary>모든 요청의 단일 통로. 429(속도 제한)면 Retry-After 만큼 기다렸다 최대 3회 재시도합니다.</summary>
    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> makeRequest)
    {
        for (int attempt = 0; ; attempt++)
        {
            var res = await _http.SendAsync(makeRequest());
            if ((int)res.StatusCode != 429 || attempt >= MaxRetries) return res;

            var wait = res.Headers.RetryAfter?.Delta
                       ?? (res.Headers.TryGetValues("Retry-After", out var v) && double.TryParse(v.FirstOrDefault(), out var sec)
                           ? TimeSpan.FromSeconds(sec) : TimeSpan.FromSeconds(1));
            res.Dispose();
            await Task.Delay(wait);
        }
    }

    // ---------- 조회 ----------

    /// <summary>DB 의 제목과 URL 을 가져옵니다. (헤더 제목 / "노션에서 열기" 용)</summary>
    public async Task<DatabaseInfo> GetDatabaseInfoAsync()
    {
        using var res = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"databases/{_databaseId}"));
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new HttpRequestException(Describe(res, text));

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        string title = "";
        if (root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.Array)
        {
            title = string.Concat(t.EnumerateArray()
                .Select(p => p.TryGetProperty("plain_text", out var pt) ? pt.GetString() ?? "" : ""));
        }

        string url = root.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(url))
            url = "https://www.notion.so/" + _databaseId.Replace("-", "");

        // 속성 점검: 제목(title)·Done(checkbox) 필수, Detail(rich_text)·Due(date) 선택
        var problems = new List<string>();
        if (root.TryGetProperty("properties", out var dbProps) && dbProps.ValueKind == JsonValueKind.Object)
        {
            bool hasTitle = false;
            foreach (var p in dbProps.EnumerateObject())
            {
                if (TypeOf(p.Value) == "title")
                {
                    _titleProp = p.Name;
                    hasTitle = true;
                    break;
                }
            }
            if (!hasTitle) problems.Add(Strings.SchemaMissingRequired("Name", "title"));
            if (!HasProp(dbProps, "Done", "checkbox"))
                problems.Add(Strings.SchemaMissingRequired("Done", Strings.TypeCheckbox));
            if (!HasProp(dbProps, "Detail", "rich_text"))
                problems.Add(Strings.SchemaMissingOptional("Detail", Strings.TypeText));
            if (!HasProp(dbProps, "Due", "date"))
                problems.Add(Strings.SchemaMissingOptional("Due", Strings.TypeDate));
        }

        return new DatabaseInfo(title, url, problems);
    }

    private static string? TypeOf(JsonElement prop)
        => prop.TryGetProperty("type", out var t) ? t.GetString() : null;

    private static bool HasProp(JsonElement props, string name, string type)
        => props.TryGetProperty(name, out var p) && TypeOf(p) == type;

    /// <summary>미완료(Done=false) 항목을 가져옵니다. 마감 임박 순, 마감일 없는 항목은 맨 뒤.</summary>
    public async Task<List<TodoItem>> GetOpenTodosAsync()
    {
        var list = new List<TodoItem>();
        string? cursor = null;

        do
        {
            var body = new Dictionary<string, object>
            {
                ["filter"] = new { property = "Done", checkbox = new { equals = false } },
                ["sorts"] = new[] { new { timestamp = "created_time", direction = "ascending" } },
                ["page_size"] = 100
            };
            if (cursor is not null) body["start_cursor"] = cursor;

            using var res = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, $"databases/{_databaseId}/query") { Content = ToJson(body) });
            var text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) throw new HttpRequestException(Describe(res, text));

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            foreach (var page in root.GetProperty("results").EnumerateArray())
                list.Add(ParsePage(page));

            cursor = root.TryGetProperty("has_more", out var hm) && hm.ValueKind == JsonValueKind.True &&
                     root.TryGetProperty("next_cursor", out var nc)
                ? nc.GetString()
                : null;
        } while (cursor is not null);

        return list.OrderBy(t => t.Due ?? DateTime.MaxValue).ToList();
    }

    private static TodoItem ParsePage(JsonElement page)
    {
        var props = page.GetProperty("properties");

        string created = "";
        if (page.TryGetProperty("created_time", out var ct) &&
            DateTimeOffset.TryParse(ct.GetString(), out var createdAt))
        {
            created = createdAt.ToLocalTime().ToString("MM.dd");
        }

        // 페이지 아이콘(이모지)
        string iconPrefix = "";
        if (page.TryGetProperty("icon", out var icon) && icon.ValueKind == JsonValueKind.Object &&
            icon.TryGetProperty("emoji", out var emoji))
        {
            iconPrefix = (emoji.GetString() ?? "") + "  ";
        }

        // 마감일(Due 속성)
        DateTime? due = null;
        if (props.TryGetProperty("Due", out var dueProp) &&
            dueProp.TryGetProperty("date", out var dateObj) && dateObj.ValueKind == JsonValueKind.Object &&
            dateObj.TryGetProperty("start", out var start) &&
            DateTime.TryParse(start.GetString(), out var parsed))
        {
            due = parsed.Date;
        }

        // 제목 속성은 이름이 아니라 타입(title)으로 찾습니다.
        string title = "";
        foreach (var p in props.EnumerateObject())
        {
            if (TypeOf(p.Value) == "title")
            {
                title = ReadText(props, p.Name, "title");
                break;
            }
        }

        var item = new TodoItem
        {
            Id = page.GetProperty("id").GetString() ?? "",
            Url = page.GetProperty("url").GetString() ?? "",
            CreatedText = created,
            IconPrefix = iconPrefix,
            Title = title,
            Detail = ReadText(props, "Detail", "rich_text")
        };
        item.SetDue(due);
        return item;
    }

    // ---------- 변경 ----------

    public Task SetDoneAsync(string pageId, bool done)
        => PatchPageAsync(pageId, new { properties = new { Done = new { checkbox = done } } });

    /// <summary>마감일 설정. null 이면 지웁니다.</summary>
    public Task SetDueAsync(string pageId, DateTime? due)
        => PatchPageAsync(pageId, new
        {
            properties = new
            {
                Due = new { date = due is null ? null : new { start = due.Value.ToString("yyyy-MM-dd") } }
            }
        });

    public Task SetDetailAsync(string pageId, string detail)
        => PatchPageAsync(pageId, new { properties = new { Detail = new { rich_text = RichText(detail) } } });

    /// <summary>삭제 = 노션 휴지통으로 이동(archived). false 로 되돌리면 복구됩니다.</summary>
    public Task SetArchivedAsync(string pageId, bool archived)
        => PatchPageAsync(pageId, new { archived });

    public async Task AddAsync(string title, string? detail = null, DateTime? due = null)
    {
        var properties = new Dictionary<string, object>
        {
            [_titleProp] = new { title = RichText(title) }
        };
        if (!string.IsNullOrWhiteSpace(detail))
            properties["Detail"] = new { rich_text = RichText(detail) };
        if (due is not null)
            properties["Due"] = new { date = new { start = due.Value.ToString("yyyy-MM-dd") } };

        var body = new
        {
            parent = new { database_id = _databaseId },
            properties
        };

        using var res = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, "pages") { Content = ToJson(body) });
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(Describe(res, await res.Content.ReadAsStringAsync()));
    }

    private async Task PatchPageAsync(string pageId, object body)
    {
        using var res = await SendAsync(() => new HttpRequestMessage(HttpMethod.Patch, $"pages/{pageId}") { Content = ToJson(body) });
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(Describe(res, await res.Content.ReadAsStringAsync()));
    }

    // ---------- 도우미 ----------

    /// <summary>노션 rich_text 는 조각당 2000자 제한이라 나눠서 보냅니다. 빈 문자열이면 빈 배열(= 내용 지우기).</summary>
    private static object[] RichText(string text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<object>();

        var parts = new List<object>();
        int pos = 0;
        while (pos < text.Length)
        {
            int len = Math.Min(2000, text.Length - pos);
            // 이모지 등 서로게이트 쌍이 경계에서 잘리면 깨지므로 한 글자 앞에서 끊습니다.
            if (pos + len < text.Length && char.IsHighSurrogate(text[pos + len - 1])) len--;
            parts.Add(new { text = new { content = text.Substring(pos, len) } });
            pos += len;
        }
        return parts.ToArray();
    }

    private static string ReadText(JsonElement props, string name, string kind)
    {
        if (!props.TryGetProperty(name, out var prop)) return "";
        if (!prop.TryGetProperty(kind, out var arr) || arr.ValueKind != JsonValueKind.Array) return "";

        var sb = new StringBuilder();
        foreach (var part in arr.EnumerateArray())
        {
            if (part.TryGetProperty("plain_text", out var t)) sb.Append(t.GetString());
        }
        return sb.ToString();
    }

    private static StringContent ToJson(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static string Describe(HttpResponseMessage res, string body)
    {
        string message = body;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m)) message = m.GetString() ?? body;
        }
        catch (JsonException) { /* 본문이 JSON이 아니면 그대로 사용 */ }
        return (int)res.StatusCode switch
        {
            401 => Strings.Http401,
            404 => Strings.Http404,
            _ => $"{(int)res.StatusCode} {message}"
        };
    }
}
