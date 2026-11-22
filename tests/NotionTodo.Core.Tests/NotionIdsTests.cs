using Xunit;

namespace NotionTodoWidget.Tests;

public class NotionIdsTests
{
    private const string Expected = "01234567-89ab-cdef-0123-456789abcdef";

    [Fact]
    public void Parse_AcceptsMobileShareUrl()
        => Assert.Equal(Expected, NotionIds.ParseDatabaseId("https://www.notion.so/ws/Todo-0123456789abcdef0123456789abcdef?v=abc&pvs=4"));

    [Fact]
    public void Parse_AcceptsNotionSiteUrl()
        => Assert.Equal(Expected, NotionIds.ParseDatabaseId("https://my.notion.site/Todo-0123456789abcdef0123456789abcdef"));

    [Fact]
    public void Parse_AcceptsHyphenatedId()
        => Assert.Equal(Expected, NotionIds.ParseDatabaseId(Expected));

    [Fact]
    public void Parse_ReturnsNullForGarbage()
        => Assert.Null(NotionIds.ParseDatabaseId("hello world"));

    [Fact]
    public void Parse_TrimsWhitespaceAndNewlines()
        => Assert.Equal(Expected, NotionIds.ParseDatabaseId("  0123456789abcdef0123456789abcdef\n"));
}
