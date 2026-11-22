using Xunit;

namespace NotionTodoWidget.Tests;

public class QuickAddStringsTests
{
    [Fact]
    public void QuickAddStrings_AreNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(Strings.QuickAddTitle));
        Assert.False(string.IsNullOrWhiteSpace(Strings.QuickAddHint));
        Assert.False(string.IsNullOrWhiteSpace(Strings.BtnAdd));
        Assert.False(string.IsNullOrWhiteSpace(Strings.QuickAddDone));
    }
}
