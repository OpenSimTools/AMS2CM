using static Core.Utils.StringExtensions;

namespace Core.Tests.Utils;

[UnitTest]
public class PostProcessorTest
{
    [Fact]
    public void NormalizeWhitespaces_ReplacesWithSpaces()
    {
        Assert.Equal("", "".NormalizeWhitespaces());
        Assert.Equal("foo bar", "foo bar".NormalizeWhitespaces());
        Assert.Equal("foo bar", "foo\r\v\nbar".NormalizeWhitespaces());
    }

    [Fact]
    public void NormalizeWhitespaces_TrimsInput()
    {
        Assert.Equal("foo", " foo\f".NormalizeWhitespaces());
    }
}
