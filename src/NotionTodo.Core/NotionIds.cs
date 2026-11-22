using System;
using System.Text.RegularExpressions;

namespace NotionTodoWidget;

/// <summary>노션 URL / ID 문자열 해석</summary>
public static class NotionIds
{
    private static readonly Regex Uuid = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);

    private static readonly Regex Hex32 = new(@"[0-9a-fA-F]{32}", RegexOptions.Compiled);

    /// <summary>
    /// 데이터베이스 URL(…/제목-&lt;32자리&gt;?v=…) 또는 ID(하이픈 유무 무관)에서 UUID 형식 ID 를 뽑아냅니다.
    /// 실패하면 null.
    /// </summary>
    public static string? ParseDatabaseId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var s = input.Trim();
        int q = s.IndexOfAny(new[] { '?', '#' });
        if (q >= 0) s = s.Substring(0, q);          // ?v=<보기 ID> 같은 부분 제거

        var uuid = Uuid.Match(s);
        if (uuid.Success) return uuid.Value.ToLowerInvariant();

        var matches = Hex32.Matches(s);
        if (matches.Count == 0) return null;

        // "제목-<ID>" 형태에서는 마지막 32자리가 페이지/DB ID
        var h = matches[matches.Count - 1].Value.ToLowerInvariant();
        return $"{h.Substring(0, 8)}-{h.Substring(8, 4)}-{h.Substring(12, 4)}-{h.Substring(16, 4)}-{h.Substring(20)}";
    }
}
